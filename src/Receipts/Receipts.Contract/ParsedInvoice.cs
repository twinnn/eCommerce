namespace Receipts.Contract;

/// <summary>One line item extracted from a supplier's final invoice.</summary>
public sealed class InvoiceLine
{
    /// <summary>The supplier's own product code (for Gabel: the 13-digit "ARTICOLO"). Confirmed live against Pohoda: existing skladové karty store this verbatim in stk:code, NOT stk:EAN (which holds a separate, genuine barcode we don't have from the invoice).</summary>
    public required string SupplierCode { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountPercent { get; init; }

    /// <summary>Printed line amount — should equal UnitPrice * Quantity * (1 - DiscountPercent/100).</summary>
    public required decimal LineAmount { get; init; }

    /// <summary>
    /// Set when <see cref="Description"/> contains a single-size pattern like "135CM"/"CM.130"/"Cm 115"
    /// — a strong hint that this is a size variant of a product family. A number RANGE before "CM"
    /// (e.g. "73-110CM") means an adjustable-length pole instead and does NOT set this. The Pohoda XML
    /// agenda has NO field linking a variant to a master/parent card — this hint can only drive a
    /// warning, a best-effort `stk:orderName`, and a by-name master-card lookup (<see cref="VariantBaseName"/>),
    /// never an automatic master-card assignment.
    /// </summary>
    public string? VariantSizeHint { get; init; }

    /// <summary>
    /// Set together with <see cref="VariantSizeHint"/>: <see cref="Description"/> with the size token
    /// removed, e.g. "DH PLATINUM SERIES" from "DH PLATINUM SERIES CM.120". A master card (when one
    /// exists) is typically named identically to its variants minus the size — used to search Pohoda
    /// for a possible master card.
    /// </summary>
    public string? VariantBaseName { get; init; }
}

/// <summary>Result of parsing one supplier final-invoice PDF.</summary>
public sealed class ParsedInvoice
{
    /// <summary>Human-readable supplier name, e.g. "Gabel" — used as the prefix of a new receipt's header text ("Gabel FT 2/260220").</summary>
    public required string SupplierName { get; init; }

    /// <summary>Normalized reference, e.g. "FT 2/260220" — this is what gets embedded in the příjemka text and used to find/match existing receipts.</summary>
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<InvoiceLine> Lines { get; init; }

    /// <summary>Freight/transport charge — mapped to a příjemka accessory charge, never a stock line.</summary>
    public required decimal FreightAmount { get; init; }

    /// <summary>Printed goods net total — sum of line amounts, used as a sanity-check target.</summary>
    public required decimal PrintedGoodsNetTotal { get; init; }

    /// <summary>Printed grand total — goods net + freight.</summary>
    public required decimal PrintedGrandTotal { get; init; }

    public required string SourceFilePath { get; init; }
    public required string SourceFileSha256 { get; init; }

    public List<string> Warnings { get; } = [];

    public decimal ComputedGoodsNetTotal => Lines.Sum(l => l.LineAmount);
}
