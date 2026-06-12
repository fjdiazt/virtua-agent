using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace VirtuaAgent.PipelineModels;

public sealed class SqlitePipelineModelStore : IPipelineModelStore, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sharedConnection;
    private static readonly PipelineModelDefinition PipelineTestFixture = new()
    {
        Id = "virtua-agent-test",
        OwnedBy = "virtua-agent",
        Pipeline = new PipelineRequestDto
        {
            DefaultModel = "gemma-4-26B-A4B-it-uncensored-vision",
            DefaultTemperature = 0.2,
            Stages =
            [
                new PipelineStageRequestDto
                {
                    Type = "single_agent",
                    Name = "Draft",
                    Instructions = "Answer the user's request in 3 short bullet points. Include the marker [draft].",
                    Agent = new AgentRequestDto(),
                    Agents = []
                },
                new PipelineStageRequestDto
                {
                    Type = "single_agent",
                    Name = "Tighten",
                    Repeat = 2,
                    Instructions = "Rewrite the previous stage output to be shorter and sharper while preserving meaning. Remove weak wording. Include the marker [tightened-iterated].",
                    Agent = new AgentRequestDto(),
                    Agents = []
                },
                new PipelineStageRequestDto
                {
                    Type = "single_agent",
                    Name = "Apply rules",
                    Instructions = "Mutate the previous stage output by applying these rules: use exactly 2 bullets, each bullet must be under 12 words, and include one concrete noun in each bullet. Include the marker [rules-applied].",
                    Agent = new AgentRequestDto(),
                    Agents = []
                }
            ]
        }
    };

    public SqlitePipelineModelStore(string connectionString)
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
            CREATE TABLE IF NOT EXISTS pipeline_models (
              id TEXT PRIMARY KEY,
              owned_by TEXT NULL,
              pipeline_json TEXT NOT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await SeedFixturesAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<PipelineModelDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, owned_by, pipeline_json
            FROM pipeline_models
            ORDER BY id ASC;
            """;
        var models = new List<PipelineModelDefinition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            models.Add(ReadModel(reader));
        }

        return models;
    }

    public async Task<PipelineModelDefinition?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, owned_by, pipeline_json
            FROM pipeline_models
            WHERE id = $id;
            """;
        Add(command, "$id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadModel(reader) : null;
    }

    public async Task SaveAsync(PipelineModelDefinition model, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pipeline_models (id, owned_by, pipeline_json, created_at, updated_at)
            VALUES ($id, $owned_by, $pipeline_json, $now, $now)
            ON CONFLICT(id) DO UPDATE SET
              owned_by = excluded.owned_by,
              pipeline_json = excluded.pipeline_json,
              updated_at = excluded.updated_at;
            """;
        Add(command, "$id", model.Id);
        Add(command, "$owned_by", model.OwnedBy);
        Add(command, "$pipeline_json", JsonSerializer.Serialize(model.Pipeline, VirtuaAgent.OpenAi.JsonOptions.Default));
        Add(command, "$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM pipeline_models WHERE id = $id;";
        Add(command, "$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sharedConnection is not null)
        {
            await _sharedConnection.DisposeAsync();
        }
    }

    private static PipelineModelDefinition ReadModel(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        OwnedBy = reader.IsDBNull(1) ? null : reader.GetString(1),
        Pipeline = JsonSerializer.Deserialize<PipelineRequestDto>(reader.GetString(2), VirtuaAgent.OpenAi.JsonOptions.Default)
    };

    private static async Task SeedFixturesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO pipeline_models (id, owned_by, pipeline_json, created_at, updated_at)
            VALUES ($id, $owned_by, $pipeline_json, $now, $now);
            """;
        Add(command, "$id", PipelineTestFixture.Id);
        Add(command, "$owned_by", PipelineTestFixture.OwnedBy);
        Add(command, "$pipeline_json", JsonSerializer.Serialize(PipelineTestFixture.Pipeline, VirtuaAgent.OpenAi.JsonOptions.Default));
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
