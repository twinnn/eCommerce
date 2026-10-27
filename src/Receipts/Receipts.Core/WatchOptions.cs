namespace Receipts.Core;

/// <summary>
/// Folder and database locations for the receipt-sync pipeline (config section "Watch").
/// Relative paths are resolved by each executable against its own base directory, so a built
/// `.exe` behaves the same whatever the current working directory is.
/// </summary>
public sealed class WatchOptions
{
    public const string SectionName = "Watch";

    /// <summary>Folder the scan job reads supplier invoice PDFs from (top level only, `*.pdf`).</summary>
    public required string InboxFolder { get; init; }

    /// <summary>Folder a PDF is moved into once its proposal has been successfully applied to Pohoda.</summary>
    public required string ArchiveFolder { get; init; }

    /// <summary>Folder the human-readable Markdown previews are written to, for review before `apply`.</summary>
    public required string PendingApprovalFolder { get; init; }

    /// <summary>SQLite file holding processed files, the audit log, the stock-code mapping cache and the proposals.</summary>
    public string DatabasePath { get; init; } = "data/receipts.db";

    /// <summary>How long the scan worker waits between two inbox sweeps. Bound from a "hh:mm:ss" string.</summary>
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMinutes(15);
}
