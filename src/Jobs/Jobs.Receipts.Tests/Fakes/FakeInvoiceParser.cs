using Receipts.Contract;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>
/// Stands in for a supplier's PDF parser: returns a pre-registered <see cref="ParsedInvoice"/> for a
/// known file name and "skips" everything else, so scan tests need no real PDF.
/// </summary>
internal sealed class FakeInvoiceParser : IInvoiceParser
{
    /// <summary>Keyed by file name. Any inbox file missing from this map is reported as skipped.</summary>
    public Dictionary<string, ParsedInvoice> Invoices { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string SkipReason { get; set; } = "PROFORMA INVOICE (pre-invoice) — mimo rozsahu automatizácie, ignorované.";

    /// <summary>Every path the runner handed to the parser, in order.</summary>
    public List<string> ParsedPaths { get; } = [];

    public bool TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason)
    {
        ParsedPaths.Add(pdfPath);

        if (Invoices.TryGetValue(Path.GetFileName(pdfPath), out var found))
        {
            invoice = found;
            skipReason = null;
            return true;
        }

        invoice = null;
        skipReason = SkipReason;
        return false;
    }
}
