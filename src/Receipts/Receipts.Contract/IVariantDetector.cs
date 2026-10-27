namespace Receipts.Contract;

/// <summary>
/// Supplier plugin: decides whether a product name (from the invoice or from a Pohoda stock card)
/// looks like a fixed-size variant of a product family. <c>Receipts.Core</c> uses it to exclude
/// sibling variants from a master-card search, whose naming convention is supplier-specific.
/// </summary>
public interface IVariantDetector
{
    bool LooksLikeSizeVariant(string text);
}
