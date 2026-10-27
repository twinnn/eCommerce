using Receipts.Contract;
using Receipts.Core;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class PreviewWriterTests
{
    [Fact]
    public void Render_describes_a_new_receipt_with_its_lines_variant_flags_freight_and_apply_command()
    {
        var proposal = Make.Proposal(
            [
                Make.LineProposal("7009190321200", "DH PLATINUM SERIES CM.120", isNewStockItem: false, quantity: 1m, unitPrice: 94.5m, variantSizeHint: "120 cm"),
                Make.LineProposal("7009181471300", "SLD KIT LIGHT BLUE 130CM", isNewStockItem: true, quantity: 2m, unitPrice: 40m,
                    variantSizeHint: "130 cm", variantBaseName: "SLD KIT LIGHT BLUE", masterCardCode: "7009181470000", masterCardSearched: true),
                Make.LineProposal("7009999990000", "NEW POLE 140CM", isNewStockItem: true, variantSizeHint: "140 cm", variantBaseName: "NEW POLE", masterCardSearched: true)
            ],
            freightToAdd: 160m,
            id: "abc123");

        var markdown = PreviewWriter.Render(proposal);

        Assert.StartsWith("# Nová príjemka — FT 2/260220", markdown);
        Assert.Contains("- **Zdrojový súbor**: `C:\\faktury\\ft2-260220del20260623.pdf`", markdown);
        Assert.Contains("- **Proposal ID**: `abc123`", markdown);
        Assert.Contains("- **Dátum faktúry**: 2026-06-23", markdown);
        Assert.Contains("- **Text novej príjemky**: `Gabel FT 2/260220`", markdown);
        Assert.DoesNotContain("Existujúca príjemka", markdown);

        Assert.Contains("## Riadky na pridanie (3)", markdown);
        Assert.Contains("| Kód dodávateľa | Popis | Množstvo | MJ | Cena | Zľava % | Skladová karta |", markdown);
        Assert.Contains($"| 7009190321200 | DH PLATINUM SERIES CM.120 | 1 | PA | {94.5m.ToString("F3")} | {35m.ToString("F2")} | 7009190321200 ⚑ variant 120 cm |", markdown);
        Assert.Contains("| **7009181471300 (nová karta)** ⚑ variant 130 cm — materská karta: 7009181470000 |", markdown);
        Assert.Contains("| **7009999990000 (nová karta)** ⚑ variant 140 cm — materská karta nenájdená |", markdown);

        Assert.Contains($"**Doprava (vedľajšie náklady)**: {160m.ToString("F2")} €", markdown);
        Assert.Contains("**Nové skladové karty na založenie**: 2", markdown);
        Assert.Contains("**⚑ Z toho vyzerá ako variantný produkt**: 2 (označené v tabuľke)", markdown);
        Assert.DoesNotContain("## ⚠ Upozornenia", markdown);
        Assert.EndsWith("Na zápis do Pohody spusti: `apply --id abc123`" + Environment.NewLine, markdown);
    }

    [Fact]
    public void Render_describes_an_append_to_an_existing_receipt_and_lists_warnings()
    {
        var proposal = Make.Proposal(
            [],
            type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42",
            existingReceiptNumber: "26PR00012",
            freightToAdd: null,
            invoiceNumber: "FT 2/250334",
            warnings: ["prvé upozornenie", "druhé upozornenie"]);

        var markdown = PreviewWriter.Render(proposal);

        Assert.StartsWith("# Doplnenie príjemky — FT 2/250334", markdown);
        Assert.Contains("- **Existujúca príjemka**: 26PR00012 (id `42`)", markdown);
        Assert.DoesNotContain("Text novej príjemky", markdown);
        Assert.Contains("_Žiadne nové riadky na pridanie._", markdown);
        Assert.DoesNotContain("Doprava", markdown);
        Assert.DoesNotContain("Nové skladové karty", markdown);
        Assert.Contains("## ⚠ Upozornenia", markdown);
        Assert.Contains("- prvé upozornenie", markdown);
        Assert.Contains("- druhé upozornenie", markdown);
    }
}
