using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Tokens;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Renders 1D (Code128, Code39, EAN, UPC) and 2D (QR, PDF417) barcodes onto the badge canvas.
/// Module rendering is performed by directly walking the ZXing BitMatrix and drawing each module
/// as an integer-pixel SKRect without anti-aliasing to preserve sharp, scannable bar edges
/// and guarantee thermal transfer K-resin ribbon routing.
/// </summary>
public static class BarcodeRenderer
{
    /// <summary>
    /// Narrowest bar or module, in printer dots, that still scans reliably from a 300 DPI card printer.
    /// </summary>
    public const int MinModulePixels = 2;

    /// <summary>
    /// Renders a BarcodeLayer into the specified destination rectangle on the SKCanvas.
    /// In a preview (<paramref name="strict"/> false) a barcode that can't be encoded is skipped and one too big for
    /// its frame is clipped to it. When <paramref name="strict"/> is true (printing), either problem throws instead.
    /// </summary>
    /// <exception cref="InvalidOperationException">Strict rendering and the barcode can't be printed faithfully.</exception>
    public static void Render(
        SKCanvas canvas,
        BarcodeLayer layer,
        SKRect destRect,
        IReadOnlyDictionary<string, string> fieldData,
        double scaleFactorX,
        double scaleFactorY,
        bool strict = false)
    {
        // 1. Resolve dynamic content tokens
        string resolvedContent = ResolveContentToken(layer.ContentToken, fieldData);
        if (string.IsNullOrWhiteSpace(resolvedContent))
        {
            return;
        }

        // 2. Generate raw BitMatrix via ZXing MultiFormatWriter
        if (!TryEncode(layer, resolvedContent, out var matrix, out string? encodeProblem))
        {
            if (strict)
            {
                throw new InvalidOperationException(encodeProblem);
            }

            return;
        }

        // 3. Set up integer pixel bounds on the canvas
        var target = ToPixelBounds(destRect);
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }

        if (strict && GetFitProblem(layer, resolvedContent, matrix, target, scaleFactorX, scaleFactorY) is { } fitProblem)
        {
            throw new InvalidOperationException(fitProblem);
        }

        // 4. Configure pure-black paint with zero anti-aliasing
        using var paint = new SKPaint
        {
            Color = layer.IsPureBlackKResin ? new SKColor(0, 0, 0, 255) : SKColors.Black,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        // 5. Draw 2D or 1D matrix, never outside the layer frame
        canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(target.Left, target.Top, target.Right, target.Bottom));

            if (matrix.Height > 1)
            {
                Render2DMatrix(canvas, matrix, target.Left, target.Top, target.Width, target.Height, paint);
            }
            else
            {
                Render1DMatrix(canvas, matrix, target.Left, target.Top, target.Width, target.Height, layer, resolvedContent, scaleFactorY, paint);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Explains why a barcode layer can't be printed faithfully with this record's data (it can't be encoded, or it
    /// doesn't fit its frame at a scannable size), or returns null when it can. Empty values are left to pre-flight
    /// validation.
    /// </summary>
    public static string? FindPrintProblem(
        BarcodeLayer layer,
        SKRect destRect,
        IReadOnlyDictionary<string, string> fieldData,
        double scaleFactorX,
        double scaleFactorY)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(fieldData);

        string resolvedContent = ResolveContentToken(layer.ContentToken, fieldData);
        if (string.IsNullOrWhiteSpace(resolvedContent))
        {
            return null;
        }

        if (!TryEncode(layer, resolvedContent, out var matrix, out string? encodeProblem))
        {
            return encodeProblem;
        }

        return GetFitProblem(layer, resolvedContent, matrix, ToPixelBounds(destRect), scaleFactorX, scaleFactorY);
    }

    /// <summary>
    /// Walks a 2D BitMatrix (e.g. QR Code, PDF417) and fills each active module as an integer-pixel SKRect.
    /// </summary>
    public static void Render2DMatrix(
        SKCanvas canvas,
        BitMatrix matrix,
        int targetX,
        int targetY,
        int targetW,
        int targetH,
        SKPaint paint)
    {
        int matrixW = matrix.Width;
        int matrixH = matrix.Height;

        // Integer pixel module size
        int moduleSize = Math.Max(1, Math.Min(targetW / matrixW, targetH / matrixH));

        int renderedW = matrixW * moduleSize;
        int renderedH = matrixH * moduleSize;

        // Center within bounding box
        int offsetX = targetX + ((targetW - renderedW) / 2);
        int offsetY = targetY + ((targetH - renderedH) / 2);

        for (int my = 0; my < matrixH; my++)
        {
            for (int mx = 0; mx < matrixW; mx++)
            {
                if (matrix[mx, my])
                {
                    int px = offsetX + (mx * moduleSize);
                    int py = offsetY + (my * moduleSize);
                    canvas.DrawRect(new SKRect(px, py, px + moduleSize, py + moduleSize), paint);
                }
            }
        }
    }

    /// <summary>
    /// Walks a 1D BitMatrix (e.g. Code128, Code39) and fills each bar module as an integer-pixel SKRect,
    /// optionally rendering human-readable text below the bars in non-anti-aliased pure black.
    /// </summary>
    public static void Render1DMatrix(
        SKCanvas canvas,
        BitMatrix matrix,
        int targetX,
        int targetY,
        int targetW,
        int targetH,
        BarcodeLayer layer,
        string content,
        double scaleFactorY,
        SKPaint paint)
    {
        int matrixW = matrix.Width;

        // Integer module width across the horizontal axis
        int moduleWidth = Math.Max(1, targetW / matrixW);
        int renderedW = matrixW * moduleWidth;
        int offsetX = targetX + ((targetW - renderedW) / 2);

        int barHeight = targetH;
        float textFontSize = 0f;
        int textSpacing = 0;
        bool shouldDrawText = layer.IncludeText && !string.IsNullOrWhiteSpace(content);

        if (shouldDrawText)
        {
            // Reserve ~20-25% of height for text at bottom, minimum 16px
            textFontSize = Math.Clamp(targetH * 0.20f, 14f, 30f);
            textSpacing = (int)Math.Round(textFontSize * 0.25f);
            int reservedBottom = (int)Math.Ceiling(textFontSize) + textSpacing;

            if (targetH - reservedBottom >= 20)
            {
                barHeight = targetH - reservedBottom;
            }
            else
            {
                shouldDrawText = false; // Box too small for both text and bars
                barHeight = targetH;
            }
        }

        // Draw each 1D bar module as an integer-pixel SKRect
        for (int mx = 0; mx < matrixW; mx++)
        {
            if (matrix[mx, 0])
            {
                int px = offsetX + (mx * moduleWidth);
                canvas.DrawRect(new SKRect(px, targetY, px + moduleWidth, targetY + barHeight), paint);
            }
        }

        // Draw human-readable text below bars
        if (shouldDrawText)
        {
            using var textFont = new SKFont(SKTypeface.Default, textFontSize)
            {
                Edging = layer.IsPureBlackKResin ? SKFontEdging.Alias : SKFontEdging.Antialias,
                Subpixel = !layer.IsPureBlackKResin
            };

            float textY = targetY + barHeight + textSpacing + (textFontSize * 0.85f);
            float textX = targetX + (targetW / 2.0f);
            canvas.DrawText(content, textX, textY, SKTextAlign.Center, textFont, paint);
        }
    }

    /// <summary>
    /// Replaces dynamic tokens in the barcode content string with record data.
    /// </summary>
    public static string ResolveContentToken(string contentToken, IReadOnlyDictionary<string, string> fieldData)
    {
        if (string.IsNullOrWhiteSpace(contentToken))
        {
            return string.Empty;
        }

        return TokenSyntax.Replace(contentToken, fieldData);
    }

    private static bool TryEncode(
        BarcodeLayer layer,
        string content,
        [NotNullWhen(true)] out BitMatrix? matrix,
        [NotNullWhen(false)] out string? problem)
    {
        try
        {
            var hints = new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.MARGIN] = 0,
                [EncodeHintType.CHARACTER_SET] = "UTF-8"
            };

            matrix = new MultiFormatWriter().encode(content, MapSymbology(layer.Symbology), 1, 1, hints);
            if (matrix != null && matrix.Width > 0 && matrix.Height > 0)
            {
                problem = null;
                return true;
            }

            problem = $"Barcode layer '{layer.Name}' produced no barcode for '{content}'.";
        }
        catch (Exception ex)
        {
            // e.g. characters the symbology can't encode
            problem = $"Barcode layer '{layer.Name}' can't encode '{content}' as {layer.Symbology}: {ex.Message}";
        }

        matrix = null;
        return false;
    }

    private static string? GetFitProblem(
        BarcodeLayer layer,
        string content,
        BitMatrix matrix,
        SKRectI target,
        double scaleFactorX,
        double scaleFactorY)
    {
        bool is2D = matrix.Height > 1;
        int neededWidth = matrix.Width * MinModulePixels;
        int neededHeight = is2D ? matrix.Height * MinModulePixels : 1;

        if (target.Width >= neededWidth && target.Height >= neededHeight)
        {
            return null;
        }

        string needed = is2D
            ? $"{ToMm(neededWidth, scaleFactorX):0.#} × {ToMm(neededHeight, scaleFactorY):0.#} mm"
            : $"{ToMm(neededWidth, scaleFactorX):0.#} mm of width";

        return $"Barcode layer '{layer.Name}' is too small for '{content}': it needs at least {needed} to scan reliably, " +
               $"but the layer is {ToMm(target.Width, scaleFactorX):0.#} × {ToMm(target.Height, scaleFactorY):0.#} mm. " +
               "Enlarge the layer or shorten the value.";
    }

    private static SKRectI ToPixelBounds(SKRect destRect) => SKRectI.Create(
        (int)Math.Round(destRect.Left),
        (int)Math.Round(destRect.Top),
        (int)Math.Round(destRect.Width),
        (int)Math.Round(destRect.Height));

    private static double ToMm(int pixels, double pixelsPerMm) => pixelsPerMm > 0 ? pixels / pixelsPerMm : pixels;

    private static BarcodeFormat MapSymbology(BarcodeSymbology symbology) => symbology switch
    {
        BarcodeSymbology.Code128 => BarcodeFormat.CODE_128,
        BarcodeSymbology.Code39 => BarcodeFormat.CODE_39,
        BarcodeSymbology.QrCode => BarcodeFormat.QR_CODE,
        BarcodeSymbology.Ean13 => BarcodeFormat.EAN_13,
        BarcodeSymbology.UpcA => BarcodeFormat.UPC_A,
        BarcodeSymbology.Pdf417 => BarcodeFormat.PDF_417,
        _ => BarcodeFormat.CODE_128
    };
}
