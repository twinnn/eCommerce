namespace Receipts.Contract;

public enum ReceiptProposalType
{
    /// <summary>No existing příjemka references this invoice number — create a new one with all lines.</summary>
    CreateReceipt,

    /// <summary>An existing příjemka already references this invoice number — only append lines missing from it.</summary>
    AppendLines
}

public enum ProposalStatus
{
    PendingApproval,
    Applied,
    Failed
}

public sealed class ReceiptLineProposal
{
    public required string SupplierCode { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountPercent { get; init; }

    /// <summary>Pohoda stock code this line resolves to (may not exist yet — see <see cref="IsNewStockItem"/>).</summary>
    public required string PohodaStockCode { get; init; }

    /// <summary>True if a new skladová karta must be created for this line before/with the příjemka.</summary>
    public required bool IsNewStockItem { get; init; }

    /// <summary>Copied from <see cref="InvoiceLine.VariantSizeHint"/> — set when this looks like a size variant (e.g. "120 cm"). Used to set `stk:orderName` on create and to warn the reviewer that mServer cannot auto-link it to a master card.</summary>
    public string? VariantSizeHint { get; init; }

    /// <summary>Copied from <see cref="InvoiceLine.VariantBaseName"/> — the description without the size, used to search Pohoda by name for a possible master card.</summary>
    public string? VariantBaseName { get; init; }

    /// <summary>
    /// Result of searching Pohoda for a stock card named <see cref="VariantBaseName"/>: its code if
    /// exactly one non-variant candidate was found, or null (only meaningful when
    /// <see cref="VariantSizeHint"/> is set — see <see cref="MasterCardSearched"/> to distinguish
    /// "not searched" from "searched, not found").
    /// </summary>
    public string? MasterCardCode { get; init; }

    /// <summary>True once a master-card-by-name search was attempted for this line (regardless of outcome).</summary>
    public bool MasterCardSearched { get; init; }
}

/// <summary>A planned, not-yet-applied change to Pohoda, derived from one parsed invoice.</summary>
public sealed class ReceiptProposal
{
    public required string Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string SourceFilePath { get; init; }
    public required string SourceFileSha256 { get; init; }
    public required ReceiptProposalType Type { get; init; }

    /// <summary>Set only for <see cref="ReceiptProposalType.AppendLines"/> — the Pohoda document id/number found.</summary>
    public string? ExistingReceiptId { get; init; }
    public string? ExistingReceiptNumber { get; init; }

    /// <summary>Header text for a new příjemka, e.g. "Gabel FT 2/260220". Null for AppendLines.</summary>
    public string? HeaderText { get; init; }

    public required IReadOnlyList<ReceiptLineProposal> LinesToAdd { get; init; }

    /// <summary>Freight amount to add as an accessory charge. Null if absent.</summary>
    public decimal? FreightToAdd { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public ProposalStatus Status { get; set; } = ProposalStatus.PendingApproval;
    public string? FailureReason { get; set; }
    public string? ResultReceiptNumber { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? AppliedAt { get; set; }
}
