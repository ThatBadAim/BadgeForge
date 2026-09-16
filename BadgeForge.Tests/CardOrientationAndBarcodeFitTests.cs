using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using Xunit;

namespace BadgeForge.Tests;

public class CardOrientationAndBarcodeFitTests
{
    private readonly CardRenderer _renderer = new();

    [Fact]
    public async Task PortraitTemplate_RendersOnThePrinterCanvasTurnedToPortrait()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var template = new TemplateDefinition { TargetFormat = CardFormat.CR80.WithOrientation(true) };

        using var bitmap = await _renderer.RenderCardAsync(template, new Dictionary<string, string>(), mockDriver);

        Assert.Equal(638, bitmap.Width);
        Assert.Equal(1011, bitmap.Height);
    }

    [Fact]
    public async Task CardSizeThePrinterDoesNotTake_StillFailsLoudlyWhenPrinting()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var template = new TemplateDefinition { TargetFormat = CardFormat.CR79 };

        await Assert.ThrowsAsync<PrinterCapabilityMismatchException>(() =>
            _renderer.RenderCardAsync(template, new Dictionary<string, string>(), mockDriver));
    }

    [Fact]
    public void Preview_DrawsAnyCardSize_WithoutAPrinter()
    {
        using var bitmap = _renderer.RenderPreview(new TemplateDefinition { TargetFormat = CardFormat.CR79 }, new Dictionary<string, string>());

        Assert.Equal(CardFormat.CR79.WidthPixels, bitmap.Width);
        Assert.Equal(CardFormat.CR79.HeightPixels, bitmap.Height);
    }

    [Fact]
    public async Task BarcodeTooLongForItsLayer_StaysInsideTheLayerInPreviews_AndIsRejectedForPrinting()
    {
        var template = new TemplateDefinition
        {
            Layers =
            {
                new BarcodeLayer
                {
                    Name = "Badge Barcode",
                    ContentToken = "{Id}",
                    Symbology = BarcodeSymbology.Code128,
                    IncludeText = false,
                    X = 10,
                    Y = 10,
                    Width = 45,
                    Height = 8
                }
            }
        };
        var fields = new Dictionary<string, string> { ["Id"] = string.Concat(Enumerable.Repeat("AbCd", 12)) };
        using var mockDriver = new MockCardPrinterDriver();

        using (var preview = await _renderer.RenderCardAsync(template, fields, mockDriver))
        {
            double pixelsPerMm = preview.Width / CardFormat.CR80.WidthMm;
            int layerLeft = (int)Math.Round(10 * pixelsPerMm);
            int layerRight = (int)Math.Round(55 * pixelsPerMm);

            int blackOutsideLayer = 0;
            for (int y = 0; y < preview.Height; y++)
            {
                for (int x = 0; x < preview.Width; x++)
                {
                    var pixel = preview.GetPixel(x, y);
                    if ((x < layerLeft || x > layerRight) && pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0)
                    {
                        blackOutsideLayer++;
                    }
                }
            }

            Assert.Equal(0, blackOutsideLayer);
        }

        var printRenderer = new CardRenderer { StrictLayerRendering = true };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => printRenderer.RenderCardAsync(template, fields, mockDriver));
        Assert.Contains("Badge Barcode", ex.Message);

        var capabilities = await mockDriver.ProbeCapabilitiesAsync("Mock-SMART-31");
        Assert.Single(printRenderer.FindPrintProblems(template, fields, capabilities));
        Assert.Empty(printRenderer.FindPrintProblems(template, new Dictionary<string, string> { ["Id"] = "E-1" }, capabilities));
    }
}
