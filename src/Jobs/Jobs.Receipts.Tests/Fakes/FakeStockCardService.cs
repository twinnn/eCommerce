using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>In-memory Pohoda stock agenda: a set of existing card codes plus a canned name-search result.</summary>
internal sealed class FakeStockCardService : IStockCardService
{
    public HashSet<string> ExistingCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StockNameMatch> NameSearchResults { get; } = [];

    public List<string> FindCalls { get; } = [];
    public List<string> SearchCalls { get; } = [];
    public List<(string Code, string Name, string? OrderName)> CreateCalls { get; } = [];

    public Task<string?> FindBySupplierCodeAsync(string supplierCode, CancellationToken ct = default)
    {
        FindCalls.Add(supplierCode);
        return Task.FromResult(ExistingCodes.Contains(supplierCode) ? supplierCode : null);
    }

    public Task<IReadOnlyList<StockNameMatch>> SearchByNameContainingAsync(string nameContains, CancellationToken ct = default)
    {
        SearchCalls.Add(nameContains);
        IReadOnlyList<StockNameMatch> results = NameSearchResults
            .Where(m => m.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<string> CreateAsync(string code, string name, string? orderName, CancellationToken ct = default)
    {
        CreateCalls.Add((code, name, orderName));
        ExistingCodes.Add(code);
        return Task.FromResult(code);
    }
}
