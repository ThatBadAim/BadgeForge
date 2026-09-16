using BadgeForge.Core.Templates.Layers;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Renders static logo or graphic elements onto the badge canvas.
/// Supports file path references and embedded Base64 payloads, plus crop mode and image adjustments.
/// </summary>
public static class StaticImageRenderer
{
    /// <summary>
    /// Renders a StaticImageLayer into the specified destination rectangle on the SKCanvas.
    /// </summary>
    public static void Render(
        SKCanvas canvas,
        StaticImageLayer layer,
        SKRect destRect,
        double scaleFactorX = 1.0,
        double scaleFactorY = 1.0)
    {
        // Cached, shared bitmap — do not dispose
        var bitmap = ImageSourceLoader.TryLoad(layer.ImagePath) ?? ImageSourceLoader.TryLoad(layer.Base64Data);
        if (bitmap == null)
        {
            return;
        }

        float radiusPx = (float)(layer.BorderRadius * ((scaleFactorX + scaleFactorY) / 2.0));
        ImageDrawing.Draw(canvas, bitmap, destRect, layer.CropMode, layer.Adjustments, radiusPx, layer.Opacity);
    }
}
