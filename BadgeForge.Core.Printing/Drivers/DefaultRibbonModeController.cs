using BadgeForge.Core.Printing.Interfaces;

namespace BadgeForge.Core.Printing.Drivers;

/// <summary>
/// Default implementation of <see cref="IRibbonModeController"/> supporting both
/// pure RGB(0,0,0) pixel extraction and driver-level DEVMODE resin-mode flags.
/// </summary>
public class DefaultRibbonModeController : IRibbonModeController
{
    public KResinRoutingMode ActiveMode { get; set; } = KResinRoutingMode.PixelBlackExtraction;

    /// <summary>
    /// Default density adjustment is +20, matching recommended IDP SMART-31 black density
    /// for barcodes and fine text.
    /// </summary>
    public int ResinDensityAdjustment { get; set; } = 20;

    public int BlackPixelThreshold { get; set; } = 0;

    public bool RequiresRealHardwareVerification => true;

    public string HardwareVerificationNotes =>
        "HARDWARE VERIFICATION REQUIRED FOR IDP SMART-31:\n" +
        "1. In PixelBlackExtraction mode: Verify that SkiaSharp rendered text with alias edging (RGB 0,0,0) " +
        "transfers using the K ribbon panel with zero YMC halo or halftone dots.\n" +
        "2. In DriverDevModeFlag mode: Verify if the specific driver version installed requires the " +
        "'Black Extraction' driver property to be toggled via DEVMODE dmDriverData or if the standard GDI " +
        "color-matching pipeline handles black separation automatically.\n" +
        "Run an empirical print test with a test card containing both a 100% black barcode and a 99% black box.";

    private bool _forceResinBlack = true;

    public void ConfigureForResinText(bool forceResinBlack)
    {
        _forceResinBlack = forceResinBlack;
    }

    public bool IsResinForced() => _forceResinBlack;
}
