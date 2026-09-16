using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class TextLayoutEngineTests
{
    // Monospace stand-in: every character is half the font size wide
    private static readonly TextMeasurer Mono = (text, size) => text.Length * size * 0.5f;

    private static TextLayoutResult Layout(string text, float width, float height, TextOverflowMode mode, float size = 20, float min = 5) =>
        TextLayoutEngine.Layout(text, width, height, size, min, mode, lineHeight: 1.2f, lineExtentRatio: 1.15f, Mono);

    [Fact]
    public void ShrinkToFit_TextThatFits_KeepsDesignedSize()
    {
        var result = Layout("Short", 100, 30, TextOverflowMode.ShrinkToFit);

        Assert.Equal(20f, result.FontSizePx);
        Assert.False(result.WasShrunk);
        Assert.Equal(new[] { "Short" }, result.Lines);
    }

    [Fact]
    public void ShrinkToFit_LongText_ShrinksUntilItFitsTheWidth()
    {
        // 20 chars at 20px = 200px wide; the frame is 100px, so ~10px fits
        var result = Layout("Alexandria Cartwright", 100, 30, TextOverflowMode.ShrinkToFit);

        Assert.True(result.WasShrunk);
        Assert.False(result.WasTruncated);
        Assert.True(Mono(result.Lines[0], result.FontSizePx) <= 100);
        Assert.InRange(result.FontSizePx, 9.0f, 10.0f);
    }

    [Fact]
    public void ShrinkToFit_TooLongAtMinimumSize_CutsWithEllipsis()
    {
        var result = Layout(new string('W', 200), 100, 30, TextOverflowMode.ShrinkToFit, min: 8);

        Assert.Equal(8f, result.FontSizePx);
        Assert.True(result.WasTruncated);
        Assert.EndsWith(TextLayoutEngine.Ellipsis, result.Lines[0]);
        Assert.True(Mono(result.Lines[0], 8) <= 100);
    }

    [Fact]
    public void Ellipsis_KeepsSizeAndTrimsLine()
    {
        var result = Layout("Chief Operations Officer", 100, 30, TextOverflowMode.Ellipsis);

        Assert.Equal(20f, result.FontSizePx);
        Assert.True(result.WasTruncated);
        // "Chief Ope…" is 10 characters × 10px: exactly the 100px frame, so it still fits
        Assert.Equal("Chief Ope" + TextLayoutEngine.Ellipsis, result.Lines[0]);
    }

    [Fact]
    public void Wrap_BreaksAtWordsWhenTheFrameIsTallEnough()
    {
        var result = Layout("Head of Infrastructure", 140, 60, TextOverflowMode.Wrap);

        Assert.False(result.WasShrunk);
        Assert.Equal(new[] { "Head of", "Infrastructure" }, result.Lines);
    }

    [Fact]
    public void Wrap_TooManyLines_ShrinksThenTruncatesTheLastVisibleLine()
    {
        string text = string.Join(' ', Enumerable.Repeat("word", 60));
        var result = Layout(text, 100, 30, TextOverflowMode.Wrap, min: 10);

        Assert.Equal(10f, result.FontSizePx);
        Assert.True(result.WasTruncated);
        Assert.True(TextLayoutEngine.BlockHeight(result.Lines.Count, result.FontSizePx, 1.2f, 1.15f) <= 30);
        Assert.EndsWith(TextLayoutEngine.Ellipsis, result.Lines[^1]);
    }

    [Fact]
    public void Overflow_LeavesTextUntouched()
    {
        var result = Layout("Alexandria Cartwright", 50, 10, TextOverflowMode.Overflow);

        Assert.Equal(20f, result.FontSizePx);
        Assert.Equal(new[] { "Alexandria Cartwright" }, result.Lines);
    }

    [Fact]
    public void ExplicitLineBreaks_AlwaysStartNewLines()
    {
        var result = Layout("Line one\nLine two", 400, 100, TextOverflowMode.ShrinkToFit);

        Assert.Equal(new[] { "Line one", "Line two" }, result.Lines);
    }

    [Fact]
    public void RenderedLongName_ShrinkToFit_StaysInsideItsFrame_WhileOverflowSpillsPastIt()
    {
        var format = CardFormat.CR80;
        double pxPerMm = format.WidthPixels / format.WidthMm;
        var frame = new TextLayer
        {
            Text = "{{FullName}}",
            FontSize = 14,
            X = 5,
            Y = 20,
            Width = 30,
            Height = 10
        };
        var fields = new Dictionary<string, string> { ["FullName"] = "Maximilian Alexander Featherstonehaugh" };
        var renderer = new CardRenderer();

        int InkRightOfFrame(TextOverflowMode mode)
        {
            var template = new TemplateDefinition { TargetFormat = format, Layers = { frame with { Overflow = mode } } };
            using var bitmap = renderer.RenderPreview(template, fields);
            int frameRight = (int)Math.Ceiling((frame.X + frame.Width) * pxPerMm) + 1;
            int count = 0;
            for (int x = frameRight; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    if (bitmap.GetPixel(x, y).Red < 128)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        Assert.Equal(0, InkRightOfFrame(TextOverflowMode.ShrinkToFit));
        Assert.True(InkRightOfFrame(TextOverflowMode.Overflow) > 0);
    }
}
