namespace BadgeForge.Core.Printing.Interfaces;

/// <summary>
/// Strategy for routing monochrome text and barcode elements to the K-resin ribbon panel
/// rather than mixing composite color via YMC dye sublimation panels.
/// </summary>
public enum KResinRoutingMode
{
    /// <summary>
    /// Path A: Pixel-level RGB(0,0,0) detection.
    /// The printer driver inspects incoming raster pixels. Any pixel with strict RGB(0,0,0)
    /// (with anti-aliasing disabled: SKFont.Edging = Alias, Subpixel = false) is extracted
    /// and printed exclusively with the thermal transfer K-resin panel. Non-black pixels are
    /// routed to the YMC dye panels.
    /// </summary>
    PixelBlackExtraction,

    /// <summary>
    /// Path B: Driver-level resin-mode flag / DEVMODE private field.
    /// The driver or proprietary spooler setup requires setting a driver configuration flag
    /// or submitting a dedicated monochrome overlay channel to activate K-resin thermal transfer.
    /// </summary>
    DriverDevModeFlag
}

/// <summary>
/// Controls ribbon panel routing and thermal heat intensity for K-resin monochrome printing.
/// </summary>
public interface IRibbonModeController
{
    /// <summary>
    /// The active mechanism used to route text and barcodes to the K-resin panel.
    /// Default is <see cref="KResinRoutingMode.PixelBlackExtraction"/> with support for switching.
    /// </summary>
    KResinRoutingMode ActiveMode { get; set; }

    /// <summary>
    /// Density / thermal heat adjustment for the K-resin head (-100 to +100).
    /// Defaults to +20 on IDP SMART-31 hardware to prevent patchy resin coverage on barcodes.
    /// </summary>
    int ResinDensityAdjustment { get; set; }

    /// <summary>
    /// Maximum RGB value threshold treated as resin black when in PixelBlackExtraction mode.
    /// Default is 0 (strict pure black: R=0, G=0, B=0).
    /// </summary>
    int BlackPixelThreshold { get; set; }

    /// <summary>
    /// Flags that this routing mechanism requires empirical confirmation on physical IDP SMART-31 hardware.
    /// </summary>
    bool RequiresRealHardwareVerification { get; }

    /// <summary>
    /// Detailed diagnostic notes explaining the two routing paths and steps required
    /// to empirically verify resin routing on the physical machine.
    /// </summary>
    string HardwareVerificationNotes { get; }

    /// <summary>
    /// Configures the printer driver settings or raster preparation pipeline for K-resin text.
    /// </summary>
    /// <param name="forceResinBlack">When true, enables strict K-resin routing for text/barcodes.</param>
    void ConfigureForResinText(bool forceResinBlack);
}
