using Jobs.Receipts.Scan;
using Jobs.Receipts.Tests.Fakes;
using Pohoda.Contract.Models;
using Receipts.Contract;

namespace Jobs.Receipts.Tests;

public class ScanRunnerTests
{
    [Fact]
    public async Task RunOnceAsync_writes_a_proposal_a_preview_file_and_marks_the_source_processed()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7009190321200", "DH PLATINUM SERIES", quantity: 2m, unitPrice: 94.5m));

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 1, Skipped: 0), result);

        var proposal = Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.Equal("FT 2/260220", proposal.InvoiceNumber);
        Assert.Equal(ReceiptProposalType.CreateReceipt, proposal.Type);
        Assert.True(await h.Store.ProcessedFiles.IsProcessedAsync("SHA-260220"));

        var previewPath = Path.Combine(h.Watch.PendingApprovalFolder, $"{proposal.Id}_ft2-260220.md");
        Assert.True(File.Exists(previewPath));
        var preview = await File.ReadAllTextAsync(previewPath);
        Assert.Contains("FT 2/260220", preview);
        Assert.Contains($"apply --id {proposal.Id}", preview);
    }

    [Fact]
    public async Task RunOnceAsync_never_writes_to_Pohoda()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7008352650000", "STRETCH LITE CARBON LILAC"));

        await h.CreateScanRunner().RunOnceAsync();

        Assert.Empty(h.StockCards.CreateCalls);
        Assert.Empty(h.Receipts.CreateCalls);
        Assert.Empty(h.Receipts.UpdateCalls);
    }

    [Fact]
    public async Task RunOnceAsync_skips_a_file_the_parser_rejects_without_creating_a_proposal()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddInboxFile("pr1-5605.pdf");

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 0, Skipped: 1), result);
        Assert.Empty(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.False(Directory.EnumerateFiles(h.Watch.PendingApprovalFolder).Any());
    }

    [Fact]
    public async Task RunOnceAsync_skips_a_file_whose_hash_was_already_processed()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7008352650000", "STRETCH LITE CARBON LILAC"));
        await h.Store.ProcessedFiles.MarkProcessedAsync("SHA-260220", "whatever.pdf", "an-earlier-proposal");

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 0, Skipped: 1), result);
        Assert.Empty(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
    }

    [Fact]
    public async Task RunOnceAsync_skips_a_file_with_an_open_pending_proposal_even_after_a_reset_forgets_it_was_processed()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7008352650000", "STRETCH LITE CARBON LILAC"));

        // Mimics the state `reset` leaves behind: the proposal still exists (PendingApproval) but the
        // processed-file row was forgotten, so only the open-proposal-hash guard can catch the file.
        // Make.Proposal derives SourceFileSha256 as "SHA-{id}", so id "260220" matches the invoice's
        // registered hash "SHA-260220" above.
        var proposal = Make.Proposal(Path.Combine(h.Watch.InboxFolder, "ft2-260220.pdf"), id: "260220");
        await h.Store.Proposals.SaveAsync(proposal);

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 0, Skipped: 1), result);
        Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
    }

    [Fact]
    public async Task RunOnceAsync_skips_a_file_with_an_open_failed_proposal_even_after_a_reset_forgets_it_was_processed()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7008352650000", "STRETCH LITE CARBON LILAC"));

        var proposal = Make.Proposal(Path.Combine(h.Watch.InboxFolder, "ft2-260220.pdf"), id: "260220");
        proposal.Status = ProposalStatus.Failed;
        proposal.FailureReason = "staršia chyba";
        await h.Store.Proposals.SaveAsync(proposal);

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 0, Skipped: 1), result);
        Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.Failed));
    }

    [Fact]
    public async Task RunOnceAsync_lists_recent_receipts_once_using_the_configured_window_and_limit()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("a.pdf", "FT 2/260001", "SHA-A", Make.Line("A", "A"));
        h.AddParsableInvoice("b.pdf", "FT 2/260002", "SHA-B", Make.Line("B", "B"));

        await h.CreateScanRunner().RunOnceAsync();

        var call = Assert.Single(h.Receipts.ListCalls);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddMonths(-h.Matching.LookbackMonths)), call.Since);
        Assert.Equal(h.Matching.ReceiptListLimit, call.Limit);
    }

    [Fact]
    public async Task RunOnceAsync_matches_an_existing_receipt_and_proposes_an_append()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("42", "26PR00012", "Gabel FT 2/260220",
            [new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m)]));
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7009190321200", "DH PLATINUM SERIES"));

        await h.CreateScanRunner().RunOnceAsync();

        var proposal = Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.Equal(ReceiptProposalType.AppendLines, proposal.Type);
        Assert.Equal("42", proposal.ExistingReceiptId);
    }

    [Fact]
    public async Task RunOnceAsync_creates_the_inbox_and_pending_folders_when_they_are_missing()
    {
        await using var h = await PipelineHarness.CreateAsync();
        Directory.Delete(h.Watch.InboxFolder, recursive: true);

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 0, ProposalsCreated: 0, Skipped: 0), result);
        Assert.True(Directory.Exists(h.Watch.InboxFolder));
        Assert.True(Directory.Exists(h.Watch.PendingApprovalFolder));
    }
}
