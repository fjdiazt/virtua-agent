using Microsoft.Data.Sqlite;

namespace VirtuaAgent.Settings;

public sealed class SqlitePipelineSettingsStore : IPipelineSettingsStore, IAsyncDisposable
{
    private const string PipelineProtocolKey = "pipeline_protocol";
    private readonly string _connectionString;
    private readonly SqliteConnection? _sharedConnection;

    public SqlitePipelineSettingsStore(string connectionString)
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
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS app_settings (
              key TEXT PRIMARY KEY,
              value TEXT NULL,
              updated_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PipelineSettingsDefinition> GetAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT value
            FROM app_settings
            WHERE key = $key;
            """;
        Add(command, "$key", PipelineProtocolKey);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return new PipelineSettingsDefinition
        {
            PipelineProtocol = value is null or DBNull ? null : value.ToString()
        };
    }

    public async Task<PipelineSettingsDefinition> SaveAsync(
        SavePipelineSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var pipelineProtocol = Normalize(request.PipelineProtocol);
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        if (pipelineProtocol is null)
        {
            command.CommandText = "DELETE FROM app_settings WHERE key = $key;";
            Add(command, "$key", PipelineProtocolKey);
        }
        else
        {
            command.CommandText = """
                INSERT INTO app_settings (key, value, updated_at)
                VALUES ($key, $value, $now)
                ON CONFLICT(key) DO UPDATE SET
                  value = excluded.value,
                  updated_at = excluded.updated_at;
                """;
            Add(command, "$key", PipelineProtocolKey);
            Add(command, "$value", pipelineProtocol);
            Add(command, "$now", DateTimeOffset.UtcNow.ToString("O"));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
        return new PipelineSettingsDefinition { PipelineProtocol = pipelineProtocol };
    }

    public async ValueTask DisposeAsync()
    {
        if (_sharedConnection is not null)
        {
            await _sharedConnection.DisposeAsync();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_sharedConnection is not null) return _sharedConnection;

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
