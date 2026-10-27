using Microsoft.Extensions.Logging;
using Pohoda.Contract.Services;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Scan;

/// <summary>Outcome of one inbox sweep.</summary>
public readonly record struct ScanResult(int FilesFound, int ProposalsCreated, int Skipped);

/// <summary>
/// One sweep of the invoice inbox: parse each PDF, skip the ones that are not a final invoice or were
/// already processed, build a <see cref="ReceiptProposal"/> for the rest and write a Markdown preview
/// for a human to approve. Read-only against Pohoda — the only writes are to the local state store and
/// the preview folder; <c>Jobs.Receipts.Apply</c> is the only thing that writes to Pohoda.
///
/// Files are processed strictly sequentially: the <see cref="ReceiptsStateStore"/>'s SQLite connection
/// is not thread-safe, and proposals are cheap enough that a parallel sweep would buy nothing.
///
/// A sweep also skips a file whose parsed hash already has an open (PendingApproval or Failed)
/// proposal, even when that file is no longer marked processed. This is what keeps `apply reset`
/// (which forgets the processed-file row and moves the PDF back to the inbox) from producing a
/// second, duplicate proposal for the same invoice on the next scheduled sweep.
/// </summary>
public sealed class ScanRunner
{
    private readonly IReceiptService _receipts;
    private readonly IInvoiceParser _parser;
    private readonly ProposalBuilder _builder;
    private readonly ReceiptsStateStore _store;
    private readonly WatchOptions _watch;
    private readonly MatchingOptions _matching;
    private readonly ILogger<ScanRunner> _logger;

    public ScanRunner(
        IReceiptService receipts,
        IInvoiceParser parser,
        ProposalBuilder builder,
        ReceiptsStateStore store,
        WatchOptions watch,
        MatchingOptions matching,
        ILogger<ScanRunner> logger)
    {
        _receipts = receipts;
        _parser = parser;
        _builder = builder;
        _store = store;
        _watch = watch;
        _matching = matching;
        _logger = logger;
    }

    public async Task<ScanResult> RunOnceAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_watch.InboxFolder);
        Directory.CreateDirectory(_watch.PendingApprovalFolder);

        // Sorted so a sweep processes the inbox in a predictable order — Directory.EnumerateFiles
        // makes no ordering guarantee.
        var pdfFiles = Directory.EnumerateFiles(_watch.InboxFolder, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _logger.LogInformation("Nájdených {Count} PDF v priečinku {Folder}.", pdfFiles.Count, _watch.InboxFolder);

        var since = DateOnly.FromDateTime(DateTime.Today.AddMonths(-_matching.LookbackMonths));
        var recentReceipts = await _receipts.ListRecentAsync(since, _matching.ReceiptListLimit, ct);
        _logger.LogInformation("Načítaných {Count} príjemiek z Pohody od {Since:yyyy-MM-dd} na porovnanie.", recentReceipts.Count, since);

        var pending = await _store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval, ct);
        var failed = await _store.Proposals.GetByStatusAsync(ProposalStatus.Failed, ct);
        var openProposalHashes = pending.Concat(failed)
            .Select(p => p.SourceFileSha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var skipped = 0;

        foreach (var pdfPath in pdfFiles)
        {
            ct.ThrowIfCancellationRequested();

            if (!_parser.TryParse(pdfPath, out var invoice, out var skipReason))
            {
                _logger.LogInformation("Preskočené {File}: {Reason}", Path.GetFileName(pdfPath), skipReason);
                skipped++;
                continue;
            }

            if (await _store.ProcessedFiles.IsProcessedAsync(invoice!.SourceFileSha256, ct))
            {
                _logger.LogInformation("Preskočené {File}: už bolo spracované skôr.", Path.GetFileName(pdfPath));
                skipped++;
                continue;
            }

            if (openProposalHashes.Contains(invoice.SourceFileSha256))
            {
                _logger.LogInformation(
                    "Preskočené {File}: pre tento súbor už existuje neuzavretý návrh (čaká na schválenie alebo zlyhal).",
                    Path.GetFileName(pdfPath));
                skipped++;
                continue;
            }

            var proposal = await _builder.BuildAsync(invoice, recentReceipts, ct);
            await _store.Proposals.SaveAsync(proposal, ct);
            await _store.ProcessedFiles.MarkProcessedAsync(invoice.SourceFileSha256, pdfPath, proposal.Id, ct);

            var previewPath = Path.Combine(
                _watch.PendingApprovalFolder,
                $"{proposal.Id}_{Path.GetFileNameWithoutExtension(pdfPath)}.md");
            await File.WriteAllTextAsync(previewPath, PreviewWriter.Render(proposal), ct);

            _logger.LogInformation(
                "{File} → proposal {Id} ({Type}), {LineCount} riadkov na pridanie, {WarningCount} upozornení. Náhľad: {Preview}",
                Path.GetFileName(pdfPath), proposal.Id, proposal.Type, proposal.LinesToAdd.Count, proposal.Warnings.Count, previewPath);

            created++;
        }

        _logger.LogInformation(
            "Scan hotový. Vytvorených {Count} nových proposalov na schválenie v {Folder}.", created, _watch.PendingApprovalFolder);

        return new ScanResult(pdfFiles.Count, created, skipped);
    }
}
