using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// Covers the last stretch between a rendered card and the printer: the tolerance that decides whether a real
/// driver's card form is accepted at all, and the pixel fidelity the K-resin panel depends on.
/// </summary>
public class PrintPathFidelityTests
{
    private static PrinterCapabilities CapabilitiesOf(int widthPixels, int heightPixels) => new()
    {
        PrinterName = "SMART-31",
        DriverName = "IDP SMART-31",
        ResolutionDpiX = 300,
        ResolutionDpiY = 300,
        PrintableWidthPixels = widthPixels,
        PrintableHeightPixels = heightPixels
    };

    [Fact]
    public void DimensionTolerance_IsHalfAMillimetreOfDots()
    {
        // 0.5mm at 300 DPI is just under 6 dots; a driver form rounded to whole hundredths of an inch moves by 3
        Assert.Equal(6, CapabilitiesOf(1011, 638).DimensionTolerancePixels);
    }

    [Fact]
    public void ValidateCompatibility_AcceptsADriverFormRoundedToWholeHundredthsOfAnInch()
    {
        // A real driver declares CR-80 in whole hundredths of an inch — 3.38" x 2.13" rather than the nominal
        // 85.60 x 53.98mm — which lands 3 dots wide of the format's 1011x638 canvas.
        var capabilities = CapabilitiesOf(1014, 639);

        Assert.True(
            capabilities.ValidateCompatibility(CardFormat.CR80, out string mismatchReason),
            $"A rounded CR-80 driver form must not block printing, but: {mismatchReason}");
    }

    [Fact]
    public void ValidateCompatibility_StillRejectsADifferentCardSize()
    {
        // CR79 is 1.7mm narrower and 3.0mm shorter than CR80 — far outside the rounding tolerance
        var capabilities = CapabilitiesOf(CardFormat.CR79.WidthPixels, CardFormat.CR79.HeightPixels);

        Assert.False(capabilities.ValidateCompatibility(CardFormat.CR80, out string mismatchReason));
        Assert.Contains("Silent stretching prevented", mismatchReason);
    }

    [Fact]
    public void ValidateCompatibility_MismatchSaysWhatToChange()
    {
        var capabilities = CapabilitiesOf(2400, 3300) with
        {
            RawDriverProperties = new Dictionary<string, string> { ["PaperSize"] = "A4 (827x1169 hundredths of an inch)" }
        };

        Assert.False(capabilities.ValidateCompatibility(CardFormat.CR80, out string mismatchReason));
        Assert.Contains("A4", mismatchReason);
        Assert.Contains("default paper size", mismatchReason);
    }

    [Fact]
    public async Task SubmitCard_ThrowsLoudly_WhenRibbonRoutingNeedsTheUnimplementedDevModeFlag()
    {
        var controller = new DefaultRibbonModeController { ActiveMode = KResinRoutingMode.DriverDevModeFlag };
        using var driver = new IdpSmart31CardPrinterDriver(ribbonController: controller);

        using var bitmap = new SKBitmap(CardFormat.CR80.WidthPixels, CardFormat.CR80.HeightPixels);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        var job = new CardPrintJob
        {
            JobId = "DEVMODE-01",
            Format = CardFormat.CR80,
            FrontPixelBuffer = png.ToArray()
        };

        var ex = await Assert.ThrowsAsync<PrinterException>(() => driver.SubmitCardAsync(job));
        Assert.Contains("DriverDevModeFlag", ex.Message);
        Assert.Contains("No card was sent", ex.Message);
    }

    [Fact]
    public void ToOpaqueGdiBitmap_KeepsPureBlackExactlyBlack()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return; // GDI+ is Windows-only; this runs on the machine the printer is attached to
        }

        using var card = new SKBitmap(new SKImageInfo(64, 32, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(card))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(10, 5, 8, 8, paint);
        }

        using var gdiBitmap = PrintDocumentHelper.ToOpaqueGdiBitmap(card);

        var black = gdiBitmap.GetPixel(12, 7);
        var white = gdiBitmap.GetPixel(0, 0);

        // Anything but an exact 0,0,0 is laid down by the YMC dye panels instead of the K-resin panel
        Assert.Equal(0, black.R);
        Assert.Equal(0, black.G);
        Assert.Equal(0, black.B);
        Assert.Equal(255, white.R);
        Assert.Equal(255, white.G);
        Assert.Equal(255, white.B);
    }

    [Fact]
    public void ToOpaqueGdiBitmap_FlattensTranslucentPixelsOntoWhite_NotBlack()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return;
        }

        using var card = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(card))
        {
            canvas.Clear(SKColors.Transparent);
        }

        using var gdiBitmap = PrintDocumentHelper.ToOpaqueGdiBitmap(card);
        var pixel = gdiBitmap.GetPixel(4, 4);

        Assert.Equal(255, pixel.R);
        Assert.Equal(255, pixel.G);
        Assert.Equal(255, pixel.B);
    }
}
