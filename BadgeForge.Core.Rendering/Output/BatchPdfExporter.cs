using BadgeForge.Core.Rendering.Batch;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Core.Rendering.Output;

/// <summary>
/// Renders a batch of cards and spools them into a single multi-page PDF.
/// </summary>
public sealed class BatchPdfExporter
{
    private readonly IBatchCardRenderService _renderService;

    public BatchPdfExporter(IBatchCardRenderService? renderService = null)
    {
        _renderService = renderService ?? new BatchCardRenderService();
    }

    /// <summary>
    /// Writes one page per job to <paramref name="outputPath"/>. The file only appears once the whole document has been
    /// written, so a cancelled or failed export never leaves a truncated PDF behind (or replaces an existing one).
    /// </summary>
    public async Task<BatchExportResult> ExportAsync(
        TemplateDefinition template,
        IReadOnlyList<CardRenderJob> jobs,
        string outputPath,
        BatchRenderOptions? options = null,
        IProgress<BatchRenderProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (jobs.Count == 0)
        {
            throw new ArgumentException("There are no cards to export.", nameof(jobs));
        }

        options ??= new BatchRenderOptions();
        string fullPath = Path.GetFullPath(outputPath);
        string tempPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        var warnings = new List<string>();

        try
        {
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                int pages;
                using (var writer = new PdfCardDocumentWriter(file, template.TargetFormat, template.Name))
                {
                    await foreach (var card in _renderService.RenderAsync(template, jobs, options, progress, ct).ConfigureAwait(false))
                    {
                        using (card)
                        {
                            warnings.AddRange(card.Warnings);
                            // Page compression is CPU work too; keep it off the caller's thread
                            await Task.Run(() => writer.AddCard(card.Bitmap), ct).ConfigureAwait(false);
                        }
                    }

                    writer.Close();
                    pages = writer.PageCount;
                }

                await file.FlushAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                File.Move(tempPath, fullPath, overwrite: true);
                return new BatchExportResult(fullPath, pages, warnings);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // Best effort: a leftover hidden temp file is harmless
                }
            }
        }
    }
}
