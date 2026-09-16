using BadgeForge.Core.Printing.Exceptions;

namespace BadgeForge.Core.Printing.Models;

/// <summary>
/// Probed printer capabilities queried from the installed driver at runtime.
/// Never hardcodes canvas pixel sizes; enables runtime assertion of dimensions.
/// </summary>
public record PrinterCapabilities
{
    /// <summary>
    /// Operating system device / queue name of the printer.
    /// </summary>
    public string PrinterName { get; init; } = string.Empty;

    /// <summary>
    /// Installed printer driver model name reported by Windows spooler.
    /// </summary>
    public string DriverName { get; init; } = string.Empty;

    /// <summary>
    /// Horizontal resolution in DPI reported by driver.
    /// </summary>
    public int ResolutionDpiX { get; init; } = 300;

    /// <summary>
    /// Vertical resolution in DPI reported by driver.
    /// </summary>
    public int ResolutionDpiY { get; init; } = 300;

    /// <summary>
    /// Actual printable canvas pixel width queried from driver.
    /// </summary>
    public int PrintableWidthPixels { get; init; }

    /// <summary>
    /// Actual printable canvas pixel height queried from driver.
    /// </summary>
    public int PrintableHeightPixels { get; init; }

    /// <summary>
    /// Physical printable width in millimeters calculated from driver capabilities.
    /// </summary>
    public double PrintableWidthMm => ResolutionDpiX > 0 ? (PrintableWidthPixels * 25.4) / ResolutionDpiX : 0.0;

    /// <summary>
    /// Physical printable height in millimeters calculated from driver capabilities.
    /// </summary>
    public double PrintableHeightMm => ResolutionDpiY > 0 ? (PrintableHeightPixels * 25.4) / ResolutionDpiY : 0.0;

    /// <summary>
    /// True if the printer supports multi-panel color ribbons (YMCKO).
    /// </summary>
    public bool SupportsColor { get; init; } = true;

    /// <summary>
    /// True if an automatic flipper / dual-sided printing unit is installed.
    /// </summary>
    public bool SupportsDuplex { get; init; } = false;

    /// <summary>
    /// True when no real printer answered the probe and these values are a stand-in profile.
    /// A simulated profile is good enough for previews, but cards must never be "printed" against it.
    /// </summary>
    public bool IsSimulated { get; init; }

    /// <summary>
    /// Additional driver-specific properties or DEVMODE capabilities queried at probe time.
    /// </summary>
    public IReadOnlyDictionary<string, string> RawDriverProperties { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// How far the driver's canvas may differ from the card format before it counts as the wrong card size.
    /// </summary>
    /// <remarks>
    /// A driver declares its card form in whole hundredths of an inch — three dots at 300 DPI — so the nominal
    /// ISO dimensions never land on it exactly, and demanding an exact match blocks printing on a correctly
    /// configured printer. Half a millimetre absorbs that rounding while still being far smaller than the
    /// 1.7mm x 3.0mm that separates CR79 from CR80.
    /// </remarks>
    public const double DimensionToleranceMm = 0.5;

    /// <summary>
    /// <see cref="DimensionToleranceMm"/> expressed in pixels at this printer's reported resolution.
    /// </summary>
    public int DimensionTolerancePixels =>
        Math.Max(2, (int)Math.Ceiling(DimensionToleranceMm / 25.4 * Math.Max(ResolutionDpiX, ResolutionDpiY)));

    /// <summary>
    /// The printer canvas in pixels, turned to match the card's orientation. Drivers report the canvas one way round;
    /// a portrait card is the same physical card turned a quarter turn, not a different canvas.
    /// </summary>
    public (int Width, int Height) GetCanvasSizeFor(CardFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        int longSide = Math.Max(PrintableWidthPixels, PrintableHeightPixels);
        int shortSide = Math.Min(PrintableWidthPixels, PrintableHeightPixels);
        return format.WidthMm >= format.HeightMm ? (longSide, shortSide) : (shortSide, longSide);
    }

    /// <summary>
    /// Validates whether the driver capabilities are compatible with the specified card format.
    /// Allows <see cref="DimensionToleranceMm"/> of rounding between the driver's declared card form and the
    /// format's nominal millimetres, but fails loudly if DPI or canvas size deviates beyond that.
    /// Orientation is not a mismatch: a portrait card is compared with the canvas turned to match.
    /// </summary>
    /// <param name="pixelTolerance">Overrides the tolerance derived from <see cref="DimensionToleranceMm"/>.</param>
    public bool ValidateCompatibility(CardFormat format, out string mismatchReason, int? pixelTolerance = null)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (ResolutionDpiX != format.Dpi || ResolutionDpiY != format.Dpi)
        {
            mismatchReason = $"Driver DPI ({ResolutionDpiX}x{ResolutionDpiY}) does not match expected card format DPI ({format.Dpi}).";
            return false;
        }

        int tolerance = pixelTolerance ?? DimensionTolerancePixels;
        var (canvasWidth, canvasHeight) = GetCanvasSizeFor(format);
        int widthDelta = Math.Abs(canvasWidth - format.WidthPixels);
        int heightDelta = Math.Abs(canvasHeight - format.HeightPixels);

        if (widthDelta > tolerance || heightDelta > tolerance)
        {
            string driverPaper = RawDriverProperties.TryGetValue("PaperSize", out string? paper) ? $" Driver paper form: {paper}." : string.Empty;
            mismatchReason =
                $"Driver printable area ({PrintableWidthPixels}x{PrintableHeightPixels}px, {PrintableWidthMm:0.00}x{PrintableHeightMm:0.00}mm) " +
                $"disagrees with expected format canvas ({format.WidthPixels}x{format.HeightPixels}px, {format.WidthMm:0.00}x{format.HeightMm:0.00}mm, name: '{format.Name}') " +
                $"by more than {DimensionToleranceMm:0.0}mm.{driverPaper} Set the printer's default paper size to this card and try again. Silent stretching prevented.";
            return false;
        }

        mismatchReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Asserts compatibility with the card format, throwing a loud <see cref="PrinterCapabilityMismatchException"/>
    /// if there is any mismatch.
    /// </summary>
    public void EnsureCompatible(CardFormat format, int? pixelTolerance = null)
    {
        if (!ValidateCompatibility(format, out string mismatchReason, pixelTolerance))
        {
            throw new PrinterCapabilityMismatchException(
                printerName: PrinterName,
                message: $"Printer '{PrinterName}' capability validation failed: {mismatchReason}",
                driverDpiX: ResolutionDpiX,
                driverDpiY: ResolutionDpiY,
                expectedDpi: format.Dpi,
                driverCanvasWidth: PrintableWidthPixels,
                driverCanvasHeight: PrintableHeightPixels,
                expectedCanvasWidth: format.WidthPixels,
                expectedCanvasHeight: format.HeightPixels
            );
        }
    }
}
