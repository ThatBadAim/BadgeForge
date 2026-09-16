using BadgeForge.Core.Printing.Models;
using Xunit;

namespace BadgeForge.Tests;

public class CardFormatTests
{
    [Fact]
    public void CR80_Standard_HasExpectedPhysicalDimensionsAndPixelCount()
    {
        var cr80 = CardFormat.CR80;

        Assert.Equal("CR80 Standard (ID-1)", cr80.Name);
        Assert.Equal(85.60, cr80.WidthMm, 2);
        Assert.Equal(53.98, cr80.HeightMm, 2);
        Assert.Equal(300, cr80.Dpi);

        // (85.60 / 25.4) * 300 ≈ 1011.02 -> 1011
        // (53.98 / 25.4) * 300 ≈ 637.56 -> 638
        Assert.InRange(cr80.WidthPixels, 1010, 1014);
        Assert.InRange(cr80.HeightPixels, 636, 640);

        // Safe margin: 0.508mm ≈ 6 pixels at 300 DPI
        Assert.Equal(6, cr80.SafeMarginPixels);
        Assert.False(cr80.IsPortrait);
        Assert.True(cr80.AspectRatio > 1.5);
    }

    [Fact]
    public void CardFormat_CanBeCustomizedAtRuntimeWithoutCodeChanges()
    {
        // Demonstrates configurable data, not constants
        var custom = new CardFormat
        {
            Name = "Oversized Event Badge",
            WidthMm = 100.0,
            HeightMm = 70.0,
            Dpi = 600,
            SafeMarginMm = 1.0,
            IsPortrait = false
        };

        Assert.Equal("Oversized Event Badge", custom.Name);
        Assert.Equal((int)Math.Round((100.0 / 25.4) * 600), custom.WidthPixels);
        Assert.Equal((int)Math.Round((70.0 / 25.4) * 600), custom.HeightPixels);
        Assert.Equal((int)Math.Round((1.0 / 25.4) * 600), custom.SafeMarginPixels);
    }

    [Fact]
    public void WithOrientation_SwapsDimensionsCorrectly()
    {
        var landscape = CardFormat.CR80;
        var portrait = landscape.WithOrientation(true);

        Assert.True(portrait.IsPortrait);
        Assert.Equal(landscape.WidthMm, portrait.HeightMm);
        Assert.Equal(landscape.HeightMm, portrait.WidthMm);
        Assert.Equal(landscape.WidthPixels, portrait.HeightPixels);
        Assert.Equal(landscape.HeightPixels, portrait.WidthPixels);
    }
}
