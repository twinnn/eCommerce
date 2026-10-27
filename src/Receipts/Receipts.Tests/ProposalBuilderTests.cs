using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.Contract.Models;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class ProposalBuilderTests
{
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _dbPath;
        public ReceiptsStateStore Store { get; }
        public FakeStockCardService StockCards { get; } = new();
        public ProposalBuilder Builder { get; }

        private Harness(string dbPath, ReceiptsStateStore store)
        {
            _dbPath = dbPath;
            Store = store;
            Builder = new ProposalBuilder(StockCards, new FakeVariantDetector(), store.StockMappings, NullLogger<ProposalBuilder>.Instance);
        }

        public static async Task<Harness> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"proposal-builder-test-{Guid.NewGuid():N}.db");
            return new Harness(dbPath, await ReceiptsStateStore.OpenAsync(dbPath));
        }

        public async ValueTask DisposeAsync()
        {
            await Store.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }

    private static readonly InvoiceLine Platinum = Make.Line("7009190321200", "DH PLATINUM SERIES CM.120", quantity: 1m, unitPrice: 94.5m, variantSizeHint: "120 cm", variantBaseName: "DH PLATINUM SERIES");
    private static readonly InvoiceLine Lilac = Make.Line("7008352650000", "STRETCH LITE CARBON LILAC", quantity: 20m, unitPrice: 35m);

    private static ExistingReceipt ReceiptFor(string text, params ExistingReceiptLine[] lines) =>
        new("42", "26PR00012", text, lines);

    [Fact]
    public async Task BuildAsync_proposes_a_new_receipt_when_no_recent_receipt_mentions_the_invoice_number()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var invoice = Make.Invoice([Platinum, Lilac], freight: 180m, warnings: "parser warning");
        var unrelated = ReceiptFor("Gabel FT 2/250334");

        var proposal = await h.Builder.BuildAsync(invoice, [unrelated]);

        Assert.Equal(ReceiptProposalType.CreateReceipt, proposal.Type);
        Assert.Equal("Gabel FT 2/260220", proposal.HeaderText);
        Assert.Null(proposal.ExistingReceiptId);
        Assert.Equal("FT 2/260220", proposal.InvoiceNumber);
        Assert.Equal(invoice.SourceFileSha256, proposal.SourceFileSha256);
        Assert.Equal(180m, proposal.FreightToAdd);
        Assert.Equal(ProposalStatus.PendingApproval, proposal.Status);
        Assert.False(string.IsNullOrWhiteSpace(proposal.Id));

        Assert.Equal(2, proposal.LinesToAdd.Count);
        var platinum = proposal.LinesToAdd[0];
        Assert.Equal("7009190321200", platinum.PohodaStockCode);
        Assert.False(platinum.IsNewStockItem);
        Assert.Equal("120 cm", platinum.VariantSizeHint);
        Assert.False(platinum.MasterCardSearched); // existing card — no master-card lookup

        var lilac = proposal.LinesToAdd[1];
        Assert.Equal("7008352650000", lilac.PohodaStockCode); // new cards use the supplier code verbatim
        Assert.True(lilac.IsNewStockItem);
        Assert.Equal(20m, lilac.Quantity);
        Assert.Equal(35m, lilac.UnitPrice);

        Assert.Equal(["parser warning"], proposal.Warnings); // invoice warnings carried over, nothing else
        Assert.Empty(h.StockCards.CreateCalls); // scan never writes
        Assert.Equal("7009190321200", await h.Store.StockMappings.GetAsync("7009190321200")); // found card cached
        Assert.Null(await h.Store.StockMappings.GetAsync("7008352650000")); // missing card NOT cached
    }

    [Fact]
    public async Task BuildAsync_appends_only_the_lines_missing_from_a_receipt_that_already_references_the_invoice()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.StockCards.ExistingCodes.Add("7008352650000");
        var invoice = Make.Invoice([Platinum, Lilac], freight: 180m);
        var existing = ReceiptFor("Príjemka gabel ft 2/260220 (1. časť)",
            new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 1m, 94.5m, 35m));

        var proposal = await h.Builder.BuildAsync(invoice, [existing]);

        Assert.Equal(ReceiptProposalType.AppendLines, proposal.Type);
        Assert.Equal("42", proposal.ExistingReceiptId);
        Assert.Equal("26PR00012", proposal.ExistingReceiptNumber);
        Assert.Null(proposal.HeaderText);
        var onlyLine = Assert.Single(proposal.LinesToAdd);
        Assert.Equal("7008352650000", onlyLine.SupplierCode);
        Assert.Equal(180m, proposal.FreightToAdd);
        Assert.Contains(proposal.Warnings, w => w.Contains("Príjemka 26PR00012 už existuje") && w.Contains("dopravu"));
    }

    [Fact]
    public async Task BuildAsync_uses_the_cached_stock_mapping_before_asking_Pohoda()
    {
        await using var h = await Harness.CreateAsync();
        await h.Store.StockMappings.SaveAsync("7008352650000", "7008352650000");
        var invoice = Make.Invoice([Lilac], freight: 0m);

        var proposal = await h.Builder.BuildAsync(invoice, []);

        var line = Assert.Single(proposal.LinesToAdd);
        Assert.False(line.IsNewStockItem);
        Assert.Empty(h.StockCards.FindCalls);
        Assert.Null(proposal.FreightToAdd);
    }

    [Fact]
    public async Task BuildAsync_suggests_a_master_card_when_exactly_one_non_variant_candidate_matches()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.NameSearchResults.Add(new StockNameMatch("7009190321250", "Gabel DH PLATINUM SERIES 125 cm"));
        h.StockCards.NameSearchResults.Add(new StockNameMatch("7009190320000", "Gabel DH PLATINUM SERIES"));
        var invoice = Make.Invoice([Platinum], freight: 0m);

        var proposal = await h.Builder.BuildAsync(invoice, []);

        var line = Assert.Single(proposal.LinesToAdd);
        Assert.True(line.IsNewStockItem);
        Assert.True(line.MasterCardSearched);
        Assert.Equal("7009190320000", line.MasterCardCode);
        Assert.Equal(["DH PLATINUM SERIES"], h.StockCards.SearchCalls);
        Assert.Contains(proposal.Warnings, w => w.Contains("Možná materská karta nájdená: 7009190320000"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task BuildAsync_reports_no_master_card_when_candidates_are_absent_or_ambiguous(int nonVariantCandidates)
    {
        await using var h = await Harness.CreateAsync();
        for (var i = 0; i < nonVariantCandidates; i++)
        {
            h.StockCards.NameSearchResults.Add(new StockNameMatch($"70091903200{i}", $"Gabel DH PLATINUM SERIES verzia {i}"));
        }
        h.StockCards.NameSearchResults.Add(new StockNameMatch("7009190321250", "Gabel DH PLATINUM SERIES 125 cm"));
        var invoice = Make.Invoice([Platinum], freight: 0m);

        var proposal = await h.Builder.BuildAsync(invoice, []);

        var line = Assert.Single(proposal.LinesToAdd);
        Assert.True(line.MasterCardSearched);
        Assert.Null(line.MasterCardCode);
        Assert.Contains(proposal.Warnings, w => w.Contains("sa v Pohode nenašla"));
    }

    [Fact]
    public async Task BuildAsync_warns_when_the_existing_receipt_already_contains_every_line_and_there_is_no_freight()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7008352650000");
        var invoice = Make.Invoice([Lilac], freight: 0m);
        var existing = ReceiptFor("Gabel FT 2/260220",
            new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m));

        var proposal = await h.Builder.BuildAsync(invoice, [existing]);

        Assert.Equal(ReceiptProposalType.AppendLines, proposal.Type);
        Assert.Empty(proposal.LinesToAdd);
        Assert.Null(proposal.FreightToAdd);
        Assert.Contains("Všetky položky z faktúry sa v existujúcej príjemke už nachádzajú — proposal nepridáva nič nové.", proposal.Warnings);
    }
}
