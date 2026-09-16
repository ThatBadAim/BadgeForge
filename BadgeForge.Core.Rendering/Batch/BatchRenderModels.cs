using SkiaSharp;

namespace BadgeForge.Core.Rendering.Batch;

/// <summary>
/// One card to render: who it is for and the token values it renders with (photo tokens hold a file path or data URI).
/// </summary>
/// <param name="Id">Identifier used in warnings and file names (e.g. the badge number).</param>
/// <param name="Label">Readable name shown in progress and warnings.</param>
/// <param name="Fields">Token values, keyed by token name without braces.</param>
public sealed record CardRenderJob(string Id, string Label, IReadOnlyDictionary<string, string> Fields);

/// <summary>
/// Settings for a batch render.
/// </summary>
public sealed record BatchRenderOptions
{
    /// <summary>
    /// Lowest resolution batch output may use: card printers and print shops expect at least 300 DPI.
    /// </summary>
    public const int MinimumDpi = 300;

    /// <summary>
    /// Output resolution in dots per inch (300 or more).
    /// </summary>
    public int Dpi { get; init; } = MinimumDpi;

    /// <summary>
    /// How many cards are rendered at once. Cards are still delivered in order; memory use grows with this value
    /// (about 2.6 MB per CR80 card at 300 DPI).
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Math.Clamp(Environment.ProcessorCount - 1, 1, 4);

    /// <summary>
    /// Print a neutral silhouette in photo frames of people with no usable photo, rather than leaving them blank.
    /// </summary>
    public bool DrawMissingPhotoPlaceholders { get; init; } = true;

    /// <summary>
    /// Fail the card (and the batch) when a barcode can't be encoded or doesn't fit, instead of skipping it.
    /// </summary>
    public bool StrictLayerRendering { get; init; }
}

/// <summary>
/// How far a batch render has got.
/// </summary>
/// <param name="Completed">Cards finished so far.</param>
/// <param name="Total">Cards in the batch.</param>
/// <param name="CurrentLabel">The card being worked on next, if any.</param>
public readonly record struct BatchRenderProgress(int Completed, int Total, string? CurrentLabel)
{
    /// <summary>
    /// Whole-number percentage complete.
    /// </summary>
    public int Percent => Total <= 0 ? 100 : (int)Math.Round(100.0 * Completed / Total);

    /// <summary>
    /// Status line such as "Rendering 12 of 150…".
    /// </summary>
    public string Message => Completed >= Total
        ? $"Rendered {Total} of {Total}"
        : $"Rendering {Completed + 1} of {Total}…";
}

/// <summary>
/// A rendered card bitmap. The caller owns it and must dispose it.
/// </summary>
public sealed class RenderedCard : IDisposable
{
    public RenderedCard(int index, CardRenderJob job, SKBitmap bitmap, IReadOnlyList<string> warnings)
    {
        Index = index;
        Job = job;
        Bitmap = bitmap;
        Warnings = warnings;
    }

    /// <summary>
    /// Zero-based position of the card in the batch.
    /// </summary>
    public int Index { get; }

    public CardRenderJob Job { get; }

    public SKBitmap Bitmap { get; }

    /// <summary>
    /// Things that printed differently than designed for this card: a missing photo, text cut short.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    public void Dispose() => Bitmap.Dispose();
}

/// <summary>
/// Outcome of exporting a batch to a document.
/// </summary>
public sealed record BatchExportResult(string OutputPath, int CardCount, IReadOnlyList<string> Warnings);
