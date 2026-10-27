using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pohoda.Contract.Models;

namespace Pohoda.Contract.Services;

/// <summary>
/// Skladová karta operations the receipt-sync pipeline needs: look a card up by the supplier's own
/// product code (stored verbatim in <c>stk:code</c>), search by partial name, and create a new card.
/// Transitional: PR4 of the merge folds these members into <see cref="IStockService"/> when the
/// existing <c>Pohoda.Server.StockService</c> moves onto <c>Pohoda.MServer</c>.
/// </summary>
public interface IStockCardService
{
    /// <summary>Returns the Pohoda stock code of the card whose <c>stk:code</c> equals <paramref name="supplierCode"/>, or null if none exists.</summary>
    Task<string?> FindBySupplierCodeAsync(string supplierCode, CancellationToken ct = default);

    /// <summary>Returns every card whose <c>stk:name</c> contains <paramref name="nameContains"/> (callers filter sibling variants themselves).</summary>
    Task<IReadOnlyList<StockNameMatch>> SearchByNameContainingAsync(string nameContains, CancellationToken ct = default);

    /// <summary>Creates a new card with the given code/name; <paramref name="orderName"/> (e.g. "120 cm") is written to <c>stk:orderName</c> when supplied. Returns the code.</summary>
    Task<string> CreateAsync(string code, string name, string? orderName, CancellationToken ct = default);
}
