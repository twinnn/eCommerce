using Receipts.Gabel;

namespace Receipts.Tests;

/// <summary>
/// Assertions here are cross-checked by hand against the printed totals in the real sample PDFs —
/// not just internal consistency. Moved unchanged from Gabel.PrijemkaSync.Tests to prove the parser
/// was not altered by the relocation.
/// </summary>
public class GabelInvoiceParserTests
{
    private static string TestDataPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    [Fact]
    public void Parses_invoice_250334_with_19_lines_and_no_warnings()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("ft2-250334del20251118.pdf"), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);
        Assert.Equal("Gabel", invoice.SupplierName); // added in the merge — the only new assertion
        Assert.Equal("FT 2/250334", invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2025, 11, 18), invoice.InvoiceDate);
        Assert.Equal("EUR", invoice.Currency);
        Assert.Equal(19, invoice.Lines.Count);
        Assert.Equal(160.00m, invoice.FreightAmount);
        Assert.Equal(2491.41m, invoice.PrintedGoodsNetTotal);
        Assert.Equal(2651.41m, invoice.PrintedGrandTotal);
        Assert.Equal(invoice.PrintedGoodsNetTotal, invoice.ComputedGoodsNetTotal);
        Assert.Empty(invoice.Warnings);

        var firstLine = invoice.Lines[0];
        Assert.Equal("7009190321200", firstLine.SupplierCode);
        Assert.Equal("DH PLATINUM SERIES CM.120", firstLine.Description);
        Assert.Equal("PA", firstLine.Unit);
        Assert.Equal(1.0000m, firstLine.Quantity);
        Assert.Equal(94.500m, firstLine.UnitPrice);
        Assert.Equal(35.00m, firstLine.DiscountPercent);
        Assert.Equal(61.43m, firstLine.LineAmount);

        // "CM.120" pattern — confirmed live that this corresponds to a Pohoda card with stk:orderName "120 cm".
        Assert.Equal("120 cm", firstLine.VariantSizeHint);
    }

    [Fact]
    public void Detects_fixed_size_variant_hints_in_their_three_real_forms()
    {
        var parser = new GabelInvoiceParser();

        var ok250334 = parser.TryParse(TestDataPath("ft2-250334del20251118.pdf"), out var invoice250334, out var skipReason1);
        Assert.True(ok250334, skipReason1);
        Assert.NotNull(invoice250334);

        var ok260220 = parser.TryParse(TestDataPath("ft2-260220del20260623.pdf"), out var invoice260220, out var skipReason2);
        Assert.True(ok260220, skipReason2);
        Assert.NotNull(invoice260220);

        // Form 1: "CM.NNN" — e.g. "DH PLATINUM SERIES CM.120".
        var dhPlatinum = invoice250334.Lines.Single(l => l.Description.Contains("DH PLATINUM"));
        Assert.Equal("120 cm", dhPlatinum.VariantSizeHint);

        // Form 2: "NNNCM" — e.g. "X-1.35 ACTIVE ORANGE 135CM".
        var activeOrange135 = invoice260220.Lines.Single(l => l.Description.Contains("135CM"));
        Assert.Equal("135 cm", activeOrange135.VariantSizeHint);

        // Form 3: "Cm NNN" — e.g. "VIPER 3S-TR Cm 115".
        var viper115 = invoice250334.Lines.Single(l => l.Description.Contains("Cm 115"));
        Assert.Equal("115 cm", viper115.VariantSizeHint);
    }

    [Fact]
    public void Does_not_flag_adjustable_length_ranges_or_plain_colour_variants_as_size_variants()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("ft2-260220del20260623.pdf"), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);

        // "73-110CM" is an adjustable-length range (telescopic pole), not a fixed-size variant.
        var nordicEnergy = invoice.Lines.Single(l => l.Description.Contains("NORDIC ENERGY"));
        Assert.Null(nordicEnergy.VariantSizeHint);

        // Colour-only variants (no size in the name) must NOT be flagged as size variants.
        var stretchLiteLilac = invoice.Lines.Single(l => l.Description.Contains("STRETCH LITE CARBON LILAC"));
        Assert.Null(stretchLiteLilac.VariantSizeHint);
    }

    [Fact]
    public void Parses_invoice_260220_with_14_lines_and_no_warnings()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("ft2-260220del20260623.pdf"), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);
        Assert.Equal("FT 2/260220", invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 6, 23), invoice.InvoiceDate);
        Assert.Equal(14, invoice.Lines.Count);
        Assert.Equal(180.00m, invoice.FreightAmount);
        Assert.Equal(3277.11m, invoice.PrintedGoodsNetTotal);
        Assert.Equal(3457.11m, invoice.PrintedGrandTotal);
        Assert.Empty(invoice.Warnings);

        // This invoice is the actual delivery for pre-invoice 5761, which ordered 2 extra IBEX lines
        // that were backordered and are correctly absent here — confirms lines come only from the
        // final invoice, never the pre-invoice.
        Assert.DoesNotContain(invoice.Lines, l => l.Description.Contains("IBEX", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ft2-260014del20260130.pdf")]
    [InlineData("ft2-260097del20260331.pdf")]
    [InlineData("ft2-260180del20260528.pdf")]
    public void Parses_remaining_sample_invoices_without_warnings(string fileName)
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath(fileName), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);
        Assert.NotEmpty(invoice.Lines);
        Assert.Empty(invoice.Warnings);
        Assert.Equal(invoice.PrintedGoodsNetTotal, invoice.ComputedGoodsNetTotal);
    }

    [Fact]
    public void Skips_proforma_preinvoice_with_explanatory_reason()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("pr1-5605del20260123.pdf"), out var invoice, out var skipReason);

        Assert.False(ok);
        Assert.Null(invoice);
        Assert.Contains("PROFORMA", skipReason, StringComparison.OrdinalIgnoreCase);
    }
}
