using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>In-memory Pohoda receipt agenda that records every call the jobs make.</summary>
internal sealed class FakeReceiptService : IReceiptService
{
    public List<ExistingReceipt> RecentReceipts { get; } = [];
    public string NextCreatedNumber { get; set; } = "26PR00013";

    public List<(DateOnly Since, int Limit)> ListCalls { get; } = [];
    public List<(string HeaderText, DateOnly Date, IReadOnlyList<ReceiptLineToWrite> Lines, decimal? Freight)> CreateCalls { get; } = [];
    public List<(string ExistingId, string HeaderText, IReadOnlyList<ReceiptLineToWrite> AllLines, decimal? Freight)> UpdateCalls { get; } = [];

    /// <summary>When set, every write throws this message — used to drive the apply job's failure path.</summary>
    public string? ThrowOnWrite { get; set; }

    public Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default)
    {
        ListCalls.Add((since, limit));
        return Task.FromResult<IReadOnlyList<ExistingReceipt>>(RecentReceipts.ToList());
    }

    public Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)
    {
        CreateCalls.Add((headerText, date, lines, freight));
        if (ThrowOnWrite is not null)
        {
            throw new InvalidOperationException(ThrowOnWrite);
        }

        return Task.FromResult(NextCreatedNumber);
    }

    public Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)
    {
        UpdateCalls.Add((existingId, headerText, allLines, freight));
        if (ThrowOnWrite is not null)
        {
            throw new InvalidOperationException(ThrowOnWrite);
        }

        return Task.CompletedTask;
    }
}
