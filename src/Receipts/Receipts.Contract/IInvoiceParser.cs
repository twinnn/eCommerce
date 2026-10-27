namespace Receipts.Contract;

/// <summary>
/// Supplier plugin: turns one invoice PDF into a <see cref="ParsedInvoice"/>. One implementation per
/// supplier (e.g. <c>Receipts.Gabel.GabelInvoiceParser</c>); <c>Receipts.Core</c> never depends on a
/// specific supplier's types.
/// </summary>
public interface IInvoiceParser
{
    /// <summary>
    /// Returns true and a populated <paramref name="invoice"/> for a final invoice this parser
    /// understands. Returns false with a human-readable <paramref name="skipReason"/> for anything
    /// else (a proforma/pre-invoice, an unrecognized layout) — skipping is a normal outcome, not an error.
    /// </summary>
    bool TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason);
}
