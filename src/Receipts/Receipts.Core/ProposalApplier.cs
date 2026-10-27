using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;
using Receipts.Contract;
using Receipts.Core.Persistence;

namespace Receipts.Core;

/// <summary>
/// Executes an already-approved <see cref="ReceiptProposal"/> against Pohoda: (re-)verifies stock
/// mappings, creates missing skladové karty, then creates or updates the příjemka. This is the only
/// place in the receipt-sync pipeline that performs a write against Pohoda mServer.
///
/// Failure semantics: this class only promotes a proposal to <see cref="ProposalStatus.Applied"/> on
/// success — the caller owns the <see cref="ProposalStatus.Failed"/> / <see cref="ReceiptProposal.FailureReason"/>
/// transition when <see cref="ApplyAsync"/> throws. If a write throws part-way through, any stock
/// cards already created and mappings already saved are deliberately left in place: re-applying is
/// idempotent, since the re-check via <see cref="IStockCardService.FindBySupplierCodeAsync"/> skips
/// cards that now exist. Audit rows are appended only after a successful Pohoda write, so a failed
/// apply leaves no audit trail — the caller is responsible for recording the failure itself.
/// </summary>
public sealed class ProposalApplier
{
    private readonly IStockCardService _stockCards;
    private readonly IReceiptService _receipts;
    private readonly ReceiptsStateStore _store;
    private readonly MatchingOptions _matching;
    private readonly ILogger<ProposalApplier> _logger;

    public ProposalApplier(
        IStockCardService stockCards,
        IReceiptService receipts,
        ReceiptsStateStore store,
        IOptions<MatchingOptions> matching,
        ILogger<ProposalApplier> logger)
    {
        _stockCards = stockCards;
        _receipts = receipts;
        _store = store;
        _matching = matching.Value;
        _logger = logger;
    }

    public async Task ApplyAsync(ReceiptProposal proposal, CancellationToken ct = default)
    {
        var auditLines = new List<string>();
        var linesToWrite = new List<ReceiptLineToWrite>();

        foreach (var line in proposal.LinesToAdd)
        {
            var stockCode = line.PohodaStockCode;

            if (line.IsNewStockItem)
            {
                // Re-verify: the card may have been created since `scan` ran.
                var nowExists = await _stockCards.FindBySupplierCodeAsync(line.SupplierCode, ct);
                if (nowExists is null)
                {
                    await _stockCards.CreateAsync(line.PohodaStockCode, line.Description, line.VariantSizeHint, ct);
                    var variantNote = line.VariantSizeHint is not null
                        ? line.MasterCardCode is not null
                            ? $" [variant {line.VariantSizeHint} — možná materská karta {line.MasterCardCode}, priraď ručne v Pohode]"
                            : $" [variant {line.VariantSizeHint} — materská karta sa nenašla, over ručne v Pohode]"
                        : string.Empty;
                    auditLines.Add($"Založená nová skladová karta {line.PohodaStockCode} '{line.Description}'.{variantNote}");
                }
                else
                {
                    stockCode = nowExists;
                    auditLines.Add($"Skladová karta pre kód {line.SupplierCode} medzičasom vznikla ({nowExists}), nová sa nezakladala.");
                }

                await _store.StockMappings.SaveAsync(line.SupplierCode, stockCode, ct);
            }

            linesToWrite.Add(new ReceiptLineToWrite(stockCode, line.Description, line.Quantity, line.Unit, line.UnitPrice, line.DiscountPercent));
            auditLines.Add($"Riadok: {line.Description} ({line.SupplierCode}) — {line.Quantity} {line.Unit} × {line.UnitPrice:F3}, zľava {line.DiscountPercent:F2} %.");
        }

        string resultNumber;

        if (proposal.Type == ReceiptProposalType.CreateReceipt)
        {
            resultNumber = await _receipts.CreateAsync(proposal.HeaderText!, proposal.InvoiceDate, linesToWrite, proposal.FreightToAdd, ct);
            auditLines.Insert(0, $"Vytvorená nová príjemka '{proposal.HeaderText}' (č. {resultNumber}).");
        }
        else
        {
            var since = DateOnly.FromDateTime(DateTime.Today.AddMonths(-_matching.LookbackMonths));
            var recent = await _receipts.ListRecentAsync(since, _matching.ReceiptListLimit, ct);
            var existing = recent.FirstOrDefault(r => r.PohodaId == proposal.ExistingReceiptId)
                ?? throw new InvalidOperationException($"Príjemka {proposal.ExistingReceiptId} sa už v Pohode nenašla — mohla byť medzičasom zmenená alebo zmazaná. Spusti 'scan' znova.");

            var unparsedCount = existing.Lines.Count(l => l.HasUnparsedNumbers);
            if (unparsedCount > 0)
            {
                _logger.LogWarning(
                    "{Count} existing line(s) on příjemka {ExistingReceiptId} could not be fully parsed (quantity/price/discount) and will be resent with zeros.",
                    unparsedCount, proposal.ExistingReceiptId);
            }

            var mergedLines = existing.Lines
                .Select(l => new ReceiptLineToWrite(l.PohodaStockCode, l.Text, l.Quantity, l.Unit, l.UnitPrice, l.DiscountPercent))
                .Concat(linesToWrite)
                .ToList();

            var freight = proposal.FreightToAdd; // note: possible duplicate if the existing příjemka already carries freight — flagged as a Warning in the preview, not auto-detected.
            await _receipts.UpdateAsync(proposal.ExistingReceiptId!, existing.Text, mergedLines, freight, ct);
            resultNumber = existing.Number;
            auditLines.Insert(0, $"Doplnená existujúca príjemka '{existing.Text}' (č. {resultNumber}) o {linesToWrite.Count} riadkov.");
            if (unparsedCount > 0)
            {
                auditLines.Insert(1, $"POZOR: {unparsedCount} existujúcich riadkov príjemky sa nepodarilo úplne načítať (množstvo/cena/zľava) — pri zápise sa odošlú s nulami. Over príjemku ručne v Pohode.");
            }
        }

        if (proposal.FreightToAdd is > 0)
        {
            auditLines.Add($"Pridaná doprava (vedľajšie náklady): {proposal.FreightToAdd:F2} €.");
        }

        proposal.Status = ProposalStatus.Applied;
        proposal.ResultReceiptNumber = resultNumber;
        proposal.AppliedAt = DateTimeOffset.UtcNow;
        await _store.Proposals.SaveAsync(proposal, ct);

        foreach (var line in auditLines)
        {
            _logger.LogInformation("{Line}", line);
            await _store.Audit.AppendAsync(proposal.SourceFilePath, line, ct);
        }
    }
}
