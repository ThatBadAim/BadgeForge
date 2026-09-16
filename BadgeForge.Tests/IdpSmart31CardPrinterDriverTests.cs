using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class IdpSmart31CardPrinterDriverTests
{
    [Fact]
    public async Task ProbeCapabilities_ReturnsDynamicCapabilities_NotHardcodedConstant()
    {
        using var driver = new IdpSmart31CardPrinterDriver("SMART-31");
        var capabilities = await driver.ProbeCapabilitiesAsync("SMART-31");

        Assert.NotNull(capabilities);
        Assert.Equal(300, capabilities.ResolutionDpiX);
        Assert.Equal(300, capabilities.ResolutionDpiY);
        Assert.True(capabilities.PrintableWidthPixels > 0);
        Assert.True(capabilities.PrintableHeightPixels > 0);
        Assert.True(capabilities.RawDriverProperties.ContainsKey("DriverSource"));
    }

    [Fact]
    public void RibbonController_SupportsDualRoutingPaths_WithHardwareVerificationFlag()
    {
        using var driver = new IdpSmart31CardPrinterDriver();
        var controller = driver.RibbonController;

        // Default routing mode is PixelBlackExtraction
        Assert.Equal(KResinRoutingMode.PixelBlackExtraction, controller.ActiveMode);
        Assert.Equal(20, controller.ResinDensityAdjustment); // IDP SMART-31 recommended density
        Assert.True(controller.RequiresRealHardwareVerification);
        Assert.Contains("HARDWARE VERIFICATION REQUIRED", controller.HardwareVerificationNotes);

        // Can switch to DriverDevModeFlag
        controller.ActiveMode = KResinRoutingMode.DriverDevModeFlag;
        Assert.Equal(KResinRoutingMode.DriverDevModeFlag, controller.ActiveMode);
    }

    [Fact]
    public async Task SubmitCard_ThrowsLoudly_WhenCanvasDimensionsMismatch()
    {
        using var driver = new IdpSmart31CardPrinterDriver();

        // Mismatched format (e.g. 500x500 px custom badge that does not match probed card canvas)
        var mismatchedFormat = new CardFormat
        {
            Name = "Mismatched Format",
            WidthMm = 42.0,
            HeightMm = 42.0,
            Dpi = 300
        };

        var job = new CardPrintJob
        {
            JobId = "MISMATCH-01",
            Format = mismatchedFormat,
            BatchIndex = 1
        };

        var ex = await Assert.ThrowsAsync<PrinterCapabilityMismatchException>(() =>
            driver.SubmitCardAsync(job));

        Assert.Contains("Silent stretching prevented", ex.Message);
    }

    [Fact]
    public async Task SubmitCard_FailsLoudly_WhenThePrinterIsNotAvailable()
    {
        // Previously this "succeeded" without sending anything, so a whole batch could be reported printed
        using var driver = new IdpSmart31CardPrinterDriver(MissingPrinterName);
        var cr80 = CardFormat.CR80;

        var job = new CardPrintJob
        {
            JobId = "CR80-TEST-01",
            Format = cr80,
            BatchIndex = 1,
            FrontPixelBuffer = CreateCardPng(cr80)
        };

        var ex = await Assert.ThrowsAsync<PrinterException>(() => driver.SubmitCardAsync(job));

        Assert.Contains("No card was sent", ex.Message);
        Assert.Equal(0, driver.StatusMonitor.CurrentStatus.CardsPrintedSincePause);
    }

    [Fact]
    public async Task ProbeCapabilities_MarksTheProfileSimulated_WhenThePrinterIsNotAvailable()
    {
        using var driver = new IdpSmart31CardPrinterDriver(MissingPrinterName);

        var capabilities = await driver.ProbeCapabilitiesAsync(MissingPrinterName);

        Assert.True(capabilities.IsSimulated);
        Assert.True(capabilities.RawDriverProperties.ContainsKey("UnavailableReason"));
    }

    [Theory]
    [InlineData(0x0u, PrinterState.Ready)]
    [InlineData(0x400000u, PrinterState.CoverOpen)]
    [InlineData(0x8u, PrinterState.PaperJam)]
    [InlineData(0x10u, PrinterState.OutOfCards)]
    [InlineData(0x40000u, PrinterState.OutOfRibbon)]
    [InlineData(0x80u, PrinterState.Offline)]
    [InlineData(0x800u, PrinterState.OperatorPauseRequired)]
    [InlineData(0x2u, PrinterState.Error)]
    [InlineData(0x1u, PrinterState.Paused)]
    [InlineData(0x400u, PrinterState.Printing)]
    [InlineData(0x400408u, PrinterState.CoverOpen)] // cover open outranks jam and printing
    public void SpoolerStatusMapper_MapsWindowsPrinterStatusFlags(uint printerStatus, PrinterState expected)
    {
        var (state, message) = SpoolerStatusMapper.Map(new SpoolerSnapshot(printerStatus, Array.Empty<SpoolerJob>()), activeJob: null);

        Assert.Equal(expected, state);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void SpoolerStatusMapper_TracksTheActiveJob_AndReportsAStuckJobAsAnError()
    {
        var printing = new SpoolerJob(7, "BF_E-1_0123456789ab", Status: 0x10);
        Assert.Equal(PrinterState.Printing, SpoolerStatusMapper.Map(new SpoolerSnapshot(0, new[] { printing }), printing).State);

        var stuck = printing with { Status = 0x2 };
        Assert.Equal(PrinterState.Error, SpoolerStatusMapper.Map(new SpoolerSnapshot(0, new[] { stuck }), stuck).State);

        Assert.False(printing.IsFinished);
        Assert.True((printing with { Status = 0x1000 }).IsFinished);
    }

    private const string MissingPrinterName = "BadgeForge Test Printer That Is Not Installed";

    private static byte[] CreateCardPng(CardFormat format)
    {
        using var bitmap = new SKBitmap(format.WidthPixels, format.HeightPixels);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
