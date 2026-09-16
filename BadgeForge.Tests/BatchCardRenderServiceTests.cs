using System.Text;
using System.Text.RegularExpressions;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering.Batch;
using BadgeForge.Core.Rendering.Output;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class BatchCardRenderServiceTests
{
    private static TemplateDefinition CreateTemplate() => new()
    {
        Name = "Staff",
        TargetFormat = CardFormat.CR80,
        Layers =
        {
            new TextLayer { Name = "Name", Text = "{{FullName}}", FontSize = 12, X = 30, Y = 10, Width = 50, Height = 8, ZIndex = 2 },
            new TextLayer { Name = "Title", Text = "{{JobTitle}}", FontSize = 9, X = 30, Y = 20, Width = 50, Height = 6, ZIndex = 3 },
            new PhotoLayer { Name = "Photo", SourceToken = "{{Photo}}", X = 4, Y = 8, Width = 22, Height = 30, IsRequired = false, ZIndex = 1 }
        }
    };

    private static CardRenderJob Job(int n, string? photo = null)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FullName"] = $"Person {n}",
            ["JobTitle"] = "Engineer"
        };
        if (photo != null)
        {
            fields["Photo"] = photo;
        }
        return new CardRenderJob(n.ToString(), $"Person {n}", fields);
    }

    private static async Task<List<RenderedCard>> RenderAll(
        TemplateDefinition template, IReadOnlyList<CardRenderJob> jobs, BatchRenderOptions? options = null, IProgress<BatchRenderProgress>? progress = null)
    {
        var cards = new List<RenderedCard>();
        await foreach (var card in new BatchCardRenderService().RenderAsync(template, jobs, options, progress))
        {
            cards.Add(card);
        }
        return cards;
    }

    [Fact]
    public async Task RenderAsync_YieldsCardsInOrder_AtCr80300Dpi()
    {
        var jobs = Enumerable.Range(1, 9).Select(n => Job(n)).ToList();

        var cards = await RenderAll(CreateTemplate(), jobs, new BatchRenderOptions { MaxDegreeOfParallelism = 4 });
        try
        {
            Assert.Equal(jobs.Select(j => j.Id), cards.Select(c => c.Job.Id));
            Assert.Equal(Enumerable.Range(0, 9), cards.Select(c => c.Index));
            Assert.All(cards, c => Assert.Equal((1011, 638), (c.Bitmap.Width, c.Bitmap.Height)));
        }
        finally
        {
            cards.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public async Task RenderAsync_ReportsRenderingNOfTotalProgress()
    {
        var reports = new List<BatchRenderProgress>();
        var progress = new SynchronousProgress<BatchRenderProgress>(reports.Add);

        var cards = await RenderAll(CreateTemplate(), Enumerable.Range(1, 3).Select(n => Job(n)).ToList(), progress: progress);
        cards.ForEach(c => c.Dispose());

        Assert.Equal(new[] { 0, 1, 2, 3 }, reports.Select(r => r.Completed));
        Assert.Equal("Rendering 1 of 3…", reports[0].Message);
        Assert.Equal("Rendering 3 of 3…", reports[2].Message);
        Assert.Equal(100, reports[^1].Percent);
    }

    [Fact]
    public async Task RenderAsync_MissingPhoto_DrawsPlaceholderAndWarns()
    {
        var cards = await RenderAll(CreateTemplate(), new[] { Job(1, photo: "/no/such/photo.jpg"), Job(2) });
        try
        {
            Assert.All(cards, c => Assert.Contains(c.Warnings, w => w.Contains("photo", StringComparison.OrdinalIgnoreCase)));

            // Centre of the photo frame is the silhouette's head, not blank white card
            double pxPerMm = 1011 / 85.6;
            var center = cards[0].Bitmap.GetPixel((int)(15 * pxPerMm), (int)(20 * pxPerMm));
            Assert.NotEqual(SKColors.White, center);
        }
        finally
        {
            cards.ForEach(c => c.Dispose());
        }

        var blank = await RenderAll(CreateTemplate(), new[] { Job(1) }, new BatchRenderOptions { DrawMissingPhotoPlaceholders = false });
        double scale = 1011 / 85.6;
        Assert.Equal(SKColors.White, blank[0].Bitmap.GetPixel((int)(15 * scale), (int)(20 * scale)));
        blank.ForEach(c => c.Dispose());
    }

    [Fact]
    public void Bind_SubstitutesSanitizedValues_WithoutTouchingTheSourceTemplate()
    {
        var template = CreateTemplate();
        var fields = new Dictionary<string, string>
        {
            ["FullName"] = "  Ada\r\nLovelace {{Photo}} ",
            ["JobTitle"] = "Analyst",
            ["Photo"] = "/photos/ada.jpg"
        };

        var bound = TemplateBinder.Bind(template, fields);

        var name = Assert.IsType<TextLayer>(bound.Template.Layers[0]);
        Assert.Equal("Ada Lovelace {{Photo}}", name.Text);
        Assert.Equal("{{FullName}}", ((TextLayer)template.Layers[0]).Text);
        Assert.NotSame(template.Layers, bound.Template.Layers);
        Assert.Equal("/photos/ada.jpg", Assert.Single(bound.PhotoSources).Source);

        // The literal "{{Photo}}" in the name must not be resolvable from the render-time lookup
        Assert.DoesNotContain("Photo", bound.RenderFields.Keys);
    }

    [Fact]
    public async Task RenderAsync_Cancellation_StopsTheRun()
    {
        using var cts = new CancellationTokenSource();
        var jobs = Enumerable.Range(1, 50).Select(n => Job(n)).ToList();
        int received = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var card in new BatchCardRenderService().RenderAsync(CreateTemplate(), jobs, ct: cts.Token))
            {
                card.Dispose();
                if (++received == 2)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(2, received);
    }

    [Fact]
    public async Task RenderAsync_BelowMinimumDpi_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            RenderAll(CreateTemplate(), new[] { Job(1) }, new BatchRenderOptions { Dpi = 150 }));
    }

    [Fact]
    public async Task PdfExport_WritesOneCardSizedPagePerRecord()
    {
        string path = Path.Combine(Path.GetTempPath(), $"badges-{Guid.NewGuid():N}.pdf");
        try
        {
            var result = await new BatchPdfExporter().ExportAsync(
                CreateTemplate(), Enumerable.Range(1, 3).Select(n => Job(n)).ToList(), path);

            Assert.Equal(3, result.CardCount);
            string pdf = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(path));
            Assert.StartsWith("%PDF-", pdf);
            Assert.Equal(3, Regex.Matches(pdf, @"/Type\s*/Page\b").Count);

            // 85.60 mm x 53.98 mm in points
            var mediaBox = Regex.Match(pdf, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
            Assert.True(mediaBox.Success);
            Assert.Equal(242.65, double.Parse(mediaBox.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1);
            Assert.Equal(153.01, double.Parse(mediaBox.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 1);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PdfExport_Cancelled_LeavesNoFileBehind()
    {
        string path = Path.Combine(Path.GetTempPath(), $"badges-{Guid.NewGuid():N}.pdf");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BatchPdfExporter().ExportAsync(
            CreateTemplate(), new[] { Job(1) }, path, ct: cts.Token));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
    }

    /// <summary>
    /// Progress that reports inline, so assertions see every report in order.
    /// </summary>
    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
