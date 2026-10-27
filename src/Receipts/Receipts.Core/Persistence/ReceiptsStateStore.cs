using Common.Data.Sqlite;

namespace Receipts.Core.Persistence;

/// <summary>
/// The receipt-sync pipeline's local state: one SQLite file holding the two generic tables from
/// <c>Common.Data.Sqlite</c> (processed files for idempotency, audit log) and the two receipt-specific
/// tables (<see cref="StockMappingCache"/>, <see cref="ProposalRepository"/>).
/// </summary>
public sealed class ReceiptsStateStore : IAsyncDisposable
{
    private readonly SqliteDatabase _db;

    public ProcessedFileTracker ProcessedFiles { get; }
    public AuditLog Audit { get; }
    public StockMappingCache StockMappings { get; }
    public ProposalRepository Proposals { get; }

    private ReceiptsStateStore(SqliteDatabase db)
    {
        _db = db;
        ProcessedFiles = new ProcessedFileTracker(db.Connection);
        Audit = new AuditLog(db.Connection);
        StockMappings = new StockMappingCache(db.Connection);
        Proposals = new ProposalRepository(db.Connection);
    }

    public static async Task<ReceiptsStateStore> OpenAsync(string dbPath, CancellationToken ct = default)
    {
        var db = await SqliteDatabase.OpenAsync(dbPath,
            [ProcessedFileTracker.Schema, AuditLog.Schema, StockMappingCache.Schema, ProposalRepository.Schema], ct);
        return new ReceiptsStateStore(db);
    }

    /// <summary>
    /// Resets a proposal back to PendingApproval and forgets that its source file was processed, so it
    /// can be re-scanned or re-applied from scratch. Use this when `apply` reported an outcome that
    /// doesn't match what actually happened in Pohoda and the operator needs a clean retry.
    /// </summary>
    public async Task ResetProposalAsync(string proposalId, CancellationToken ct = default)
    {
        await Proposals.ResetAsync(proposalId, ct);
        await ProcessedFiles.ForgetAsync(proposalId, ct);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
