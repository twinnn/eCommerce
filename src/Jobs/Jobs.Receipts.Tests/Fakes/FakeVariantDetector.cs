using Receipts.Contract;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>Deliberately simplistic, supplier-neutral rule — these tests are about the job, not about variant naming.</summary>
internal sealed class FakeVariantDetector : IVariantDetector
{
    public bool LooksLikeSizeVariant(string text) =>
        text.EndsWith(" cm", StringComparison.OrdinalIgnoreCase) || text.Contains("CM", StringComparison.Ordinal);
}
