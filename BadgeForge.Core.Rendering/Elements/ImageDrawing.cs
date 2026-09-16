using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Shared drawing routine for photo and static image layers: crop mode, zoom/pan,
/// quarter-turn rotation, flips, rounded corners, opacity and color correction.
/// </summary>
public static class ImageDrawing
{
    // Rec. 709 luma weights
    private const float LumaR = 0.2126f;
    private const float LumaG = 0.7152f;
    private const float LumaB = 0.0722f;

    public static void Draw(
        SKCanvas canvas,
        SKBitmap bitmap,
        SKRect frame,
        PhotoCropMode mode,
        ImageAdjustments adjustments,
        float cornerRadiusPx,
        double opacity)
    {
        if (bitmap.Width <= 0 || bitmap.Height <= 0 || frame.Width <= 0 || frame.Height <= 0 || opacity <= 0.0)
        {
            return;
        }

        var content = ImageLayout.ComputeContentRect(bitmap.Width, bitmap.Height, frame, mode, adjustments);

        // A fitted image that is smaller than its frame gets rounded corners on the visible image, not the empty frame
        var clip = mode == PhotoCropMode.AspectFit ? SKRect.Intersect(content, frame) : frame;
        if (clip.IsEmpty)
        {
            return;
        }

        canvas.Save();
        try
        {
            if (cornerRadiusPx > 0.0f)
            {
                using var roundRect = new SKRoundRect(clip, cornerRadiusPx, cornerRadiusPx);
                canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias: true);
            }
            else
            {
                canvas.ClipRect(clip, SKClipOperation.Intersect, antialias: false);
            }

            using var colorFilter = adjustments.HasColorAdjustments ? CreateColorFilter(adjustments) : null;
            using var paint = new SKPaint
            {
                IsAntialias = true,
                ColorFilter = colorFilter
            };

            if (opacity < 1.0)
            {
                byte alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
                paint.Color = paint.Color.WithAlpha(alpha);
            }

            var sampling = new SKSamplingOptions(SKFilterMode.Linear);
            var fullSource = new SKRect(0, 0, bitmap.Width, bitmap.Height);
            int rotation = adjustments.NormalizedRotation;

            if (rotation == 0 && !adjustments.FlipHorizontal && !adjustments.FlipVertical)
            {
                if (mode == PhotoCropMode.AspectFill && adjustments.IsIdentityTransform)
                {
                    // Exact source crop keeps default cover-mode output stable (golden image tests)
                    var srcRect = ImageLayout.ComputeAspectFillCrop(bitmap.Width, bitmap.Height, frame.Width, frame.Height);
                    canvas.DrawBitmap(bitmap, srcRect, frame, sampling, paint);
                }
                else
                {
                    canvas.DrawBitmap(bitmap, fullSource, content, sampling, paint);
                }

                return;
            }

            // Rotate and mirror around the content center; the unrotated draw size swaps for quarter turns
            bool swap = rotation is 90 or 270;
            float drawW = swap ? content.Height : content.Width;
            float drawH = swap ? content.Width : content.Height;

            canvas.Translate(content.MidX, content.MidY);
            canvas.Scale(adjustments.FlipHorizontal ? -1 : 1, adjustments.FlipVertical ? -1 : 1);
            canvas.RotateDegrees(rotation);
            canvas.DrawBitmap(bitmap, fullSource, new SKRect(-drawW / 2, -drawH / 2, drawW / 2, drawH / 2), sampling, paint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Builds a single color matrix for saturation, then contrast (around mid-grey), then brightness.
    /// </summary>
    public static SKColorFilter CreateColorFilter(ImageAdjustments adjustments)
    {
        float s = 1f + (float)Math.Clamp(adjustments.Saturation, -1.0, 1.0);
        float c = 1f + (float)Math.Clamp(adjustments.Contrast, -1.0, 1.0);
        float translate = 0.5f * (1f - c) + (float)Math.Clamp(adjustments.Brightness, -1.0, 1.0);

        float sr = (1f - s) * LumaR;
        float sg = (1f - s) * LumaG;
        float sb = (1f - s) * LumaB;

        // Translation column is normalized (0–1) in this SkiaSharp version, verified by ImageAdjustmentTests
        return SKColorFilter.CreateColorMatrix(new[]
        {
            c * (sr + s), c * sg,       c * sb,       0f, translate,
            c * sr,       c * (sg + s), c * sb,       0f, translate,
            c * sr,       c * sg,       c * (sb + s), 0f, translate,
            0f,           0f,           0f,           1f, 0f
        });
    }
}
