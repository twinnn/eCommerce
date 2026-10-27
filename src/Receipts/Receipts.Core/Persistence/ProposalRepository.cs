using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Receipts.Contract;

namespace Receipts.Core.Persistence;

/// <summary>
/// Proposals awaiting / having received `apply`. Lines and warnings are stored as JSON columns —
/// this is a handful of rows for a single-operator tool, not a reporting database.
/// </summary>
public sealed class ProposalRepository(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS proposals (
            id TEXT PRIMARY KEY,
            invoice_number TEXT NOT NULL,
            invoice_date TEXT NOT NULL,
            source_file_path TEXT NOT NULL,
            source_file_sha256 TEXT NOT NULL,
            type TEXT NOT NULL,
            existing_receipt_id TEXT NULL,
            existing_receipt_number TEXT NULL,
            header_text TEXT NULL,
            lines_json TEXT NOT NULL,
            freight_to_add TEXT NULL,
            warnings_json TEXT NOT NULL,
            status TEXT NOT NULL,
            failure_reason TEXT NULL,
            result_receipt_number TEXT NULL,
            created_at TEXT NOT NULL,
            applied_at TEXT NULL
        );
        """;

    /// <summary>Inserts a new proposal, or — for an existing id — updates only its outcome columns (status, failure, result, applied_at).</summary>
    public async Task SaveAsync(ReceiptProposal proposal, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO proposals (id, invoice_number, invoice_date, source_file_path, source_file_sha256, type,
                existing_receipt_id, existing_receipt_number, header_text, lines_json, freight_to_add,
                warnings_json, status, failure_reason, result_receipt_number, created_at, applied_at)
            VALUES ($id, $invNum, $invDate, $path, $sha, $type, $exId, $exNum, $header, $lines, $freight,
                $warnings, $status, $failure, $result, $createdAt, $appliedAt)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                failure_reason = excluded.failure_reason,
                result_receipt_number = excluded.result_receipt_number,
                applied_at = excluded.applied_at
            """;
        cmd.Parameters.AddWithValue("$id", proposal.Id);
        cmd.Parameters.AddWithValue("$invNum", proposal.InvoiceNumber);
        cmd.Parameters.AddWithValue("$invDate", proposal.InvoiceDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$path", proposal.SourceFilePath);
        cmd.Parameters.AddWithValue("$sha", proposal.SourceFileSha256);
        cmd.Parameters.AddWithValue("$type", proposal.Type.ToString());
        cmd.Parameters.AddWithValue("$exId", (object?)proposal.ExistingReceiptId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$exNum", (object?)proposal.ExistingReceiptNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$header", (object?)proposal.HeaderText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lines", JsonSerializer.Serialize(proposal.LinesToAdd));
        cmd.Parameters.AddWithValue("$freight", proposal.FreightToAdd?.ToString(CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$warnings", JsonSerializer.Serialize(proposal.Warnings));
        cmd.Parameters.AddWithValue("$status", proposal.Status.ToString());
        cmd.Parameters.AddWithValue("$failure", (object?)proposal.FailureReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$result", (object?)proposal.ResultReceiptNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", proposal.CreatedAt.ToUniversalTime().ToString("O"));
        cmd.Parameters.AddWithValue("$appliedAt", proposal.AppliedAt?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ReceiptProposal>> GetByStatusAsync(ProposalStatus status, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM proposals WHERE status = $status ORDER BY created_at";
        cmd.Parameters.AddWithValue("$status", status.ToString());
        return await ReadProposalsAsync(cmd, ct);
    }

    public async Task<ReceiptProposal?> GetAsync(string id, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM proposals WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        var results = await ReadProposalsAsync(cmd, ct);
        return results.FirstOrDefault();
    }

    /// <summary>Resets a proposal back to PendingApproval, clearing failure/applied bookkeeping. See <see cref="ReceiptsStateStore.ResetProposalAsync"/> for the full operator-facing reset.</summary>
    public async Task ResetAsync(string id, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE proposals
            SET status = $status, failure_reason = NULL, result_receipt_number = NULL, applied_at = NULL
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$status", ProposalStatus.PendingApproval.ToString());
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<IReadOnlyList<ReceiptProposal>> ReadProposalsAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var results = new List<ReceiptProposal>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var lines = JsonSerializer.Deserialize<List<ReceiptLineProposal>>(reader.GetString(reader.GetOrdinal("lines_json"))) ?? [];
            var warnings = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("warnings_json"))) ?? [];
            var freightOrdinal = reader.GetOrdinal("freight_to_add");

            results.Add(new ReceiptProposal
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                InvoiceNumber = reader.GetString(reader.GetOrdinal("invoice_number")),
                InvoiceDate = DateOnly.Parse(reader.GetString(reader.GetOrdinal("invoice_date")), CultureInfo.InvariantCulture),
                SourceFilePath = reader.GetString(reader.GetOrdinal("source_file_path")),
                SourceFileSha256 = reader.GetString(reader.GetOrdinal("source_file_sha256")),
                Type = Enum.Parse<ReceiptProposalType>(reader.GetString(reader.GetOrdinal("type"))),
                ExistingReceiptId = ReadNullableString(reader, "existing_receipt_id"),
                ExistingReceiptNumber = ReadNullableString(reader, "existing_receipt_number"),
                HeaderText = ReadNullableString(reader, "header_text"),
                LinesToAdd = lines,
                FreightToAdd = reader.IsDBNull(freightOrdinal) ? null : decimal.Parse(reader.GetString(freightOrdinal), CultureInfo.InvariantCulture),
                Warnings = warnings,
                Status = Enum.Parse<ProposalStatus>(reader.GetString(reader.GetOrdinal("status"))),
                FailureReason = ReadNullableString(reader, "failure_reason"),
                ResultReceiptNumber = ReadNullableString(reader, "result_receipt_number"),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at")), CultureInfo.InvariantCulture),
                AppliedAt = ReadNullableString(reader, "applied_at") is { } appliedAt
                    ? DateTimeOffset.Parse(appliedAt, CultureInfo.InvariantCulture)
                    : null
            });
        }

        return results;
    }

    private static string? ReadNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
