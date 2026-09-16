using System.Runtime.CompilerServices;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Core.Rendering.Batch;

/// <summary>
/// Renders one card per record without touching the UI thread.
/// </summary>
public interface IBatchCardRenderService
{
    /// <summary>
    /// Renders every job, yielding cards in job order as they finish. The template is snapshotted once up front, so
    /// editing it meanwhile doesn't affect the run. Dispose each card when done with it.
    /// </summary>
    IAsyncEnumerable<RenderedCard> RenderAsync(
        TemplateDefinition template,
        IReadOnlyList<CardRenderJob> jobs,
        BatchRenderOptions? options = null,
        IProgress<BatchRenderProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Off-screen batch renderer. Lifecycle per run:
/// <list type="number">
/// <item>Snapshot the template once and size the canvas for the card format at the requested DPI.</item>
/// <item>For each record, on a worker thread: bind a private copy of the template to the record's sanitized values,
/// decode and scale its photo (through the shared image cache), then draw the card.</item>
/// <item>Hand finished cards back strictly in order, keeping at most <see cref="BatchRenderOptions.MaxDegreeOfParallelism"/>
/// cards in flight so a 1,000-card run never holds 1,000 bitmaps.</item>
/// </list>
/// </summary>
public sealed class BatchCardRenderService : IBatchCardRenderService
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<RenderedCard> RenderAsync(
        TemplateDefinition template,
        IReadOnlyList<CardRenderJob> jobs,
        BatchRenderOptions? options = null,
        IProgress<BatchRenderProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(jobs);
        options ??= new BatchRenderOptions();
        if (options.Dpi < BatchRenderOptions.MinimumDpi)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Dpi,
                $"Batch output needs at least {BatchRenderOptions.MinimumDpi} DPI.");
        }

        // Snapshot once, outside the loop: the per-card work only ever reads this private copy
        var snapshot = template with
        {
            Layers = new List<TemplateLayer>(template.Layers),
            Metadata = new Dictionary<string, string>(template.Metadata)
        };
        var jobList = jobs.ToList();
        var (canvasWidth, canvasHeight) = GetCanvasSize(snapshot.TargetFormat, options.Dpi);
        var renderer = new CardRenderer
        {
            StrictLayerRendering = options.StrictLayerRendering,
            DrawMissingPhotoPlaceholders = options.DrawMissingPhotoPlaceholders
        };

        int total = jobList.Count;
        int parallelism = Math.Max(1, options.MaxDegreeOfParallelism);
        var inFlight = new Queue<Task<RenderedCard>>(parallelism);
        int nextToStart = 0;
        int completed = 0;

        progress?.Report(new BatchRenderProgress(0, total, total > 0 ? jobList[0].Label : null));

        try
        {
            while (completed < total)
            {
                ct.ThrowIfCancellationRequested();

                // Keep the look-ahead window full so workers stay busy while the consumer handles the next card
                while (inFlight.Count < parallelism && nextToStart < total)
                {
                    int index = nextToStart++;
                    inFlight.Enqueue(Task.Run(
                        () => RenderCard(renderer, snapshot, jobList[index], index, canvasWidth, canvasHeight, ct), ct));
                }

                var card = await inFlight.Dequeue().ConfigureAwait(false);
                completed++;
                progress?.Report(new BatchRenderProgress(completed, total, completed < total ? jobList[completed].Label : null));
                yield return card;
            }
        }
        finally
        {
            // Cancelled, failed, or the consumer stopped early: free cards rendered ahead that were never handed out
            while (inFlight.Count > 0)
            {
                var pending = inFlight.Dequeue();
                try
                {
                    (await pending.ConfigureAwait(false)).Dispose();
                }
                catch
                {
                    // Already reported through the card that failed first
                }
            }
        }
    }

    /// <summary>
    /// Canvas pixel size of a card format at a resolution.
    /// </summary>
    public static (int Width, int Height) GetCanvasSize(CardFormat format, int dpi) =>
        ((int)Math.Round(format.WidthMm / 25.4 * dpi), (int)Math.Round(format.HeightMm / 25.4 * dpi));

    private static RenderedCard RenderCard(
        CardRenderer renderer,
        TemplateDefinition snapshot,
        CardRenderJob job,
        int index,
        int canvasWidth,
        int canvasHeight,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var bound = TemplateBinder.Bind(snapshot, job.Fields);
            var warnings = new List<string>();

            // Resolve photos first: decoding and downscaling happen here on the worker, and the decoded bitmaps stay
            // cached (and leased) for the draw below
            using (ImageSourceLoader.AcquireLease())
            {
                foreach (var (layer, source) in bound.PhotoSources)
                {
                    if (ImageSourceLoader.TryLoad(source) == null && ImageSourceLoader.TryLoad(layer.FallbackImagePath) == null)
                    {
                        warnings.Add(source == null
                            ? $"{job.Label}: no photo for '{layer.Name}'."
                            : $"{job.Label}: the photo for '{layer.Name}' couldn't be read ({Path.GetFileName(source)}).");
                    }
                }

                var bitmap = renderer.RenderAtSize(bound.Template, bound.RenderFields, canvasWidth, canvasHeight, ct);
                warnings.AddRange(FindCutText(bound.Template, canvasWidth, canvasHeight, job.Label));
                return new RenderedCard(index, job, bitmap, warnings);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Card {index + 1} ({job.Label}) couldn't be rendered: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Text layers whose value was too long for the frame even after shrinking, so it was cut with an ellipsis.
    /// </summary>
    private static IEnumerable<string> FindCutText(TemplateDefinition bound, int canvasWidth, int canvasHeight, string label)
    {
        double scaleX = canvasWidth / bound.TargetFormat.WidthMm;
        double scaleY = canvasHeight / bound.TargetFormat.HeightMm;

        foreach (var text in bound.Layers.OfType<TextLayer>().Where(l => l.IsVisible && l.Text.Length > 0))
        {
            var layout = TextRenderer.LayoutText(text, text.Text, (float)(text.Width * scaleX), (float)(text.Height * scaleY), scaleY);
            if (layout.WasTruncated)
            {
                yield return $"{label}: '{text.Name}' was too long and was cut short.";
            }
        }
    }
}
