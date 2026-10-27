using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite;

/// <summary>
/// Sha256-based idempotency tracking: has a given file already been processed, and under which
/// reference id (e.g. a proposal id). Generalized from Gabel.PrijemkaSync's `processed_files` table —
/// `proposal_id` renamed `reference_id` since this is no longer receipt-proposal-specific.
/// </summary>
public sealed class ProcessedFileTracker(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS processed_files (
            sha256 TEXT PRIMARY KEY,
            file_path TEXT NOT NULL,
            reference_id TEXT NOT NULL,
            processed_at TEXT NOT NULL
        );
        """;

    public async Task<bool> IsProcessedAsync(string sha256, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM processed_files WHERE sha256 = $sha256";
        cmd.Parameters.AddWithValue("$sha256", sha256);
        var count = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        return count > 0;
    }

    public async Task MarkProcessedAsync(
        string sha256, string filePath, string referenceId, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO processed_files (sha256, file_path, reference_id, processed_at)
            VALUES ($sha256, $path, $referenceId, $now)
            ON CONFLICT(sha256) DO UPDATE SET reference_id = excluded.reference_id, processed_at = excluded.processed_at
            """;
        cmd.Parameters.AddWithValue("$sha256", sha256);
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.Parameters.AddWithValue("$referenceId", referenceId);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ForgetAsync(string referenceId, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM processed_files WHERE reference_id = $referenceId";
        cmd.Parameters.AddWithValue("$referenceId", referenceId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
