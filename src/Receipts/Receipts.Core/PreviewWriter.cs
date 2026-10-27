using System.Text;
using Receipts.Contract;

namespace Receipts.Core;

/// <summary>Writes a human-readable Markdown preview of a proposal for review before `apply`.</summary>
public static class PreviewWriter
{
    public static string Render(ReceiptProposal proposal)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {(proposal.Type == ReceiptProposalType.CreateReceipt ? "Nová príjemka" : "Doplnenie príjemky")} — {proposal.InvoiceNumber}");
        sb.AppendLine();
        sb.AppendLine($"- **Zdrojový súbor**: `{proposal.SourceFilePath}`");
        sb.AppendLine($"- **Proposal ID**: `{proposal.Id}`");
        sb.AppendLine($"- **Dátum faktúry**: {proposal.InvoiceDate:yyyy-MM-dd}");

        if (proposal.Type == ReceiptProposalType.CreateReceipt)
        {
            sb.AppendLine($"- **Text novej príjemky**: `{proposal.HeaderText}`");
        }
        else
        {
            sb.AppendLine($"- **Existujúca príjemka**: {proposal.ExistingReceiptNumber} (id `{proposal.ExistingReceiptId}`)");
        }

        sb.AppendLine();

        if (proposal.LinesToAdd.Count == 0)
        {
            sb.AppendLine("_Žiadne nové riadky na pridanie._");
        }
        else
        {
            sb.AppendLine($"## Riadky na pridanie ({proposal.LinesToAdd.Count})");
            sb.AppendLine();
            sb.AppendLine("| Kód dodávateľa | Popis | Množstvo | MJ | Cena | Zľava % | Skladová karta |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var line in proposal.LinesToAdd)
            {
                var stockNote = line.IsNewStockItem ? $"**{line.PohodaStockCode} (nová karta)**" : line.PohodaStockCode;
                if (line.VariantSizeHint is not null)
                {
                    stockNote += $" ⚑ variant {line.VariantSizeHint}";
                    stockNote += line.MasterCardCode is not null
                        ? $" — materská karta: {line.MasterCardCode}"
                        : line.MasterCardSearched ? " — materská karta nenájdená" : string.Empty;
                }
                sb.AppendLine($"| {line.SupplierCode} | {line.Description} | {line.Quantity} | {line.Unit} | {line.UnitPrice:F3} | {line.DiscountPercent:F2} | {stockNote} |");
            }
        }

        sb.AppendLine();

        if (proposal.FreightToAdd is > 0)
        {
            sb.AppendLine($"**Doprava (vedľajšie náklady)**: {proposal.FreightToAdd:F2} €");
            sb.AppendLine();
        }

        var newCardCount = proposal.LinesToAdd.Count(l => l.IsNewStockItem);
        if (newCardCount > 0)
        {
            sb.AppendLine($"**Nové skladové karty na založenie**: {newCardCount}");
            sb.AppendLine();
        }

        var newVariantCount = proposal.LinesToAdd.Count(l => l.IsNewStockItem && l.VariantSizeHint is not null);
        if (newVariantCount > 0)
        {
            sb.AppendLine(
                $"**⚑ Z toho vyzerá ako variantný produkt**: {newVariantCount} (označené v tabuľke) — mServer XML nevie automaticky priradiť materskú kartu, over ručne v Pohode.");
            sb.AppendLine();
        }

        if (proposal.Warnings.Count > 0)
        {
            sb.AppendLine("## ⚠ Upozornenia");
            sb.AppendLine();
            foreach (var warning in proposal.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Na zápis do Pohody spusti: `apply --id {proposal.Id}`");

        return sb.ToString();
    }
}
