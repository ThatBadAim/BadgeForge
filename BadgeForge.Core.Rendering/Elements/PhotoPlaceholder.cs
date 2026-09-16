using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// The stand-in graphic printed in a photo frame when a record has no usable photo and the layer has no fallback
/// image: a neutral head-and-shoulders silhouette, so a missing photo reads as intentional rather than as a
/// rendering fault.
/// </summary>
public static class PhotoPlaceholder
{
    private static readonly SKColor Background = new(0xE3, 0xE8, 0xE6);
    private static readonly SKColor Silhouette = new(0xAE, 0xBA, 0xB6);

    /// <summary>
    /// Draws the placeholder into <paramref name="frame"/>, clipped to its rounded corners.
    /// </summary>
    public static void Draw(SKCanvas canvas, SKRect frame, float cornerRadiusPx, double opacity = 1.0)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (frame.Width <= 0 || frame.Height <= 0 || opacity <= 0.0)
        {
            return;
        }

        byte alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);

        canvas.Save();
        try
        {
            using (var clip = new SKRoundRect(frame, Math.Max(0, cornerRadiusPx), Math.Max(0, cornerRadiusPx)))
            {
                canvas.ClipRoundRect(clip, SKClipOperation.Intersect, antialias: true);
            }

            using var backgroundPaint = new SKPaint { Color = Background.WithAlpha(alpha), IsAntialias = true };
            canvas.DrawRect(frame, backgroundPaint);

            using var silhouettePaint = new SKPaint { Color = Silhouette.WithAlpha(alpha), IsAntialias = true };
            float size = Math.Min(frame.Width, frame.Height);
            float centerX = frame.MidX;
            float headRadius = size * 0.19f;
            float headCenterY = frame.Top + frame.Height * 0.40f;
            canvas.DrawCircle(centerX, headCenterY, headRadius, silhouettePaint);

            // Shoulders: a wide oval starting just below the head and running off the bottom of the frame
            float shouldersTop = headCenterY + headRadius * 1.25f;
            canvas.DrawOval(new SKRect(centerX - size * 0.42f, shouldersTop, centerX + size * 0.42f, shouldersTop + size * 0.8f), silhouettePaint);
        }
        finally
        {
            canvas.Restore();
        }
    }
}
