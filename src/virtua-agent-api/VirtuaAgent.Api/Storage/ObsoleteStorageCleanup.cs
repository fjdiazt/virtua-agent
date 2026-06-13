using Microsoft.Data.Sqlite;

namespace VirtuaAgent.Storage;

public static class ObsoleteStorageCleanup
{
    public static async Task DropTemporaryBenchTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE IF EXISTS chat_messages;
            DROP TABLE IF EXISTS chat_sessions;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
