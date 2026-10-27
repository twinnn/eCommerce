using Receipts.Contract;

namespace Receipts.Tests.Fakes;

/// <summary>Deliberately simplistic, supplier-neutral rule so Core tests do not depend on Receipts.Gabel.</summary>
internal sealed class FakeVariantDetector : IVariantDetector
{
    public bool LooksLikeSizeVariant(string text) =>
        text.EndsWith(" cm", StringComparison.OrdinalIgnoreCase) || text.Contains("CM", StringComparison.Ordinal);
}
