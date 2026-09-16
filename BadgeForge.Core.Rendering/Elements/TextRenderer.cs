using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Tokens;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Renders dynamic and static text layers onto the badge canvas.
/// Enforces strict pure-black, non-anti-aliased text rendering via SKFont
/// (Edging = Alias, Subpixel = false) for direct routing to the K-resin ribbon panel.
/// Text that doesn't fit its frame is shrunk, wrapped or cut according to <see cref="TextLayer.Overflow"/>.
/// </summary>
public static class TextRenderer
{
    /// <summary>
    /// Renders a TextLayer into the specified destination rectangle on the SKCanvas.
    /// </summary>
    public static void Render(
        SKCanvas canvas,
        TextLayer layer,
        SKRect destRect,
        IReadOnlyDictionary<string, string> fieldData,
        double scaleFactorX,
        double scaleFactorY)
    {
        // 1. Perform token substitution
        string resolvedText = ResolveTextTokens(layer.Text, fieldData);
        if (string.IsNullOrEmpty(resolvedText))
        {
            return;
        }

        // 2. Resolve typeface — falls back to a bundled font when the requested family isn't
        // genuinely available on this machine, instead of silently rendering with whatever
        // low-quality default SkiaSharp's font manager picks.
        var typeface = ResolveTypeface(layer);

        // 3. Configure SKFont at the designed size (points -> pixels at canvas DPI)
        using var font = new SKFont(typeface, PointsToPixels(layer.FontSize, scaleFactorY));
        using var paint = new SKPaint();

        if (layer.IsPureBlackKResin)
        {
            // Strict pure-black 1-bit non-anti-aliased rendering for K-resin ribbon panel
            font.Edging = SKFontEdging.Alias;
            font.Subpixel = false;

            paint.Color = new SKColor(0, 0, 0, 255);
            paint.IsAntialias = false;
        }
        else
        {
            // Standard dye-sublimation color rendering with antialiasing
            font.Edging = SKFontEdging.Antialias;
            font.Subpixel = true;

            SKColor baseColor = SKColor.TryParse(layer.ColorHex, out var parsed) ? parsed : SKColors.Black;
            byte alpha = (byte)Math.Clamp((int)Math.Round(layer.Opacity * 255), 0, 255);
            paint.Color = baseColor.WithAlpha(alpha);
            paint.IsAntialias = true;
        }

        // 4. Fit the text to the frame (shrink / wrap / ellipsis), measured with the exact font configuration above
        var layout = LayoutWithFont(layer, resolvedText, destRect.Width, destRect.Height, scaleFactorY, font);
        font.Size = layout.FontSizePx;

        // 5. Determine horizontal alignment and X coordinate
        (SKTextAlign textAlign, float posX) = layer.Alignment switch
        {
            TextAlignment.Center => (SKTextAlign.Center, destRect.MidX),
            TextAlignment.Right => (SKTextAlign.Right, destRect.Right),
            _ => (SKTextAlign.Left, destRect.Left)
        };

        // 6. Center the block of lines vertically in the frame. For one line this is exactly the historical
        // single-line baseline, so existing designs render pixel-identically.
        font.GetFontMetrics(out var metrics);
        float advance = layout.FontSizePx * (float)layer.LineHeight;
        int lineCount = layout.Lines.Count;
        float baselineY = destRect.MidY - ((metrics.Ascent + metrics.Descent) / 2.0f) - (lineCount - 1) * advance / 2.0f;

        // 7. Draw text
        foreach (string line in layout.Lines)
        {
            if (line.Length > 0)
            {
                canvas.DrawText(line, posX, baselineY, textAlign, font, paint);
            }

            baselineY += advance;
        }
    }

    /// <summary>
    /// Works out the lines and font size a text layer draws for the given resolved text in a frame of
    /// <paramref name="boxWidthPx"/> × <paramref name="boxHeightPx"/> canvas pixels. The designer uses this to size its
    /// inline editor exactly as the badge will print.
    /// </summary>
    public static TextLayoutResult LayoutText(
        TextLayer layer,
        string resolvedText,
        float boxWidthPx,
        float boxHeightPx,
        double scaleFactorY)
    {
        ArgumentNullException.ThrowIfNull(layer);

        using var font = new SKFont(ResolveTypeface(layer), PointsToPixels(layer.FontSize, scaleFactorY));
        font.Edging = layer.IsPureBlackKResin ? SKFontEdging.Alias : SKFontEdging.Antialias;
        font.Subpixel = !layer.IsPureBlackKResin;
        return LayoutWithFont(layer, resolvedText ?? string.Empty, boxWidthPx, boxHeightPx, scaleFactorY, font);
    }

    /// <summary>
    /// The typeface a text layer renders with (a system font when genuinely installed, otherwise a bundled one).
    /// The returned typeface is cached and shared: do not dispose it.
    /// </summary>
    public static SKTypeface ResolveTypeface(TextLayer layer) =>
        BundledFonts.Resolve(layer.FontFamily, ParseFontWeight(layer.FontWeight), SKFontStyleSlant.Upright);

    /// <summary>
    /// Converts a size in points to canvas pixels. 1 pt = 1/72 inch; canvas DPI = pixels-per-mm × 25.4.
    /// </summary>
    public static float PointsToPixels(double points, double scaleFactorY) => (float)(points * (scaleFactorY * 25.4 / 72.0));

    /// <summary>
    /// Replaces dynamic tokens such as "{{FirstName}}" (or legacy "{FirstName}") with matching field values,
    /// sanitized for printing. Unmapped tokens are left as written.
    /// </summary>
    public static string ResolveTextTokens(string templateText, IReadOnlyDictionary<string, string> fieldData) =>
        TokenSyntax.Replace(templateText, fieldData, sanitize: true);

    private static TextLayoutResult LayoutWithFont(
        TextLayer layer,
        string resolvedText,
        float boxWidthPx,
        float boxHeightPx,
        double scaleFactorY,
        SKFont font)
    {
        float nominalPx = PointsToPixels(layer.FontSize, scaleFactorY);
        float minPx = PointsToPixels(layer.MinFontSize, scaleFactorY);
        float originalSize = font.Size;

        font.Size = nominalPx;
        font.GetFontMetrics(out var metrics);
        float extentRatio = nominalPx > 0 ? (metrics.Descent - metrics.Ascent) / nominalPx : 1.2f;

        try
        {
            return TextLayoutEngine.Layout(
                resolvedText,
                boxWidthPx,
                boxHeightPx,
                nominalPx,
                minPx,
                layer.Overflow,
                (float)layer.LineHeight,
                extentRatio,
                (text, sizePx) =>
                {
                    font.Size = sizePx;
                    return font.MeasureText(text);
                });
        }
        finally
        {
            font.Size = originalSize;
        }
    }

    private static SKFontStyleWeight ParseFontWeight(string? weightName)
    {
        if (string.IsNullOrWhiteSpace(weightName))
        {
            return SKFontStyleWeight.Normal;
        }

        return weightName.ToLowerInvariant() switch
        {
            "thin" or "100" => SKFontStyleWeight.Thin,
            "extralight" or "ultralight" or "200" => SKFontStyleWeight.ExtraLight,
            "light" or "300" => SKFontStyleWeight.Light,
            "normal" or "regular" or "400" => SKFontStyleWeight.Normal,
            "medium" or "500" => SKFontStyleWeight.Medium,
            "semibold" or "demibold" or "600" => SKFontStyleWeight.SemiBold,
            "bold" or "700" => SKFontStyleWeight.Bold,
            "extrabold" or "ultrabold" or "800" => SKFontStyleWeight.ExtraBold,
            "black" or "heavy" or "900" => SKFontStyleWeight.Black,
            _ => SKFontStyleWeight.Normal
        };
    }
}
