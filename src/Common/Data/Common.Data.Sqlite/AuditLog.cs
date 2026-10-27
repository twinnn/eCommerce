using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite;

/// <summary>
/// Append-only, chronological audit trail. Generalized from Gabel.PrijemkaSync's `audit_log` table —
/// `source_file` renamed `source` since this is no longer PDF-invoice-specific.
/// </summary>
public sealed class AuditLog(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS audit_log (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            source TEXT NOT NULL,
            message TEXT NOT NULL
        );
        """;

    public async Task AppendAsync(string source, string message, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO audit_log (timestamp, source, message) VALUES ($ts, $source, $msg)";
        cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$source", source);
        cmd.Parameters.AddWithValue("$msg", message);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<(DateTimeOffset Timestamp, string Source, string Message)>> ReadAllAsync(
        CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT timestamp, source, message FROM audit_log ORDER BY id";

        var results = new List<(DateTimeOffset, string, string)>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return results;
    }
}
