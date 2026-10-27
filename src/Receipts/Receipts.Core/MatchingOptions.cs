namespace Receipts.Core;

/// <summary>How far back to list Pohoda příjemky when matching an invoice to an existing receipt (config section "Matching").</summary>
public sealed class MatchingOptions
{
    public const string SectionName = "Matching";

    public int LookbackMonths { get; init; } = 12;

    /// <summary>Maximum number of příjemky requested from mServer in one list call.</summary>
    public int ReceiptListLimit { get; init; } = 500;
}
