using SkiaSharp;

namespace BadgeForge.Core.Rendering.Testing;

/// <summary>
/// Result summary of a pixel-by-pixel image comparison.
/// </summary>
public record ImageDiffResult
{
    /// <summary>
    /// True if the images match within the given tolerances.
    /// </summary>
    public bool IsMatch => MismatchPixels == 0;

    /// <summary>
    /// Total number of pixels compared.
    /// </summary>
    public int TotalPixels { get; init; }

    /// <summary>
    /// Count of differing pixels that exceeded the channel tolerance.
    /// </summary>
    public int MismatchPixels { get; init; }

    /// <summary>
    /// Percentage of differing pixels relative to total image pixels (0.0 to 100.0).
    /// </summary>
    public double MismatchPercentage => TotalPixels > 0 ? (double)MismatchPixels / TotalPixels * 100.0 : 0.0;

    /// <summary>
    /// Maximum absolute delta observed across any color channel (R, G, B, A).
    /// </summary>
    public int MaxChannelDelta { get; init; }

    /// <summary>
    /// Visual difference bitmap highlighting differing pixels in magenta (#FF007F) and identical pixels dimmed.
    /// Caller is responsible for disposing this bitmap when present.
    /// </summary>
    public SKBitmap? DiffBitmap { get; init; }
}

/// <summary>
/// Service for pixel-by-pixel image comparison and golden-image regression verification.
/// </summary>
public static class ImageDiffService
{
    /// <summary>
    /// Compares two SKBitmap instances pixel-by-pixel, computing mismatch statistics
    /// and generating an optional visual diff highlighting discrepancies.
    /// </summary>
    /// <param name="expected">Reference golden bitmap.</param>
    /// <param name="actual">Rendered test bitmap.</param>
    /// <param name="channelTolerance">Allowed difference per RGB channel (default 0 for strict equality).</param>
    /// <param name="generateDiffBitmap">Whether to produce a visual diff SKBitmap.</param>
    /// <returns>Comparison result details.</returns>
    public static ImageDiffResult Compare(
        SKBitmap expected,
        SKBitmap actual,
        byte channelTolerance = 0,
        bool generateDiffBitmap = true)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new ImageDiffResult
            {
                TotalPixels = Math.Max(expected.Width * expected.Height, actual.Width * actual.Height),
                MismatchPixels = Math.Max(expected.Width * expected.Height, actual.Width * actual.Height),
                MaxChannelDelta = 255,
                DiffBitmap = null
            };
        }

        int width = expected.Width;
        int height = expected.Height;
        int totalPixels = width * height;
        int mismatchCount = 0;
        int maxDelta = 0;

        SKBitmap? diffBmp = generateDiffBitmap
            ? new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul)
            : null;

        var highlightColor = new SKColor(255, 0, 128, 255); // Vibrant magenta for diff highlighting

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pExpected = expected.GetPixel(x, y);
                var pActual = actual.GetPixel(x, y);

                int deltaR = Math.Abs(pExpected.Red - pActual.Red);
                int deltaG = Math.Abs(pExpected.Green - pActual.Green);
                int deltaB = Math.Abs(pExpected.Blue - pActual.Blue);
                int deltaA = Math.Abs(pExpected.Alpha - pActual.Alpha);

                int localMaxDelta = Math.Max(Math.Max(deltaR, deltaG), Math.Max(deltaB, deltaA));
                if (localMaxDelta > maxDelta)
                {
                    maxDelta = localMaxDelta;
                }

                bool isDifferent = deltaR > channelTolerance ||
                                   deltaG > channelTolerance ||
                                   deltaB > channelTolerance ||
                                   deltaA > channelTolerance;

                if (isDifferent)
                {
                    mismatchCount++;
                    diffBmp?.SetPixel(x, y, highlightColor);
                }
                else if (diffBmp != null)
                {
                    // Dim matching pixels to 20% opacity for context
                    byte dimmedR = (byte)(pActual.Red * 0.2);
                    byte dimmedG = (byte)(pActual.Green * 0.2);
                    byte dimmedB = (byte)(pActual.Blue * 0.2);
                    diffBmp.SetPixel(x, y, new SKColor(dimmedR, dimmedG, dimmedB, 255));
                }
            }
        }

        return new ImageDiffResult
        {
            TotalPixels = totalPixels,
            MismatchPixels = mismatchCount,
            MaxChannelDelta = maxDelta,
            DiffBitmap = diffBmp
        };
    }

    /// <summary>
    /// Saves an SKBitmap to disk as an uncompressed/standard PNG image.
    /// </summary>
    public static void SavePng(SKBitmap bitmap, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        // File.Create truncates: OpenWrite would leave the tail of a larger old file after the new PNG
        using var stream = File.Create(filePath);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Loads a PNG image from disk into an SKBitmap.
    /// </summary>
    public static SKBitmap LoadPng(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Golden reference file not found: {filePath}", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return SKBitmap.Decode(stream);
    }
}
