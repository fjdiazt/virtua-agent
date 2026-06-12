using System.Text.Json;
using Microsoft.Data.Sqlite;
using VirtuaAgent.OpenAi;

namespace VirtuaAgent.ChatSessions;

public sealed class SqliteChatSessionStore : IChatSessionStore, IAsyncDisposable
{
    private const string CurrentSessionId = "default";
    private readonly string _connectionString;
    private readonly SqliteConnection? _sharedConnection;

    public SqliteChatSessionStore(string connectionString)
    {
        _connectionString = connectionString;
        if (connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            _sharedConnection = new SqliteConnection(connectionString);
            _sharedConnection.Open();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS chat_sessions (
              id TEXT PRIMARY KEY,
              title TEXT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            """, cancellationToken);
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS chat_messages (
              id TEXT PRIMARY KEY,
              session_id TEXT NOT NULL,
              role TEXT NOT NULL,
              content TEXT NOT NULL,
              reasoning_json TEXT NULL,
              created_at TEXT NOT NULL,
              FOREIGN KEY(session_id) REFERENCES chat_sessions(id) ON DELETE CASCADE
            );
            """, cancellationToken);
        await ExecuteAsync(connection, """
            CREATE INDEX IF NOT EXISTS ix_chat_messages_session_created
            ON chat_messages(session_id, created_at);
            """, cancellationToken);
        await EnsureCurrentSessionAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatSessionMessageDto>> ListCurrentMessagesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await EnsureCurrentSessionAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, role, content, reasoning_json, created_at
            FROM chat_messages
            WHERE session_id = $session_id
            ORDER BY created_at ASC, id ASC;
            """;
        Add(command, "$session_id", CurrentSessionId);

        var messages = new List<ChatSessionMessageDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadMessage(reader));
        }

        return messages;
    }

    public async Task<ChatSessionMessageDto> AppendCurrentMessageAsync(SaveChatSessionMessageRequest request, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await EnsureCurrentSessionAsync(connection, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var message = new ChatSessionMessageDto
        {
            Id = "msg_" + Guid.NewGuid().ToString("N"),
            Role = request.Role.Trim().ToLowerInvariant(),
            Content = request.Content,
            Reasoning = request.Reasoning is { Count: > 0 } ? request.Reasoning : null,
            CreatedAt = now
        };

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO chat_messages (id, session_id, role, content, reasoning_json, created_at)
            VALUES ($id, $session_id, $role, $content, $reasoning_json, $created_at);
            UPDATE chat_sessions
            SET updated_at = $created_at
            WHERE id = $session_id;
            """;
        Add(command, "$id", message.Id);
        Add(command, "$session_id", CurrentSessionId);
        Add(command, "$role", message.Role);
        Add(command, "$content", message.Content);
        Add(command, "$reasoning_json", message.Reasoning is null ? null : JsonSerializer.Serialize(message.Reasoning, JsonOptions.Default));
        Add(command, "$created_at", message.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return message;
    }

    public async Task<int> ClearCurrentMessagesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await EnsureCurrentSessionAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM chat_messages
            WHERE session_id = $session_id;
            """;
        Add(command, "$session_id", CurrentSessionId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sharedConnection is not null)
        {
            await _sharedConnection.DisposeAsync();
        }
    }

    private static ChatSessionMessageDto ReadMessage(SqliteDataReader reader)
    {
        var reasoningJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        return new ChatSessionMessageDto
        {
            Id = reader.GetString(0),
            Role = reader.GetString(1),
            Content = reader.GetString(2),
            Reasoning = string.IsNullOrWhiteSpace(reasoningJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(reasoningJson, JsonOptions.Default),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(4))
        };
    }

    private static async Task EnsureCurrentSessionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO chat_sessions (id, title, created_at, updated_at)
            VALUES ($id, $title, $now, $now);
            """;
        Add(command, "$id", CurrentSessionId);
        Add(command, "$title", "Current chat");
        Add(command, "$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_sharedConnection is not null) return _sharedConnection;

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private sealed class ConnectionCleanup : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly bool _dispose;

        private ConnectionCleanup(SqliteConnection connection, bool dispose)
        {
            _connection = connection;
            _dispose = dispose;
        }

        public static ConnectionCleanup Create(SqliteConnection connection, bool dispose) => new(connection, dispose);

        public async ValueTask DisposeAsync()
        {
            if (_dispose) await _connection.DisposeAsync();
        }
    }
}
