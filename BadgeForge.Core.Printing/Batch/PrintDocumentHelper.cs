using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Models;
using SkiaSharp;

namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Helper for Windows System.Drawing.Printing.PrintDocument configuration.
/// Ensures strict single-card spooling, correct physical paper sizing and orientation,
/// and draws pre-rendered SKBitmap buffers onto PrintPageEventArgs.Graphics.
/// </summary>
public static class PrintDocumentHelper
{
    /// <summary>
    /// How far a driver's paper form may differ from the card, in hundredths of an inch summed over both edges,
    /// before it is treated as a different size. Drivers declare card forms in whole hundredths (3 dots at 300 DPI),
    /// so an exact match is unrealistic, while CR80 and CR79 differ by far more than this.
    /// </summary>
    private const int PaperMatchToleranceHundredths = 10;

    /// <summary>
    /// Largest page-versus-card difference, in printer dots, still treated as millimetre rounding worth centring.
    /// 16 dots is 1.35mm at 300 DPI — well past any legitimate rounding, so anything bigger means the page size
    /// couldn't be read sensibly and the card is printed from the corner instead of being shoved off the edge.
    /// </summary>
    private const int MaxCentringDots = 16;

    /// <summary>
    /// Spools one card (front, plus back when double-sided) to a Windows printer as a single print job and waits
    /// until the spooler has accepted it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void PrintCard(string printerName, CardFormat format, string documentName, SKBitmap front, SKBitmap? back = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        using var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;
        if (!document.PrinterSettings.IsValid)
        {
            throw new PrinterException($"Printer '{printerName}' is not installed. No card was sent.");
        }

        // No "Printing page 1" progress window from a background thread
        document.PrintController = new StandardPrintController();

        if (back != null && document.PrinterSettings.CanDuplex)
        {
            // Flip direction for the card back needs confirming on the physical printer
            document.PrinterSettings.Duplex = Duplex.Vertical;
        }

        ConfigurePrintDocument(document, format, documentName, front, back);
        document.Print();
    }

    /// <summary>
    /// Configures a PrintDocument for an exact single-card badge print operation.
    /// Selects the driver's own paper form for the active CardFormat, configures landscape/portrait orientation,
    /// and hooks the PrintPage event to render the pre-rendered SKBitmap to the target graphics device context.
    /// The handlers detach themselves when printing ends, so a document can be configured again.
    /// </summary>
    /// <param name="document">PrintDocument instance.</param>
    /// <param name="format">Target physical card format profile.</param>
    /// <param name="jobName">Identifiable correlated job name.</param>
    /// <param name="cardBitmap">Pre-rendered 300 DPI SKBitmap of the card front.</param>
    /// <param name="backBitmap">Optional pre-rendered card back, printed as the second side of the same card.</param>
    [SupportedOSPlatform("windows")]
    public static void ConfigurePrintDocument(
        PrintDocument document,
        CardFormat format,
        string jobName,
        SKBitmap cardBitmap,
        SKBitmap? backBitmap = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(cardBitmap);

        document.DocumentName = jobName;

        var paper = SelectPaperSize(document.PrinterSettings, format);
        document.DefaultPageSettings.PaperSize = paper;

        // The chosen form may already be landscape, so only turn the page when the form and the card disagree —
        // turning an already-landscape form would print the card sideways.
        bool cardIsLandscape = format.WidthMm >= format.HeightMm;
        bool paperIsLandscape = paper.Width > paper.Height;
        document.DefaultPageSettings.Landscape = cardIsLandscape != paperIsLandscape;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.OriginAtMargins = false;

        var pages = backBitmap == null ? new[] { cardBitmap } : new[] { cardBitmap, backBitmap };
        int nextPage = 0;

        void OnBeginPrint(object? sender, PrintEventArgs e) => nextPage = 0;

        void OnPrintPage(object? sender, PrintPageEventArgs e)
        {
            if (e.Graphics != null)
            {
                DrawCardPage(e.Graphics, pages[nextPage], e.PageSettings, e.PageBounds);
            }

            // CRITICAL: Strictly one card at a time — a second page is only ever the same card's back
            nextPage++;
            e.HasMorePages = nextPage < pages.Length;
        }

        void OnEndPrint(object? sender, PrintEventArgs e)
        {
            document.BeginPrint -= OnBeginPrint;
            document.PrintPage -= OnPrintPage;
            document.EndPrint -= OnEndPrint;
        }

        document.BeginPrint += OnBeginPrint;
        document.PrintPage += OnPrintPage;
        document.EndPrint += OnEndPrint;
    }

    /// <summary>
    /// The driver's own paper form for this card format, or a custom form when the driver defines no match.
    /// </summary>
    /// <remarks>
    /// A driver-defined form carries the paper id the driver actually understands (dmPaperSize). A custom PaperSize
    /// only reaches the driver through DM_PAPERWIDTH/DM_PAPERLENGTH, which card drivers frequently don't offer — the
    /// size is then dropped without an error and the card prints on whatever form the driver defaults to.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static PaperSize SelectPaperSize(PrinterSettings settings, CardFormat format)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(format);

        int shortEdge = ToHundredthsOfInch(Math.Min(format.WidthMm, format.HeightMm));
        int longEdge = ToHundredthsOfInch(Math.Max(format.WidthMm, format.HeightMm));

        PaperSize? closest = null;
        int closestDelta = int.MaxValue;
        try
        {
            if (settings.IsValid)
            {
                foreach (PaperSize candidate in settings.PaperSizes)
                {
                    int candidateShort = Math.Min(candidate.Width, candidate.Height);
                    int candidateLong = Math.Max(candidate.Width, candidate.Height);
                    int delta = Math.Abs(candidateShort - shortEdge) + Math.Abs(candidateLong - longEdge);
                    if (delta <= PaperMatchToleranceHundredths && delta < closestDelta)
                    {
                        closest = candidate;
                        closestDelta = delta;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PrintDocumentHelper] Couldn't read the driver's paper forms: {ex.Message}");
        }

        // The fallback form is given portrait (short edge first) and turned by Landscape in ConfigurePrintDocument.
        // Standard CR80: 85.60mm x 53.98mm -> 3.37" x 2.125" -> 213 x 337 hundredths
        return closest ?? new PaperSize(format.Name, shortEdge, longEdge);
    }

    /// <summary>
    /// Draws one pre-rendered card onto a printer page, one rendered pixel to one printer dot.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void DrawCardPage(
        System.Drawing.Graphics graphics,
        SKBitmap bitmap,
        PageSettings pageSettings,
        System.Drawing.Rectangle pageBoundsHundredths)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(pageSettings);

        // Work in printer dots rather than the default hundredths of an inch, so the card is blitted 1:1. Any
        // rescale here resamples pure RGB(0,0,0) text and barcodes into near-black greys, and the printer only
        // routes exact black to the K-resin panel — the aliased rendering upstream would be thrown away.
        graphics.PageUnit = System.Drawing.GraphicsUnit.Pixel;

        float dpiX = graphics.DpiX > 0 ? graphics.DpiX : 300f;
        float dpiY = graphics.DpiY > 0 ? graphics.DpiY : 300f;

        // Drawing starts at the printable area's corner; shift back so the image covers the whole physical card
        graphics.TranslateTransform(
            -HundredthsToDots(pageSettings.HardMarginX, dpiX),
            -HundredthsToDots(pageSettings.HardMarginY, dpiY));

        int pageWidthDots = (int)Math.Round(HundredthsToDots(pageBoundsHundredths.Width, dpiX));
        int pageHeightDots = (int)Math.Round(HundredthsToDots(pageBoundsHundredths.Height, dpiY));

        // The driver's page and the rendered canvas can differ by a dot or two from rounding millimetres. Centre the
        // card on the page instead of scaling it to fit, so every rendered pixel reaches the printer untouched.
        var destination = new System.Drawing.Rectangle(
            CentringOffset(pageWidthDots, bitmap.Width),
            CentringOffset(pageHeightDots, bitmap.Height),
            bitmap.Width,
            bitmap.Height);

        DrawSkiaBitmapToGraphics(graphics, bitmap, destination);
    }

    /// <summary>
    /// Draws an SKBitmap onto a System.Drawing.Graphics surface using GDI+ coordinates.
    /// </summary>
    /// <remarks>
    /// The destination is expected to be the bitmap's own pixel size in the graphics' current unit; anything else
    /// resamples the card and greys out the pure black that K-resin routing depends on.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static void DrawSkiaBitmapToGraphics(
        System.Drawing.Graphics graphics,
        SKBitmap bitmap,
        System.Drawing.Rectangle destinationBounds)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(bitmap);

        using var gdiBitmap = ToOpaqueGdiBitmap(bitmap);

        // Nearest-neighbour with no half-pixel offset: at 1:1 every rendered dot reaches the printer unchanged and
        // pure black stays pure black. Bicubic sampling would fringe text and barcode edges into greys that the
        // printer then lays down with the YMC dye panels instead of the K-resin panel.
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

        graphics.DrawImage(gdiBitmap, destinationBounds);
    }

    /// <summary>
    /// Copies an SKBitmap into an opaque 24-bit GDI+ bitmap, pixel for pixel.
    /// </summary>
    /// <remarks>
    /// Going through an encoded PNG instead costs a compress and decompress per card, and hands GDI+ an alpha
    /// channel it then blends — another chance for an exactly-black pixel to come out a shade off.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static System.Drawing.Bitmap ToOpaqueGdiBitmap(SKBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Flatten onto white first: badges are printed on white cards, and dropping the alpha channel of a
        // premultiplied buffer would otherwise composite anything translucent against black.
        using var flattened = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(flattened))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        }

        var gdiBitmap = new System.Drawing.Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var bounds = new System.Drawing.Rectangle(0, 0, source.Width, source.Height);
        var locked = gdiBitmap.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            ReadOnlySpan<byte> pixels = flattened.GetPixelSpan();
            int sourceStride = flattened.RowBytes;
            var row = new byte[source.Width * 3];

            for (int y = 0; y < source.Height; y++)
            {
                var sourceRow = pixels.Slice(y * sourceStride, source.Width * 4);
                for (int x = 0; x < source.Width; x++)
                {
                    // Skia BGRA8888 -> GDI+ 24bpp BGR, dropping the now-opaque alpha byte
                    row[(x * 3) + 0] = sourceRow[(x * 4) + 0];
                    row[(x * 3) + 1] = sourceRow[(x * 4) + 1];
                    row[(x * 3) + 2] = sourceRow[(x * 4) + 2];
                }

                Marshal.Copy(row, 0, locked.Scan0 + (y * locked.Stride), row.Length);
            }
        }
        catch
        {
            gdiBitmap.Dispose();
            throw;
        }
        finally
        {
            gdiBitmap.UnlockBits(locked);
        }

        return gdiBitmap;
    }

    /// <summary>
    /// Half the difference between page and card when that is small enough to be millimetre rounding; 0 otherwise.
    /// </summary>
    private static int CentringOffset(int pageDots, int bitmapDots)
    {
        int difference = pageDots - bitmapDots;
        return Math.Abs(difference) <= MaxCentringDots ? difference / 2 : 0;
    }

    private static float HundredthsToDots(float hundredthsOfInch, float dpi) => hundredthsOfInch / 100f * dpi;

    private static int ToHundredthsOfInch(double millimeters) => (int)Math.Round((millimeters / 25.4) * 100);
}
