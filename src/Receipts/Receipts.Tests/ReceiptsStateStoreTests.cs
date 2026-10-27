using Microsoft.Data.Sqlite;
using Receipts.Contract;
using Receipts.Core.Persistence;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class ReceiptsStateStoreTests
{
    /// <summary>Opens a fresh temp-file database; disposing removes the file (same convention as Common.Data.Sqlite.Tests).</summary>
    private sealed class TempStore : IAsyncDisposable
    {
        private readonly string _dbPath;
        public ReceiptsStateStore Store { get; }

        private TempStore(string dbPath, ReceiptsStateStore store)
        {
            _dbPath = dbPath;
            Store = store;
        }

        public static async Task<TempStore> OpenAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"receipts-state-test-{Guid.NewGuid():N}.db");
            return new TempStore(dbPath, await ReceiptsStateStore.OpenAsync(dbPath));
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

    [Fact]
    public async Task StockMappings_return_null_for_unknown_codes_and_upsert_known_ones()
    {
        await using var temp = await TempStore.OpenAsync();
        var mappings = temp.Store.StockMappings;

        Assert.Null(await mappings.GetAsync("7009190321200"));

        await mappings.SaveAsync("7009190321200", "7009190321200");
        await mappings.SaveAsync("7009190321200", "GN-7009190321200");

        Assert.Equal("GN-7009190321200", await mappings.GetAsync("7009190321200"));
    }

    [Fact]
    public async Task Proposals_round_trip_every_field_including_lines_and_warnings()
    {
        await using var temp = await TempStore.OpenAsync();

        var proposal = Make.Proposal(
            [
                Make.LineProposal("7009190321200", "DH PLATINUM SERIES CM.120", isNewStockItem: false, quantity: 2m, unitPrice: 94.5m),
                Make.LineProposal("7009181471300", "SLD KIT LIGHT BLUE 130CM", isNewStockItem: true,
                    variantSizeHint: "130 cm", variantBaseName: "SLD KIT LIGHT BLUE", masterCardCode: "7009181470000", masterCardSearched: true)
            ],
            type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42",
            existingReceiptNumber: "26PR00012",
            freightToAdd: 160.5m,
            warnings: ["prvé upozornenie", "druhé upozornenie"]);

        await temp.Store.Proposals.SaveAsync(proposal);
        var loaded = await temp.Store.Proposals.GetAsync("proposal-1");

        Assert.NotNull(loaded);
        Assert.Equal(proposal.Id, loaded.Id);
        Assert.Equal(proposal.InvoiceNumber, loaded.InvoiceNumber);
        Assert.Equal(proposal.InvoiceDate, loaded.InvoiceDate);
        Assert.Equal(proposal.SourceFilePath, loaded.SourceFilePath);
        Assert.Equal(proposal.SourceFileSha256, loaded.SourceFileSha256);
        Assert.Equal(ReceiptProposalType.AppendLines, loaded.Type);
        Assert.Equal("42", loaded.ExistingReceiptId);
        Assert.Equal("26PR00012", loaded.ExistingReceiptNumber);
        Assert.Null(loaded.HeaderText);
        Assert.Equal(160.5m, loaded.FreightToAdd);
        Assert.Equal(["prvé upozornenie", "druhé upozornenie"], loaded.Warnings);
        Assert.Equal(ProposalStatus.PendingApproval, loaded.Status);
        Assert.Null(loaded.FailureReason);
        Assert.Null(loaded.ResultReceiptNumber);
        Assert.Equal(proposal.CreatedAt, loaded.CreatedAt);
        Assert.Null(loaded.AppliedAt);

        Assert.Equal(2, loaded.LinesToAdd.Count);
        var variant = loaded.LinesToAdd[1];
        Assert.Equal("7009181471300", variant.SupplierCode);
        Assert.Equal("SLD KIT LIGHT BLUE 130CM", variant.Description);
        Assert.True(variant.IsNewStockItem);
        Assert.Equal("130 cm", variant.VariantSizeHint);
        Assert.Equal("SLD KIT LIGHT BLUE", variant.VariantBaseName);
        Assert.Equal("7009181470000", variant.MasterCardCode);
        Assert.True(variant.MasterCardSearched);
        Assert.Equal(94.5m, loaded.LinesToAdd[0].UnitPrice);
        Assert.Equal(2m, loaded.LinesToAdd[0].Quantity);
    }

    [Fact]
    public async Task Proposals_GetAsync_returns_null_for_an_unknown_id()
    {
        await using var temp = await TempStore.OpenAsync();

        Assert.Null(await temp.Store.Proposals.GetAsync("missing"));
    }

    [Fact]
    public async Task Proposals_SaveAsync_updates_status_result_and_applied_time_of_an_existing_row()
    {
        await using var temp = await TempStore.OpenAsync();
        var proposal = Make.Proposal([Make.LineProposal("A", "Line A", isNewStockItem: false)]);
        await temp.Store.Proposals.SaveAsync(proposal);

        proposal.Status = ProposalStatus.Applied;
        proposal.ResultReceiptNumber = "26PR00013";
        proposal.AppliedAt = new DateTimeOffset(2026, 9, 11, 9, 30, 0, TimeSpan.Zero);
        await temp.Store.Proposals.SaveAsync(proposal);

        var loaded = await temp.Store.Proposals.GetAsync(proposal.Id);
        Assert.NotNull(loaded);
        Assert.Equal(ProposalStatus.Applied, loaded.Status);
        Assert.Equal("26PR00013", loaded.ResultReceiptNumber);
        Assert.Equal(proposal.AppliedAt, loaded.AppliedAt);
    }

    [Fact]
    public async Task Proposals_GetByStatusAsync_filters_by_status_in_creation_order()
    {
        await using var temp = await TempStore.OpenAsync();
        var pendingOld = Make.Proposal([Make.LineProposal("A", "A", false)], id: "p-old");
        var pendingNew = Make.Proposal([Make.LineProposal("B", "B", false)], id: "p-new");
        var failed = Make.Proposal([Make.LineProposal("C", "C", false)], id: "p-failed");
        failed.Status = ProposalStatus.Failed;
        failed.FailureReason = "mServer vrátil chybový stav.";

        // Save the newer one first to prove ordering is by created_at, not insertion.
        await temp.Store.Proposals.SaveAsync(new ReceiptProposal
        {
            Id = pendingNew.Id, InvoiceNumber = pendingNew.InvoiceNumber, InvoiceDate = pendingNew.InvoiceDate,
            SourceFilePath = pendingNew.SourceFilePath, SourceFileSha256 = pendingNew.SourceFileSha256, Type = pendingNew.Type,
            HeaderText = pendingNew.HeaderText, LinesToAdd = pendingNew.LinesToAdd, FreightToAdd = pendingNew.FreightToAdd,
            Warnings = pendingNew.Warnings, CreatedAt = pendingNew.CreatedAt.AddMinutes(5)
        });
        await temp.Store.Proposals.SaveAsync(pendingOld);
        await temp.Store.Proposals.SaveAsync(failed);

        var pending = await temp.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval);
        var failedList = await temp.Store.Proposals.GetByStatusAsync(ProposalStatus.Failed);

        Assert.Equal(["p-old", "p-new"], pending.Select(p => p.Id));
        var onlyFailed = Assert.Single(failedList);
        Assert.Equal("mServer vrátil chybový stav.", onlyFailed.FailureReason);
    }

    [Fact]
    public async Task ResetProposalAsync_returns_the_proposal_to_PendingApproval_and_forgets_its_source_file()
    {
        await using var temp = await TempStore.OpenAsync();
        var proposal = Make.Proposal([Make.LineProposal("A", "A", false)]);
        proposal.Status = ProposalStatus.Failed;
        proposal.FailureReason = "boom";
        proposal.ResultReceiptNumber = "26PR00099";
        proposal.AppliedAt = DateTimeOffset.UtcNow;
        await temp.Store.Proposals.SaveAsync(proposal);
        await temp.Store.ProcessedFiles.MarkProcessedAsync(proposal.SourceFileSha256, proposal.SourceFilePath, proposal.Id);

        await temp.Store.ResetProposalAsync(proposal.Id);

        var loaded = await temp.Store.Proposals.GetAsync(proposal.Id);
        Assert.NotNull(loaded);
        Assert.Equal(ProposalStatus.PendingApproval, loaded.Status);
        Assert.Null(loaded.FailureReason);
        Assert.Null(loaded.ResultReceiptNumber);
        Assert.Null(loaded.AppliedAt);
        Assert.False(await temp.Store.ProcessedFiles.IsProcessedAsync(proposal.SourceFileSha256));
    }

    [Fact]
    public async Task Audit_and_ProcessedFiles_share_the_same_database()
    {
        await using var temp = await TempStore.OpenAsync();

        await temp.Store.Audit.AppendAsync(@"C:\faktury\a.pdf", "Vytvorená nová príjemka.");
        await temp.Store.ProcessedFiles.MarkProcessedAsync("sha-a", @"C:\faktury\a.pdf", "proposal-a");

        var audit = await temp.Store.Audit.ReadAllAsync();
        Assert.Single(audit);
        Assert.Equal("Vytvorená nová príjemka.", audit[0].Message);
        Assert.True(await temp.Store.ProcessedFiles.IsProcessedAsync("sha-a"));
    }
}
