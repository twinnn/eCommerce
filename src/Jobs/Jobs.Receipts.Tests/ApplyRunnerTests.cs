using Jobs.Receipts.Tests.Fakes;
using Pohoda.Contract.Models;
using Receipts.Contract;

namespace Jobs.Receipts.Tests;

public class ApplyRunnerTests
{
    [Fact]
    public async Task ApplyAsync_by_id_writes_the_receipt_archives_the_pdf_and_marks_the_proposal_applied()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var proposal = await h.AddPendingProposalAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(0, exitCode);
        var call = Assert.Single(h.Receipts.CreateCalls);
        Assert.Equal("TestDodávateľ FT 2/260220", call.HeaderText);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Equal("26PR00013", stored.ResultReceiptNumber);

        Assert.False(File.Exists(proposal.SourceFilePath));
        Assert.True(File.Exists(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf")));
    }

    [Fact]
    public async Task ApplyAsync_by_id_returns_one_for_an_unknown_proposal_and_writes_nothing()
    {
        await using var h = await PipelineHarness.CreateAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync("missing", all: false);

        Assert.Equal(1, exitCode);
        Assert.Empty(h.Receipts.CreateCalls);
    }

    [Fact]
    public async Task ApplyAsync_by_id_refuses_a_proposal_that_was_already_applied()
    {
        await using var h = await PipelineHarness.CreateAsync();
        var proposal = await h.AddPendingProposalAsync();
        proposal.Status = ProposalStatus.Applied;
        proposal.ResultReceiptNumber = "26PR00010";
        await h.Store.Proposals.SaveAsync(proposal);

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(1, exitCode);
        Assert.Empty(h.Receipts.CreateCalls);
    }

    [Fact]
    public async Task ApplyAsync_by_id_retries_a_failed_proposal()
    {
        await using var h = await PipelineHarness.CreateAsync();
        var proposal = await h.AddPendingProposalAsync();
        proposal.Status = ProposalStatus.Failed;
        proposal.FailureReason = "predchádzajúci pokus zlyhal";
        await h.Store.Proposals.SaveAsync(proposal);

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(0, exitCode);
        Assert.Single(h.Receipts.CreateCalls);
        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Null(stored.FailureReason);
    }

    [Fact]
    public async Task ApplyAsync_all_applies_every_pending_proposal_and_skips_failed_ones()
    {
        await using var h = await PipelineHarness.CreateAsync();
        await h.AddPendingProposalAsync("a.pdf", id: "p-a");
        await h.AddPendingProposalAsync("b.pdf", id: "p-b");
        var failed = await h.AddPendingProposalAsync("c.pdf", id: "p-c");
        failed.Status = ProposalStatus.Failed;
        failed.FailureReason = "staršia chyba";
        await h.Store.Proposals.SaveAsync(failed);

        var exitCode = await h.CreateApplyRunner().ApplyAsync(id: null, all: true);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, h.Receipts.CreateCalls.Count);
        Assert.Equal(ProposalStatus.Failed, (await h.Store.Proposals.GetAsync("p-c"))!.Status);
    }

    [Fact]
    public async Task ApplyAsync_all_returns_zero_when_nothing_is_pending()
    {
        await using var h = await PipelineHarness.CreateAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync(id: null, all: true);

        Assert.Equal(0, exitCode);
        Assert.Empty(h.Receipts.CreateCalls);
    }

    [Fact]
    public async Task ApplyAsync_records_a_failed_write_and_leaves_the_pdf_in_the_inbox()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.Receipts.ThrowOnWrite = "mServer vrátil chybový stav.";
        var proposal = await h.AddPendingProposalAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(1, exitCode);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Failed, stored!.Status);
        Assert.Contains("mServer vrátil chybový stav.", stored.FailureReason);
        Assert.Null(stored.ResultReceiptNumber);

        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message.StartsWith("CHYBA pri zápise:") && e.Source == proposal.SourceFilePath);

        Assert.True(File.Exists(proposal.SourceFilePath));
        Assert.False(Directory.Exists(h.Watch.ArchiveFolder) && File.Exists(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf")));
    }

    [Fact]
    public async Task ApplyAsync_by_id_keeps_the_proposal_applied_when_the_post_write_archive_move_fails()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var proposal = await h.AddPendingProposalAsync();

        // File.Move onto an existing directory throws — a deterministic way to fail the archive step
        // after the Pohoda write has already succeeded.
        Directory.CreateDirectory(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf"));

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(0, exitCode);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Equal("26PR00013", stored.ResultReceiptNumber);
        Assert.Null(stored.FailureReason);

        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.DoesNotContain(audit, e => e.Message.StartsWith("CHYBA pri zápise:"));
    }

    [Fact]
    public async Task ApplyAsync_all_keeps_going_after_one_proposal_fails()
    {
        await using var h = await PipelineHarness.CreateAsync();
        await h.AddPendingProposalAsync("a.pdf", id: "p-a");
        await h.AddPendingProposalAsync("b.pdf", id: "p-b");
        h.Receipts.ThrowOnWrite = "mServer vrátil chybový stav.";

        var exitCode = await h.CreateApplyRunner().ApplyAsync(id: null, all: true);

        Assert.Equal(1, exitCode);
        Assert.Equal(2, h.Receipts.CreateCalls.Count);
        Assert.Equal(ProposalStatus.Failed, (await h.Store.Proposals.GetAsync("p-a"))!.Status);
        Assert.Equal(ProposalStatus.Failed, (await h.Store.Proposals.GetAsync("p-b"))!.Status);
    }

    [Fact]
    public async Task StatusAsync_returns_zero_and_reads_both_pending_and_failed_proposals()
    {
        await using var h = await PipelineHarness.CreateAsync();
        await h.AddPendingProposalAsync("a.pdf", id: "p-a");
        var failed = await h.AddPendingProposalAsync("b.pdf", id: "p-b");
        failed.Status = ProposalStatus.Failed;
        failed.FailureReason = "chyba";
        await h.Store.Proposals.SaveAsync(failed);

        Assert.Equal(0, await h.CreateApplyRunner().StatusAsync());

        Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.Failed));
    }

    [Fact]
    public async Task ResetAsync_returns_the_proposal_to_pending_and_moves_the_pdf_back_from_the_archive()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var proposal = await h.AddPendingProposalAsync();
        await h.Store.ProcessedFiles.MarkProcessedAsync(proposal.SourceFileSha256, proposal.SourceFilePath, proposal.Id);
        await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        var exitCode = await h.CreateApplyRunner().ResetAsync(proposal.Id);

        Assert.Equal(0, exitCode);
        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.PendingApproval, stored!.Status);
        Assert.Null(stored.ResultReceiptNumber);
        Assert.False(await h.Store.ProcessedFiles.IsProcessedAsync(proposal.SourceFileSha256));
        Assert.True(File.Exists(proposal.SourceFilePath));
        Assert.False(File.Exists(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf")));
    }

    [Fact]
    public async Task ResetAsync_returns_one_for_an_unknown_proposal()
    {
        await using var h = await PipelineHarness.CreateAsync();

        Assert.Equal(1, await h.CreateApplyRunner().ResetAsync("missing"));
    }
}
