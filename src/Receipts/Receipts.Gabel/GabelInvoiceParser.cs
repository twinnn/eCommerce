using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Common.Documents.PdfReader;
using Receipts.Contract;

namespace Receipts.Gabel;

/// <summary>Deterministic parser for Gabel final invoices (`ft2-*.pdf`). Ported unchanged from Gabel.PrijemkaSync.Extraction.</summary>
public sealed partial class GabelInvoiceParser : IInvoiceParser
{
    public const string SupplierName = "Gabel";

    // Header line, e.g.: "33.05548 SK2121512712 57.00039 DISTRIBUTORI CEE EUR FT 2/260220 23/06/2026 1"
    [GeneratedRegex(@"(?<currency>[A-Z]{3})\s+FT\s+(?<num>\d+/\d+)\s+(?<date>\d{2}/\d{2}/\d{4})")]
    private static partial Regex HeaderRegex();

    // Summary line, e.g.: "5.041,70 3.277,11 180,00 3.457,11" (LORDO / NETTO MERCE / SPESE TRASP. / TOTALE IMPONIBILE)
    [GeneratedRegex(@"^(?<lordo>[\d.]+,\d{2})\s+(?<netto>[\d.]+,\d{2})\s+(?<spese>[\d.]+,\d{2})\s+(?<imponibile>[\d.]+,\d{2})$")]
    private static partial Regex SummaryRegex();

    // Item line, e.g.: "7008352650000 STRETCH LITE CARBON LILAC PA 20 35,000 35,00 455,00 I41"
    [GeneratedRegex(@"^(?<code>\d{10,14})\s+(?<desc>.+?)\s+(?<unit>PA|PZ)\s+(?<qty>\d+(?:,\d+)?)\s+(?<price>\d+(?:,\d+)?)\s+(?<discount>\d+(?:,\d+)?)\s+(?<amount>\d+(?:,\d+)?)(?:\s+\S+)?$")]
    private static partial Regex ItemLineRegex();

    [GeneratedRegex(@"^\d{10,14}\b")]
    private static partial Regex LooksLikeItemLineRegex();

    /// <summary>
    /// Parses a Gabel final invoice PDF (`ft2-*.pdf`) into a <see cref="ParsedInvoice"/>.
    /// Returns false with a human-readable <paramref name="skipReason"/> if the PDF is not a final
    /// invoice (e.g. it is a PROFORMA/pre-invoice, which is intentionally out of scope).
    /// </summary>
    public bool TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason)
    {
        var rows = PdfRowReader.ReadRows(pdfPath);
        var fullText = string.Join('\n', rows);

        var headerMatch = HeaderRegex().Match(fullText);
        if (!headerMatch.Success)
        {
            invoice = null;
            skipReason = fullText.Contains("PROFORMA INVOICE", StringComparison.OrdinalIgnoreCase)
                ? "PROFORMA INVOICE (pre-invoice) — mimo rozsahu automatizácie, ignorované."
                : "Nenašlo sa číslo faktúry vo formáte 'FT n/nnnnnn' — pravdepodobne nejde o finálnu Gabel faktúru.";
            return false;
        }

        var invoiceNumber = $"FT {headerMatch.Groups["num"].Value}";
        var invoiceDate = DateOnly.ParseExact(headerMatch.Groups["date"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        var currency = headerMatch.Groups["currency"].Value;

        var warnings = new List<string>();
        var lines = new List<InvoiceLine>();

        foreach (var row in rows)
        {
            var itemMatch = ItemLineRegex().Match(row);
            if (itemMatch.Success)
            {
                var description = itemMatch.Groups["desc"].Value.Trim();
                var (variantSizeHint, variantBaseName) = GabelVariantDetector.Detect(description);
                lines.Add(new InvoiceLine
                {
                    SupplierCode = itemMatch.Groups["code"].Value,
                    Description = description,
                    Unit = itemMatch.Groups["unit"].Value,
                    Quantity = ParseItalianDecimal(itemMatch.Groups["qty"].Value),
                    UnitPrice = ParseItalianDecimal(itemMatch.Groups["price"].Value),
                    DiscountPercent = ParseItalianDecimal(itemMatch.Groups["discount"].Value),
                    VariantSizeHint = variantSizeHint,
                    VariantBaseName = variantBaseName,
                    LineAmount = ParseItalianDecimal(itemMatch.Groups["amount"].Value)
                });
            }
            else if (LooksLikeItemLineRegex().IsMatch(row))
            {
                warnings.Add($"Riadok vyzerá ako položka, ale nesedí na očakávaný formát a bol preskočený: \"{row}\"");
            }
        }

        var summaryMatch = rows.Select(r => SummaryRegex().Match(r)).FirstOrDefault(m => m.Success);
        decimal printedNet;
        decimal freight;
        decimal printedGrandTotal;

        if (summaryMatch is not null)
        {
            printedNet = ParseItalianDecimal(summaryMatch.Groups["netto"].Value);
            freight = ParseItalianDecimal(summaryMatch.Groups["spese"].Value);
            printedGrandTotal = ParseItalianDecimal(summaryMatch.Groups["imponibile"].Value);
        }
        else
        {
            warnings.Add("Súhrnný riadok (NETTO MERCE / SPESE TRASP. / TOTALE IMPONIBILE) sa nenašiel — súčty nebolo možné overiť.");
            printedNet = lines.Sum(l => l.LineAmount);
            freight = 0m;
            printedGrandTotal = printedNet;
        }

        var parsed = new ParsedInvoice
        {
            SupplierName = SupplierName,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            Currency = currency,
            Lines = lines,
            FreightAmount = freight,
            PrintedGoodsNetTotal = printedNet,
            PrintedGrandTotal = printedGrandTotal,
            SourceFilePath = pdfPath,
            SourceFileSha256 = ComputeSha256(pdfPath)
        };

        foreach (var warning in warnings)
        {
            parsed.Warnings.Add(warning);
        }

        var computedVsPrinted = Math.Abs(parsed.ComputedGoodsNetTotal - parsed.PrintedGoodsNetTotal);
        if (computedVsPrinted > 0.02m)
        {
            parsed.Warnings.Add(
                $"Súčet riadkov ({parsed.ComputedGoodsNetTotal:F2}) nesedí na tlačenú hodnotu NETTO MERCE ({parsed.PrintedGoodsNetTotal:F2}) — over manuálne pred zápisom.");
        }

        var computedGrandTotal = parsed.ComputedGoodsNetTotal + parsed.FreightAmount;
        var grandTotalDiff = Math.Abs(computedGrandTotal - parsed.PrintedGrandTotal);
        if (grandTotalDiff > 0.02m)
        {
            parsed.Warnings.Add(
                $"Súčet riadkov + doprava ({computedGrandTotal:F2}) nesedí na tlačený TOTALE IMPONIBILE/NET TO PAY ({parsed.PrintedGrandTotal:F2}) — over manuálne pred zápisom.");
        }

        invoice = parsed;
        skipReason = null;
        return true;
    }

    private static decimal ParseItalianDecimal(string value) =>
        decimal.Parse(value.Replace(".", string.Empty).Replace(',', '.'), CultureInfo.InvariantCulture);

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
