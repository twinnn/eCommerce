using Microsoft.Extensions.Logging;
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;
using Receipts.Contract;
using Receipts.Core.Persistence;

namespace Receipts.Core;

/// <summary>
/// Turns one <see cref="ParsedInvoice"/> plus a snapshot of recently-listed Pohoda příjemky into a
/// <see cref="ReceiptProposal"/>. Read-only against Pohoda (only stock lookups) — nothing is created
/// here; `scan` only proposes, `apply` writes.
/// </summary>
public sealed class ProposalBuilder
{
    private readonly IStockCardService _stockCards;
    private readonly IVariantDetector _variantDetector;
    private readonly StockMappingCache _stockMappings;
    private readonly ILogger<ProposalBuilder> _logger;

    public ProposalBuilder(
        IStockCardService stockCards,
        IVariantDetector variantDetector,
        StockMappingCache stockMappings,
        ILogger<ProposalBuilder> logger)
    {
        _stockCards = stockCards;
        _variantDetector = variantDetector;
        _stockMappings = stockMappings;
        _logger = logger;
    }

    public async Task<ReceiptProposal> BuildAsync(ParsedInvoice invoice, IReadOnlyList<ExistingReceipt> recentReceipts, CancellationToken ct = default)
    {
        var existing = recentReceipts.FirstOrDefault(r =>
            r.Text.Contains(invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase));

        var existingStockCodes = existing?.Lines
            .Select(l => l.PohodaStockCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var linesToAdd = new List<ReceiptLineProposal>();
        var warnings = new List<string>(invoice.Warnings);

        foreach (var line in invoice.Lines)
        {
            var (stockCode, isNew) = await ResolveStockCodeAsync(line, ct);

            if (existing is not null && existingStockCodes.Contains(stockCode))
            {
                _logger.LogDebug("Riadok {Code} sa v príjemke {Number} už nachádza, preskakujem.", line.SupplierCode, existing.Number);
                continue;
            }

            string? masterCardCode = null;
            var masterCardSearched = false;

            if (isNew && line.VariantSizeHint is not null && line.VariantBaseName is not null)
            {
                masterCardSearched = true;
                masterCardCode = await FindMasterCardAsync(line.VariantBaseName, ct);

                warnings.Add(masterCardCode is not null
                    ? $"'{line.Description}' vyzerá ako variantný produkt (veľkosť {line.VariantSizeHint}). Možná materská karta nájdená: {masterCardCode} ('{line.VariantBaseName}') — mServer XML ju nevie automaticky priradiť, priraď ju ručne v Pohode po založení."
                    : $"'{line.Description}' vyzerá ako variantný produkt (veľkosť {line.VariantSizeHint}), ale karta s názvom '{line.VariantBaseName}' sa v Pohode nenašla — over ručne, či materská karta produktovej rady existuje (možno pod iným názvom), prípadne ju bude treba tiež založiť.");
            }

            linesToAdd.Add(new ReceiptLineProposal
            {
                SupplierCode = line.SupplierCode,
                Description = line.Description,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                PohodaStockCode = stockCode,
                IsNewStockItem = isNew,
                VariantSizeHint = line.VariantSizeHint,
                VariantBaseName = line.VariantBaseName,
                MasterCardCode = masterCardCode,
                MasterCardSearched = masterCardSearched
            });
        }

        decimal? freightToAdd = invoice.FreightAmount > 0 ? invoice.FreightAmount : null;
        if (existing is not null && freightToAdd is not null)
        {
            warnings.Add(
                $"Príjemka {existing.Number} už existuje — over ručne, či už obsahuje dopravu ({freightToAdd:F2} €) ako vedľajšie náklady, aby sa nezdvojila (list response ju momentálne nevie spoľahlivo rozlíšiť od bežnej položky).");
        }

        if (existing is not null && linesToAdd.Count == 0 && freightToAdd is null)
        {
            warnings.Add("Všetky položky z faktúry sa v existujúcej príjemke už nachádzajú — proposal nepridáva nič nové.");
        }

        return new ReceiptProposal
        {
            Id = Guid.NewGuid().ToString("N"),
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            SourceFilePath = invoice.SourceFilePath,
            SourceFileSha256 = invoice.SourceFileSha256,
            Type = existing is null ? ReceiptProposalType.CreateReceipt : ReceiptProposalType.AppendLines,
            ExistingReceiptId = existing?.PohodaId,
            ExistingReceiptNumber = existing?.Number,
            HeaderText = existing is null ? $"{invoice.SupplierName} {invoice.InvoiceNumber}" : null,
            LinesToAdd = linesToAdd,
            FreightToAdd = freightToAdd,
            Warnings = warnings,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Searches Pohoda for a master card named <paramref name="baseName"/> (the variant's description
    /// with the size removed). The underlying search is a substring match, which also returns sibling
    /// variants (e.g. "Gabel SLK-R ... 125 cm" contains "SLK-R"). Those are filtered out via
    /// <see cref="IVariantDetector.LooksLikeSizeVariant"/>, leaving only non-variant candidates.
    /// Returns a code only when exactly one such candidate remains — an ambiguous (more than one) or
    /// empty result both come back as "not found" (null), since guessing among several candidates
    /// would be worse than asking the reviewer to check manually.
    /// </summary>
    private async Task<string?> FindMasterCardAsync(string baseName, CancellationToken ct)
    {
        var matches = await _stockCards.SearchByNameContainingAsync(baseName, ct);
        var candidates = matches.Where(m => !_variantDetector.LooksLikeSizeVariant(m.Name)).ToList();

        if (candidates.Count != 1)
        {
            if (candidates.Count > 1)
            {
                _logger.LogDebug(
                    "Vyhľadanie materskej karty pre '{BaseName}' vrátilo {Count} nejednoznačných kandidátov, preskakujem automatický výber.",
                    baseName, candidates.Count);
            }

            return null;
        }

        return candidates[0].Code;
    }

    private async Task<(string Code, bool IsNew)> ResolveStockCodeAsync(InvoiceLine line, CancellationToken ct)
    {
        var cached = await _stockMappings.GetAsync(line.SupplierCode, ct);
        if (cached is not null)
        {
            return (cached, false);
        }

        var found = await _stockCards.FindBySupplierCodeAsync(line.SupplierCode, ct);
        if (found is not null)
        {
            await _stockMappings.SaveAsync(line.SupplierCode, found, ct);
            return (found, false);
        }

        // New cards use the supplier product code verbatim as stk:code — confirmed live that existing
        // supplier cards follow this convention with no prefix.
        return (line.SupplierCode, true);
    }
}
