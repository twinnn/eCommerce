using Jobs.Receipts.Apply;
using Jobs.Receipts.Scan;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>
/// A throwaway pipeline: temp inbox / archive / pending folders, a temp-file SQLite store, fake
/// Pohoda agendas and a real <see cref="ProposalBuilder"/>. Disposing closes the store and deletes
/// the whole temp tree.
/// </summary>
internal sealed class PipelineHarness : IAsyncDisposable
{
    public string Root { get; }
    public WatchOptions Watch { get; }
    public MatchingOptions Matching { get; } = new();
    public ReceiptsStateStore Store { get; }

    public FakeReceiptService Receipts { get; } = new();
    public FakeStockCardService StockCards { get; } = new();
    public FakeInvoiceParser Parser { get; } = new();
    public ProposalBuilder Builder { get; }

    private PipelineHarness(string root, WatchOptions watch, ReceiptsStateStore store)
    {
        Root = root;
        Watch = watch;
        Store = store;
        Builder = new ProposalBuilder(StockCards, new FakeVariantDetector(), store.StockMappings, NullLogger<ProposalBuilder>.Instance);
    }

    public static async Task<PipelineHarness> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"receipts-jobs-test-{Guid.NewGuid():N}");
        var watch = new WatchOptions
        {
            InboxFolder = Path.Combine(root, "inbox"),
            ArchiveFolder = Path.Combine(root, "archive"),
            PendingApprovalFolder = Path.Combine(root, "pending"),
            DatabasePath = Path.Combine(root, "receipts.db")
        };

        Directory.CreateDirectory(watch.InboxFolder);
        var store = await ReceiptsStateStore.OpenAsync(watch.DatabasePath);
        return new PipelineHarness(root, watch, store);
    }

    public ScanRunner CreateScanRunner() =>
        new(Receipts, Parser, Builder, Store, Watch, Matching, NullLogger<ScanRunner>.Instance);

    /// <summary>Drops a placeholder file into the inbox (the parser is faked, so the bytes never matter) and returns its full path.</summary>
    public string AddInboxFile(string fileName)
    {
        var path = Path.Combine(Watch.InboxFolder, fileName);
        File.WriteAllText(path, "placeholder — the invoice parser is faked in these tests");
        return path;
    }

    /// <summary>Drops a file into the inbox and registers a parsed invoice for it.</summary>
    public ParsedInvoice AddParsableInvoice(
        string fileName,
        string invoiceNumber,
        string sha256,
        params InvoiceLine[] lines)
    {
        var path = AddInboxFile(fileName);
        var goodsNet = lines.Sum(l => l.LineAmount);
        var invoice = new ParsedInvoice
        {
            SupplierName = "TestDodávateľ",
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            Currency = "EUR",
            Lines = lines,
            FreightAmount = 0m,
            PrintedGoodsNetTotal = goodsNet,
            PrintedGrandTotal = goodsNet,
            SourceFilePath = path,
            SourceFileSha256 = sha256
        };

        Parser.Invoices[fileName] = invoice;
        return invoice;
    }

    public ApplyRunner CreateApplyRunner() =>
        new(
            new ProposalApplier(StockCards, Receipts, Store, Options.Create(Matching), NullLogger<ProposalApplier>.Instance),
            Store,
            Watch,
            NullLogger<ApplyRunner>.Instance);

    /// <summary>Drops a placeholder PDF into the inbox and saves a proposal pointing at it.</summary>
    public async Task<ReceiptProposal> AddPendingProposalAsync(
        string fileName = "ft2-260220.pdf",
        string id = "proposal-1",
        IReadOnlyList<ReceiptLineProposal>? lines = null,
        ReceiptProposalType type = ReceiptProposalType.CreateReceipt,
        string? existingReceiptId = null,
        string? existingReceiptNumber = null)
    {
        var path = AddInboxFile(fileName);
        var proposal = Make.Proposal(path, id, lines: lines, type: type,
            existingReceiptId: existingReceiptId, existingReceiptNumber: existingReceiptNumber);
        await Store.Proposals.SaveAsync(proposal);
        return proposal;
    }

    public async ValueTask DisposeAsync()
    {
        await Store.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
