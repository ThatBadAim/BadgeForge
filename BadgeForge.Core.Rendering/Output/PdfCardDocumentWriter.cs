using BadgeForge.Core.Printing.Models;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Output;

/// <summary>
/// Writes rendered cards into one multi-page PDF, one card per page, each page exactly the physical card size
/// (CR80 = 85.60 × 53.98 mm ≈ 242.6 × 153.0 pt). Card bitmaps are embedded losslessly at their own resolution.
/// </summary>
public sealed class PdfCardDocumentWriter : IDisposable
{
    private const float PointsPerInch = 72f;

    // Skia snaps PDF page sizes to its raster grid (72 / RasterDpi points). At 300 that turns a 53.98 mm card into
    // 54.02 mm; at 7200 the grid is 0.01 pt, so pages are the exact card size. Card bitmaps are embedded at their own
    // resolution either way — this value only applies to effects Skia would have to rasterize, and cards draw none.
    private const float PageGridDpi = 7200f;

    private readonly SKManagedWStream _stream;
    private readonly SKDocument _document;
    private bool _closed;

    /// <param name="output">Destination stream; left open.</param>
    /// <param name="format">Card format giving the page size.</param>
    /// <param name="title">Document title shown by PDF viewers.</param>
    public PdfCardDocumentWriter(Stream output, CardFormat format, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(format);

        PageWidthPoints = (float)(format.WidthMm / 25.4 * PointsPerInch);
        PageHeightPoints = (float)(format.HeightMm / 25.4 * PointsPerInch);

        _stream = new SKManagedWStream(output, disposeManagedStream: false);
        var now = DateTime.Now;
        _document = SKDocument.CreatePdf(_stream, new SKDocumentPdfMetadata
        {
            Title = title ?? "Badges",
            Creator = "BadgeForge",
            Producer = "BadgeForge",
            RasterDpi = PageGridDpi,
            EncodingQuality = 101, // Lossless: no JPEG artefacts around text and barcodes
            Creation = now,
            Modified = now
        }) ?? throw new InvalidOperationException("PDF output isn't available in this SkiaSharp build.");
    }

    public float PageWidthPoints { get; }

    public float PageHeightPoints { get; }

    public int PageCount { get; private set; }

    /// <summary>
    /// Adds a page holding the card bitmap scaled to the full page.
    /// </summary>
    public void AddCard(SKBitmap cardBitmap)
    {
        ArgumentNullException.ThrowIfNull(cardBitmap);
        ObjectDisposedException.ThrowIf(_closed, this);

        using var image = SKImage.FromBitmap(cardBitmap);
        var canvas = _document.BeginPage(PageWidthPoints, PageHeightPoints);
        try
        {
            canvas.DrawImage(image, new SKRect(0, 0, PageWidthPoints, PageHeightPoints), new SKSamplingOptions(SKFilterMode.Linear));
        }
        finally
        {
            _document.EndPage();
        }

        PageCount++;
    }

    /// <summary>
    /// Finishes the PDF and flushes it to the stream.
    /// </summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _document.Close();
        _stream.Flush();
    }

    public void Dispose()
    {
        Close();
        _document.Dispose();
        _stream.Dispose();
    }
}
