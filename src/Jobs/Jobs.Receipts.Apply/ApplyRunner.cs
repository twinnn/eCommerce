using Microsoft.Extensions.Logging;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Apply;

/// <summary>
/// The bodies of the `apply`, `status` and `reset` commands. Every method returns a process exit code
/// (0 = success). This is the only part of the receipt-sync pipeline that is allowed to trigger a
/// write to Pohoda, and it only ever runs because a human invoked it for a proposal they reviewed.
///
/// <see cref="ProposalApplier"/> promotes a proposal to Applied on success but leaves the failure
/// transition to its caller — that transition lives here: a proposal whose write throws is stored as
/// <see cref="ProposalStatus.Failed"/> with the exception message, an audit row is appended, and the
/// loop continues with the next proposal so one bad invoice cannot block a batch.
/// </summary>
public sealed class ApplyRunner
{
    private readonly ProposalApplier _applier;
    private readonly ReceiptsStateStore _store;
    private readonly WatchOptions _watch;
    private readonly ILogger<ApplyRunner> _logger;

    public ApplyRunner(
        ProposalApplier applier,
        ReceiptsStateStore store,
        WatchOptions watch,
        ILogger<ApplyRunner> logger)
    {
        _applier = applier;
        _store = store;
        _watch = watch;
        _logger = logger;
    }

    public async Task<int> ApplyAsync(string? id, bool all, CancellationToken ct = default)
    {
        var toApply = new List<ReceiptProposal>();

        if (all)
        {
            toApply.AddRange(await _store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval, ct));
        }
        else
        {
            var proposal = await _store.Proposals.GetAsync(id!, ct);
            if (proposal is null)
            {
                _logger.LogError("Proposal {Id} sa nenašiel.", id);
                return 1;
            }

            // Retrying a Failed proposal by explicit --id is allowed: a Failed proposal never reached
            // ProposalStatus.Applied, so no write was recorded as complete for it — safe to retry after
            // fixing whatever caused the failure. `apply --all` intentionally does NOT auto-retry Failed
            // proposals, to avoid silently re-attempting known-bad ones in bulk.
            if (proposal.Status is not (ProposalStatus.PendingApproval or ProposalStatus.Failed))
            {
                _logger.LogError("Proposal {Id} má stav {Status}, nie je čakajúci na schválenie ani zlyhaný.", id, proposal.Status);
                return 1;
            }

            toApply.Add(proposal);
        }

        if (toApply.Count == 0)
        {
            _logger.LogInformation("Žiadne proposaly na spracovanie.");
            return 0;
        }

        var failures = 0;

        foreach (var proposal in toApply)
        {
            try
            {
                // Retrying a previously Failed proposal must not leave its stale reason behind once
                // the write succeeds. Clearing it before the call is enough: ProposalApplier saves the
                // proposal after a successful write, and that upsert persists every outcome column.
                proposal.FailureReason = null;

                await _applier.ApplyAsync(proposal, ct);

                // The Pohoda write already succeeded and is recorded (Applied). Archival is a separate,
                // best-effort step from here on: if it throws (the PDF is open in a viewer, a bad
                // archive folder, permissions), the failure must NOT flow into the catch below — that
                // would overwrite the Applied row with Failed and prompt an operator retry, which would
                // write a second příjemka into the production ERP for the same invoice.
                try
                {
                    Directory.CreateDirectory(_watch.ArchiveFolder);
                    var archivePath = Path.Combine(_watch.ArchiveFolder, Path.GetFileName(proposal.SourceFilePath));
                    if (File.Exists(proposal.SourceFilePath))
                    {
                        File.Move(proposal.SourceFilePath, archivePath, overwrite: true);
                    }

                    _logger.LogInformation(
                        "Proposal {Id} úspešne zapísaný → príjemka {Number}. PDF presunuté do {Archive}.",
                        proposal.Id, proposal.ResultReceiptNumber, archivePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Príjemka {Number} bola zapísaná, ale PDF sa nepodarilo presunúť do archívu: {Path}", proposal.ResultReceiptNumber, proposal.SourceFilePath);
                }
            }
            catch (Exception ex)
            {
                failures++;
                proposal.Status = ProposalStatus.Failed;
                proposal.FailureReason = ex.Message;

                // Deliberately not the caller's token: the failure must be recorded even when the run
                // was cancelled, otherwise a half-finished write leaves no trace at all.
                await _store.Proposals.SaveAsync(proposal, CancellationToken.None);
                await _store.Audit.AppendAsync(proposal.SourceFilePath, $"CHYBA pri zápise: {ex.Message}", CancellationToken.None);

                _logger.LogError(ex, "Proposal {Id} zlyhal.", proposal.Id);
            }
        }

        return failures == 0 ? 0 : 1;
    }

    public async Task<int> StatusAsync(CancellationToken ct = default)
    {
        var pending = await _store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval, ct);
        var failed = await _store.Proposals.GetByStatusAsync(ProposalStatus.Failed, ct);

        _logger.LogInformation("Čakajúce na schválenie ({Count}):", pending.Count);
        foreach (var proposal in pending)
        {
            _logger.LogInformation("  {Id} — {InvoiceNumber} ({Type}), {LineCount} riadkov, {File}",
                proposal.Id, proposal.InvoiceNumber, proposal.Type, proposal.LinesToAdd.Count, Path.GetFileName(proposal.SourceFilePath));
        }

        _logger.LogInformation("Zlyhané ({Count}):", failed.Count);
        foreach (var proposal in failed)
        {
            _logger.LogInformation("  {Id} — {InvoiceNumber}: {Reason}", proposal.Id, proposal.InvoiceNumber, proposal.FailureReason);
        }

        return 0;
    }

    public async Task<int> ResetAsync(string id, CancellationToken ct = default)
    {
        var proposal = await _store.Proposals.GetAsync(id, ct);
        if (proposal is null)
        {
            _logger.LogError("Proposal {Id} sa nenašiel.", id);
            return 1;
        }

        await _store.ResetProposalAsync(id, ct);

        // If the proposal's PDF was already moved to the archive folder (by a previous `apply`), move
        // it back so `scan` / `apply --id` can act on it again.
        var archivedPath = Path.Combine(_watch.ArchiveFolder, Path.GetFileName(proposal.SourceFilePath));
        if (!File.Exists(proposal.SourceFilePath) && File.Exists(archivedPath))
        {
            File.Move(archivedPath, proposal.SourceFilePath, overwrite: false);
            _logger.LogInformation("PDF presunuté späť z archívu: {Path}", proposal.SourceFilePath);
        }

        _logger.LogInformation("Proposal {Id} bol resetovaný na PendingApproval.", id);
        return 0;
    }
}
