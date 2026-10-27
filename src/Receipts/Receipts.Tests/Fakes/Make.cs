using Receipts.Contract;

namespace Receipts.Tests.Fakes;

/// <summary>Small builders so tests state only what matters. Defaults mirror real Gabel invoice FT 2/260220.</summary>
internal static class Make
{
    public static InvoiceLine Line(
        string supplierCode,
        string description,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m,
        string? variantSizeHint = null,
        string? variantBaseName = null) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            LineAmount = Math.Round(quantity * unitPrice * (1 - discountPercent / 100m), 2),
            VariantSizeHint = variantSizeHint,
            VariantBaseName = variantBaseName
        };

    public static ParsedInvoice Invoice(
        IReadOnlyList<InvoiceLine> lines,
        decimal freight = 180m,
        string invoiceNumber = "FT 2/260220",
        string supplierName = "Gabel",
        params string[] warnings)
    {
        var goodsNet = lines.Sum(l => l.LineAmount);
        var invoice = new ParsedInvoice
        {
            SupplierName = supplierName,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            Currency = "EUR",
            Lines = lines,
            FreightAmount = freight,
            PrintedGoodsNetTotal = goodsNet,
            PrintedGrandTotal = goodsNet + freight,
            SourceFilePath = @"C:\faktury\ft2-260220del20260623.pdf",
            SourceFileSha256 = "SHA-260220"
        };
        invoice.Warnings.AddRange(warnings);
        return invoice;
    }

    public static ReceiptLineProposal LineProposal(
        string supplierCode,
        string description,
        bool isNewStockItem,
        string? pohodaStockCode = null,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m,
        string? variantSizeHint = null,
        string? variantBaseName = null,
        string? masterCardCode = null,
        bool masterCardSearched = false) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            PohodaStockCode = pohodaStockCode ?? supplierCode,
            IsNewStockItem = isNewStockItem,
            VariantSizeHint = variantSizeHint,
            VariantBaseName = variantBaseName,
            MasterCardCode = masterCardCode,
            MasterCardSearched = masterCardSearched
        };

    public static ReceiptProposal Proposal(
        IReadOnlyList<ReceiptLineProposal> lines,
        ReceiptProposalType type = ReceiptProposalType.CreateReceipt,
        string? existingReceiptId = null,
        string? existingReceiptNumber = null,
        decimal? freightToAdd = 180m,
        string id = "proposal-1",
        string invoiceNumber = "FT 2/260220",
        params string[] warnings) =>
        new()
        {
            Id = id,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            SourceFilePath = @"C:\faktury\ft2-260220del20260623.pdf",
            SourceFileSha256 = "SHA-260220",
            Type = type,
            ExistingReceiptId = existingReceiptId,
            ExistingReceiptNumber = existingReceiptNumber,
            HeaderText = type == ReceiptProposalType.CreateReceipt ? $"Gabel {invoiceNumber}" : null,
            LinesToAdd = lines,
            FreightToAdd = freightToAdd,
            Warnings = warnings,
            CreatedAt = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero)
        };
}
