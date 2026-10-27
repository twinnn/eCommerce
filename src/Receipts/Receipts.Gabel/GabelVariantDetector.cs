using System.Text.RegularExpressions;
using Receipts.Contract;

namespace Receipts.Gabel;

/// <summary>
/// Gabel's size-variant naming convention, used both by <see cref="GabelInvoiceParser"/> (to tag
/// invoice lines, via the static <see cref="Detect"/>) and by <c>Receipts.Core</c> through
/// <see cref="IVariantDetector"/> (to exclude sibling variants — which also contain the base name as
/// a substring — from a master-card search).
/// </summary>
public sealed partial class GabelVariantDetector : IVariantDetector
{
    // Detects a fixed-size variant: "CM" (any case) plus a single number, in either order, with
    // optional whitespace/dot between them — "CM.130", "135CM", "Cm 115".
    [GeneratedRegex(@"(?<a>\d{2,3})\s*CM\b|CM\.?\s*(?<b>\d{2,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex VariantSizeRegex();

    // A number RANGE before "CM" (e.g. "73-110CM", "77/130CM") means an adjustable-length pole, not
    // a fixed-size variant — must be excluded even though its trailing number alone would otherwise
    // match VariantSizeRegex (e.g. the "110" in "73-110CM").
    [GeneratedRegex(@"\d+\s*[-/]\s*\d+\s*CM\b", RegexOptions.IgnoreCase)]
    private static partial Regex VariantRangeRegex();

    /// <summary>True if <paramref name="text"/> contains a fixed-size variant pattern (not a range).</summary>
    public bool LooksLikeSizeVariant(string text) => Detect(text).SizeHint is not null;

    /// <summary>
    /// Detects a fixed-size variant in <paramref name="text"/> and returns both the size hint (e.g.
    /// "120 cm") and the text with the size token removed (e.g. "DH PLATINUM SERIES" from
    /// "DH PLATINUM SERIES CM.120"). Returns (null, null) if no fixed-size pattern is found.
    /// </summary>
    public static (string? SizeHint, string? BaseName) Detect(string text)
    {
        if (VariantRangeRegex().IsMatch(text))
        {
            return (null, null);
        }

        var match = VariantSizeRegex().Match(text);
        if (!match.Success)
        {
            return (null, null);
        }

        var size = match.Groups["a"].Success ? match.Groups["a"].Value : match.Groups["b"].Value;
        var baseName = text.Remove(match.Index, match.Length).Trim();
        return ($"{size} cm", baseName);
    }
}
