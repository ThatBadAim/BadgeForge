using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Models;
using Xunit;

namespace BadgeForge.Tests;

public class CapabilitiesProbeTests
{
    [Fact]
    public void ValidateCompatibility_Succeeds_WhenDriverAndFormatMatch()
    {
        var format = CardFormat.CR80;
        var capabilities = new PrinterCapabilities
        {
            PrinterName = "SMART-31",
            DriverName = "IDP SMART-31",
            ResolutionDpiX = 300,
            ResolutionDpiY = 300,
            PrintableWidthPixels = format.WidthPixels,
            PrintableHeightPixels = format.HeightPixels
        };

        bool isValid = capabilities.ValidateCompatibility(format, out string mismatchReason);

        Assert.True(isValid);
        Assert.Empty(mismatchReason);
        // EnsureCompatible should not throw
        capabilities.EnsureCompatible(format);
    }

    [Fact]
    public void EnsureCompatible_ThrowsLoudly_WhenDpiMismatches()
    {
        var format = CardFormat.CR80; // 300 DPI
        var capabilities = new PrinterCapabilities
        {
            PrinterName = "SMART-31",
            DriverName = "IDP SMART-31",
            ResolutionDpiX = 600, // Disagrees with format
            ResolutionDpiY = 600,
            PrintableWidthPixels = (int)Math.Round((85.60 / 25.4) * 600),
            PrintableHeightPixels = (int)Math.Round((53.98 / 25.4) * 600)
        };

        var ex = Assert.Throws<PrinterCapabilityMismatchException>(() =>
            capabilities.EnsureCompatible(format));

        Assert.Contains("DPI", ex.Message);
        Assert.Equal(600, ex.DriverDpiX);
        Assert.Equal(300, ex.ExpectedDpi);
    }

    [Fact]
    public void EnsureCompatible_ThrowsLoudly_WhenCanvasDimensionsMismatchToPreventSilentStretching()
    {
        var format = CardFormat.CR80; // 1011 x 638 px
        var capabilities = new PrinterCapabilities
        {
            PrinterName = "SMART-31",
            DriverName = "IDP SMART-31",
            ResolutionDpiX = 300,
            ResolutionDpiY = 300,
            // Simulates driver configured for Letter/A4 instead of CR80 card
            PrintableWidthPixels = 2400,
            PrintableHeightPixels = 3300
        };

        var ex = Assert.Throws<PrinterCapabilityMismatchException>(() =>
            capabilities.EnsureCompatible(format));

        Assert.Contains("disagrees with expected format canvas", ex.Message);
        Assert.Contains("Silent stretching prevented", ex.Message);
        Assert.Equal(2400, ex.DriverCanvasWidth);
        Assert.Equal(format.WidthPixels, ex.ExpectedCanvasWidth);
    }
}
