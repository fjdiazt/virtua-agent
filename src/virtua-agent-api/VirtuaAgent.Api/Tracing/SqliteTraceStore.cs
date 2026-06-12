using Microsoft.Data.Sqlite;

namespace VirtuaAgent.Tracing;

public sealed class SqliteTraceStore : ITraceStore, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sharedConnection;

    public SqliteTraceStore(string connectionString)
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
            CREATE TABLE IF NOT EXISTS runs (
              run_id TEXT PRIMARY KEY,
              request_id TEXT NOT NULL,
              client_id TEXT NULL,
              status TEXT NOT NULL,
              preview TEXT NOT NULL,
              store INTEGER NOT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              request_json TEXT NULL,
              response_json TEXT NULL,
              error_json TEXT NULL
            );
            """, cancellationToken);

        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS trace_events (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              run_id TEXT NOT NULL,
              type TEXT NOT NULL,
              json TEXT NOT NULL,
              created_at TEXT NOT NULL
            );
            """, cancellationToken);

        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS stage_reasonings (
              run_id TEXT NOT NULL,
              stage_index INTEGER NOT NULL,
              execution_index INTEGER NOT NULL,
              iteration_index INTEGER NOT NULL,
              label TEXT NOT NULL,
              content TEXT NOT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              PRIMARY KEY (run_id, execution_index)
            );
            """, cancellationToken);
    }

    public async Task CreateRunAsync(RunRecord run, CancellationToken cancellationToken = default)
    {
        if (!run.Store) return;

        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (run_id, request_id, client_id, status, preview, store, created_at, updated_at, request_json, response_json, error_json)
            VALUES ($run_id, $request_id, $client_id, $status, $preview, $store, $created_at, $updated_at, $request_json, $response_json, NULL);
            """;
        Add(command, "$run_id", run.RunId);
        Add(command, "$request_id", run.RequestId);
        Add(command, "$client_id", run.ClientId);
        Add(command, "$status", run.Status);
        Add(command, "$preview", run.Preview);
        Add(command, "$store", run.Store ? 1 : 0);
        Add(command, "$created_at", run.CreatedAt.ToString("O"));
        Add(command, "$updated_at", run.UpdatedAt.ToString("O"));
        Add(command, "$request_json", run.RequestJson);
        Add(command, "$response_json", run.ResponseJson);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendEventAsync(string runId, TraceEventRecord traceEvent, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO trace_events (run_id, type, json, created_at)
            VALUES ($run_id, $type, $json, $created_at);
            """;
        Add(command, "$run_id", runId);
        Add(command, "$type", traceEvent.Type);
        Add(command, "$json", traceEvent.Json);
        Add(command, "$created_at", traceEvent.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendReasoningAsync(string runId, ReasoningRecord reasoning, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO stage_reasonings (run_id, stage_index, execution_index, iteration_index, label, content, created_at, updated_at)
            VALUES ($run_id, $stage_index, $execution_index, $iteration_index, $label, $content, $created_at, $updated_at)
            ON CONFLICT(run_id, execution_index) DO UPDATE SET
              content = stage_reasonings.content || excluded.content,
              updated_at = excluded.updated_at;
            """;
        Add(command, "$run_id", runId);
        Add(command, "$stage_index", reasoning.StageIndex);
        Add(command, "$execution_index", reasoning.ExecutionIndex);
        Add(command, "$iteration_index", reasoning.IterationIndex);
        Add(command, "$label", reasoning.Label);
        Add(command, "$content", reasoning.Content);
        Add(command, "$created_at", reasoning.CreatedAt.ToString("O"));
        Add(command, "$updated_at", reasoning.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task CompleteRunAsync(string runId, string responseJson, CancellationToken cancellationToken = default) =>
        UpdateRunStatusAsync(runId, "completed", responseJson, null, cancellationToken);

    public Task FailRunAsync(string runId, string errorJson, CancellationToken cancellationToken = default) =>
        UpdateRunStatusAsync(runId, "failed", null, errorJson, cancellationToken);

    public async Task<RunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        var runs = await QueryRunsAsync(connection, "WHERE run_id = $run_id", command => Add(command, "$run_id", runId), 1, cancellationToken);
        return runs.Count == 0 ? null : runs[0];
    }

    public async Task<IReadOnlyList<RunRecord>> ListRunsAsync(string? status, string? clientId, int limit, CancellationToken cancellationToken = default)
    {
        var clauses = new List<string>();
        var binders = new List<Action<SqliteCommand>>();
        if (!string.IsNullOrWhiteSpace(status))
        {
            clauses.Add("status = $status");
            binders.Add(command => Add(command, "$status", status));
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            clauses.Add("client_id = $client_id");
            binders.Add(command => Add(command, "$client_id", clientId));
        }

        var where = clauses.Count == 0 ? "" : "WHERE " + string.Join(" AND ", clauses);
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        return await QueryRunsAsync(connection, where, command =>
        {
            foreach (var binder in binders) binder(command);
        }, limit, cancellationToken);
    }

    public async Task<int> ClearRunsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = (SqliteTransaction)transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM runs;";
        var deleted = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var eventsCommand = connection.CreateCommand();
        eventsCommand.Transaction = (SqliteTransaction)transaction;
        eventsCommand.CommandText = "DELETE FROM trace_events;";
        await eventsCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var reasoningsCommand = connection.CreateCommand();
        reasoningsCommand.Transaction = (SqliteTransaction)transaction;
        reasoningsCommand.CommandText = "DELETE FROM stage_reasonings;";
        await reasoningsCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var runsCommand = connection.CreateCommand();
        runsCommand.Transaction = (SqliteTransaction)transaction;
        runsCommand.CommandText = "DELETE FROM runs;";
        await runsCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sharedConnection is not null)
        {
            await _sharedConnection.DisposeAsync();
        }
    }

    private async Task UpdateRunStatusAsync(string runId, string status, string? responseJson, string? errorJson, CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var cleanup = ConnectionCleanup.Create(connection, _sharedConnection is null);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE runs
            SET status = $status, updated_at = $updated_at, response_json = COALESCE($response_json, response_json), error_json = COALESCE($error_json, error_json)
            WHERE run_id = $run_id;
            """;
        Add(command, "$run_id", runId);
        Add(command, "$status", status);
        Add(command, "$updated_at", DateTimeOffset.UtcNow.ToString("O"));
        Add(command, "$response_json", responseJson);
        Add(command, "$error_json", errorJson);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<RunRecord>> QueryRunsAsync(SqliteConnection connection, string whereClause, Action<SqliteCommand> bind, int limit, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT run_id, request_id, client_id, status, preview, store, created_at, updated_at, request_json, response_json
            FROM runs
            {whereClause}
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        Add(command, "$limit", limit);
        bind(command);

        var runs = new List<RunRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var runId = reader.GetString(0);
            runs.Add(new RunRecord(
                runId,
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5) == 1,
                DateTimeOffset.Parse(reader.GetString(6)),
                DateTimeOffset.Parse(reader.GetString(7)),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                await QueryEventsAsync(connection, runId, cancellationToken),
                await QueryReasoningsAsync(connection, runId, cancellationToken)));
        }

        return runs;
    }

    private static async Task<List<TraceEventRecord>> QueryEventsAsync(SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT type, json, created_at
            FROM trace_events
            WHERE run_id = $run_id
            ORDER BY id ASC;
            """;
        Add(command, "$run_id", runId);

        var events = new List<TraceEventRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new TraceEventRecord(
                reader.GetString(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2))));
        }

        return events;
    }

    private static async Task<List<ReasoningRecord>> QueryReasoningsAsync(SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT stage_index, execution_index, iteration_index, label, content, created_at, updated_at
            FROM stage_reasonings
            WHERE run_id = $run_id
            ORDER BY execution_index ASC;
            """;
        Add(command, "$run_id", runId);

        var reasonings = new List<ReasoningRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reasonings.Add(new ReasoningRecord(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5)),
                DateTimeOffset.Parse(reader.GetString(6))));
        }

        return reasonings;
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
