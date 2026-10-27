using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Models;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class ProposalApplierTests
{
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _dbPath;
        public ReceiptsStateStore Store { get; }
        public FakeStockCardService StockCards { get; } = new();
        public FakeReceiptService Receipts { get; } = new();
        public ProposalApplier Applier { get; }

        private Harness(string dbPath, ReceiptsStateStore store)
        {
            _dbPath = dbPath;
            Store = store;
            Applier = new ProposalApplier(StockCards, Receipts, store, Options.Create(new MatchingOptions()), NullLogger<ProposalApplier>.Instance);
        }

        public static async Task<Harness> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"proposal-applier-test-{Guid.NewGuid():N}.db");
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

    private static ReceiptLineProposal ExistingCardLine() =>
        Make.LineProposal("7009190321200", "DH PLATINUM SERIES CM.120", isNewStockItem: false, quantity: 1m, unitPrice: 94.5m, variantSizeHint: "120 cm");

    private static ReceiptLineProposal NewCardLine() =>
        Make.LineProposal("7009181471300", "SLD KIT LIGHT BLUE 130CM", isNewStockItem: true, quantity: 2m, unitPrice: 40m,
            variantSizeHint: "130 cm", variantBaseName: "SLD KIT LIGHT BLUE", masterCardCode: "7009181470000", masterCardSearched: true);

    [Fact]
    public async Task ApplyAsync_creates_missing_cards_then_a_new_receipt_with_every_line_and_freight_and_marks_the_proposal_applied()
    {
        await using var h = await Harness.CreateAsync();
        var proposal = Make.Proposal([ExistingCardLine(), NewCardLine()], freightToAdd: 160m);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        // The new card was created with the variant size as orderName; the existing one was left alone.
        var created = Assert.Single(h.StockCards.CreateCalls);
        Assert.Equal(("7009181471300", "SLD KIT LIGHT BLUE 130CM", "130 cm"), created);
        Assert.Equal("7009181471300", await h.Store.StockMappings.GetAsync("7009181471300"));

        var call = Assert.Single(h.Receipts.CreateCalls);
        Assert.Equal("Gabel FT 2/260220", call.HeaderText);
        Assert.Equal(new DateOnly(2026, 6, 23), call.Date);
        Assert.Equal(160m, call.Freight);
        Assert.Equal(
            [
                new ReceiptLineToWrite("7009190321200", "DH PLATINUM SERIES CM.120", 1m, "PA", 94.5m, 35m),
                new ReceiptLineToWrite("7009181471300", "SLD KIT LIGHT BLUE 130CM", 2m, "PA", 40m, 35m)
            ],
            call.Lines);
        Assert.Empty(h.Receipts.UpdateCalls);

        Assert.Equal(ProposalStatus.Applied, proposal.Status);
        Assert.Equal("26PR00013", proposal.ResultReceiptNumber);
        Assert.NotNull(proposal.AppliedAt);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Equal("26PR00013", stored.ResultReceiptNumber);

        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.All(audit, entry => Assert.Equal(proposal.SourceFilePath, entry.Source));
        Assert.Contains(audit, e => e.Message.StartsWith("Vytvorená nová príjemka 'Gabel FT 2/260220' (č. 26PR00013)."));
        Assert.Contains(audit, e => e.Message.Contains("Založená nová skladová karta 7009181471300") && e.Message.Contains("možná materská karta 7009181470000"));
        Assert.Contains(audit, e => e.Message.Contains("Pridaná doprava (vedľajšie náklady): 160"));
        Assert.Equal(2, audit.Count(e => e.Message.StartsWith("Riadok: ")));
    }

    [Fact]
    public async Task ApplyAsync_does_not_create_a_card_that_appeared_in_Pohoda_since_the_scan()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009181471300");
        var proposal = Make.Proposal([NewCardLine()], freightToAdd: null);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        Assert.Empty(h.StockCards.CreateCalls);
        Assert.Equal(["7009181471300"], h.StockCards.FindCalls);
        Assert.Equal("7009181471300", await h.Store.StockMappings.GetAsync("7009181471300"));
        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message.Contains("medzičasom vznikla"));
        Assert.Null(Assert.Single(h.Receipts.CreateCalls).Freight);
    }

    [Fact]
    public async Task ApplyAsync_appends_by_resending_the_existing_lines_followed_by_the_new_ones()
    {
        await using var h = await Harness.CreateAsync();
        var existingLines = new[]
        {
            new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m),
            new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 1m, 94.5m, 35m)
        };
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("42", "26PR00012", "Gabel FT 2/260220", existingLines));
        var proposal = Make.Proposal([NewCardLine()], type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42", existingReceiptNumber: "26PR00012", freightToAdd: 180m);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        Assert.Empty(h.Receipts.CreateCalls);
        var update = Assert.Single(h.Receipts.UpdateCalls);
        Assert.Equal("42", update.ExistingId);
        Assert.Equal("Gabel FT 2/260220", update.HeaderText);
        Assert.Equal(180m, update.Freight);
        Assert.Equal(
            [
                new ReceiptLineToWrite("7008352650000", "STRETCH LITE CARBON LILAC", 20m, "PA", 35m, 35m),
                new ReceiptLineToWrite("7009190321200", "DH PLATINUM SERIES CM.120", 1m, "PA", 94.5m, 35m),
                new ReceiptLineToWrite("7009181471300", "SLD KIT LIGHT BLUE 130CM", 2m, "PA", 40m, 35m)
            ],
            update.AllLines);

        Assert.Equal(ProposalStatus.Applied, proposal.Status);
        Assert.Equal("26PR00012", proposal.ResultReceiptNumber);
        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message.StartsWith("Doplnená existujúca príjemka 'Gabel FT 2/260220' (č. 26PR00012) o 1 riadkov."));
        Assert.DoesNotContain(audit, e => e.Message.StartsWith("POZOR:"));
    }

    [Fact]
    public async Task ApplyAsync_still_updates_and_flags_an_audit_warning_when_an_existing_line_has_unparsed_numbers()
    {
        await using var h = await Harness.CreateAsync();
        var existingLines = new[]
        {
            new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m),
            new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 0m, 94.5m, 35m, HasUnparsedNumbers: true)
        };
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("42", "26PR00012", "Gabel FT 2/260220", existingLines));
        var proposal = Make.Proposal([NewCardLine()], type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42", existingReceiptNumber: "26PR00012", freightToAdd: 180m);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        var update = Assert.Single(h.Receipts.UpdateCalls);
        Assert.Equal("42", update.ExistingId);

        Assert.Equal(ProposalStatus.Applied, proposal.Status);
        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message == "POZOR: 1 existujúcich riadkov príjemky sa nepodarilo úplne načítať (množstvo/cena/zľava) — pri zápise sa odošlú s nulami. Over príjemku ručne v Pohode.");
    }

    [Fact]
    public async Task ApplyAsync_throws_and_leaves_the_proposal_pending_when_the_existing_receipt_is_gone()
    {
        await using var h = await Harness.CreateAsync();
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("99", "26PR00099", "iná príjemka", []));
        var proposal = Make.Proposal([ExistingCardLine()], type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42", existingReceiptNumber: "26PR00012");
        await h.Store.Proposals.SaveAsync(proposal);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Applier.ApplyAsync(proposal));

        Assert.Contains("Príjemka 42 sa už v Pohode nenašla", ex.Message);
        Assert.Empty(h.Receipts.UpdateCalls);
        Assert.Equal(ProposalStatus.PendingApproval, proposal.Status);
        Assert.Empty(await h.Store.Audit.ReadAllAsync());
    }
}
