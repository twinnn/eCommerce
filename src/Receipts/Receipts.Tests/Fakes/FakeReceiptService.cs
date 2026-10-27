using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Receipts.Tests.Fakes;

internal sealed class FakeReceiptService : IReceiptService
{
    public List<ExistingReceipt> RecentReceipts { get; } = [];
    public string NextCreatedNumber { get; set; } = "26PR00013";

    public List<(string HeaderText, DateOnly Date, IReadOnlyList<ReceiptLineToWrite> Lines, decimal? Freight)> CreateCalls { get; } = [];
    public List<(string ExistingId, string HeaderText, IReadOnlyList<ReceiptLineToWrite> AllLines, decimal? Freight)> UpdateCalls { get; } = [];

    public Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExistingReceipt>>(RecentReceipts.ToList());

    public Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)
    {
        CreateCalls.Add((headerText, date, lines, freight));
        return Task.FromResult(NextCreatedNumber);
    }

    public Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)
    {
        UpdateCalls.Add((existingId, headerText, allLines, freight));
        return Task.CompletedTask;
    }
}
