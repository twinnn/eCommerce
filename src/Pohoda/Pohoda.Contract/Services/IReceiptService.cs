using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pohoda.Contract.Models;

namespace Pohoda.Contract.Services;

/// <summary>Příjemka (goods receipt) agenda: list recent documents, create a new one, or update an existing one.</summary>
public interface IReceiptService
{
    /// <summary>
    /// Lists příjemky since <paramref name="since"/> (bounded by <paramref name="limit"/>) so the caller
    /// can locally search their <c>Text</c> for a substring (e.g. "FT 2/260220") — the Pohoda XML agenda
    /// has no server-side free-text filter for this document type.
    /// </summary>
    Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default);

    /// <summary>Creates a new příjemka and returns its Pohoda document number.</summary>
    Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing příjemka. Callers must pass the FULL desired line set (existing lines +
    /// newly discovered ones) — resending the complete list is the safe default until incremental
    /// append semantics are confirmed live.
    /// </summary>
    Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default);
}
