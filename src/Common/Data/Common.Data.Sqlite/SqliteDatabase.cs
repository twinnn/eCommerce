using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite;

/// <summary>
/// Opens a SQLite connection and runs the given schema DDL statements. Deliberately domain-agnostic —
/// callers own their own table shapes (see <see cref="ProcessedFileTracker"/>, <see cref="AuditLog"/>
/// for the two generic tables this library ships, and any consumer-specific tables it doesn't know about).
/// </summary>
public sealed class SqliteDatabase : IAsyncDisposable
{
    public SqliteConnection Connection { get; }

    private SqliteDatabase(SqliteConnection connection) => Connection = connection;

    public static async Task<SqliteDatabase> OpenAsync(
        string dbPath, IReadOnlyList<string> schemaStatements, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);

        foreach (var statement in schemaStatements)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return new SqliteDatabase(connection);
    }

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
