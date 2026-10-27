using Receipts.Contract;
using Receipts.Gabel;

namespace Receipts.Tests;

public class GabelVariantDetectorTests
{
    [Theory]
    [InlineData("DH PLATINUM SERIES CM.120", "120 cm", "DH PLATINUM SERIES")]
    [InlineData("X-1.35 ACTIVE ORANGE 135CM", "135 cm", "X-1.35 ACTIVE ORANGE")]
    [InlineData("VIPER 3S-TR Cm 115", "115 cm", "VIPER 3S-TR")]
    [InlineData("SLD KIT LIGHT BLUE 130CM", "130 cm", "SLD KIT LIGHT BLUE")]
    public void Detect_returns_the_size_hint_and_the_name_without_the_size_token(string text, string expectedHint, string expectedBaseName)
    {
        var (sizeHint, baseName) = GabelVariantDetector.Detect(text);

        Assert.Equal(expectedHint, sizeHint);
        Assert.Equal(expectedBaseName, baseName);
    }

    [Theory]
    [InlineData("NORDIC ENERGY FL 73-110CM")]
    [InlineData("TREKKING 77/130CM")]
    [InlineData("STRETCH LITE CARBON LILAC")]
    [InlineData("Gabel SLK-R - pretekárska palica")]
    public void Detect_returns_nulls_for_adjustable_ranges_and_names_without_a_size(string text)
    {
        var (sizeHint, baseName) = GabelVariantDetector.Detect(text);

        Assert.Null(sizeHint);
        Assert.Null(baseName);
    }

    [Fact]
    public void LooksLikeSizeVariant_is_exposed_through_IVariantDetector()
    {
        IVariantDetector detector = new GabelVariantDetector();

        Assert.True(detector.LooksLikeSizeVariant("Gabel SLK-R - pretekárska palica 125 cm"));
        Assert.False(detector.LooksLikeSizeVariant("Gabel SLK-R - pretekárska palica"));
    }
}
