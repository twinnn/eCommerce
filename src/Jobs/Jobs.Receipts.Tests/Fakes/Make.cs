using Receipts.Contract;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>Small builders so job tests state only what matters.</summary>
internal static class Make
{
    public static InvoiceLine Line(
        string supplierCode,
        string description,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            LineAmount = Math.Round(quantity * unitPrice * (1 - discountPercent / 100m), 2)
        };

    public static ReceiptLineProposal LineProposal(
        string supplierCode,
        string description,
        bool isNewStockItem = false,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            PohodaStockCode = supplierCode,
            IsNewStockItem = isNewStockItem
        };

    public static ReceiptProposal Proposal(
        string sourceFilePath,
        string id = "proposal-1",
        string invoiceNumber = "FT 2/260220",
        IReadOnlyList<ReceiptLineProposal>? lines = null,
        ReceiptProposalType type = ReceiptProposalType.CreateReceipt,
        string? existingReceiptId = null,
        string? existingReceiptNumber = null,
        decimal? freightToAdd = null) =>
        new()
        {
            Id = id,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            SourceFilePath = sourceFilePath,
            SourceFileSha256 = $"SHA-{id}",
            Type = type,
            ExistingReceiptId = existingReceiptId,
            ExistingReceiptNumber = existingReceiptNumber,
            HeaderText = type == ReceiptProposalType.CreateReceipt ? $"TestDodávateľ {invoiceNumber}" : null,
            LinesToAdd = lines ?? [LineProposal("7009190321200", "DH PLATINUM SERIES")],
            FreightToAdd = freightToAdd,
            Warnings = [],
            CreatedAt = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero)
        };
}
