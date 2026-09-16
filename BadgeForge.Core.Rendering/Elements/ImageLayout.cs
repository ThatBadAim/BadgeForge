using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Unit-agnostic geometry for placing an image inside a layer frame.
/// Works equally in canvas pixels (rendering) and millimeters (designer interaction).
/// </summary>
public static class ImageLayout
{
    /// <summary>
    /// Image dimensions after rotation (90° and 270° swap width and height).
    /// </summary>
    public static (float Width, float Height) GetOrientedSize(float width, float height, ImageAdjustments adjustments)
    {
        return adjustments.NormalizedRotation is 90 or 270 ? (height, width) : (width, height);
    }

    /// <summary>
    /// Computes the rectangle the (rotated) image occupies before it is clipped to the frame.
    /// Zoom scales around the crop mode's base size; offsets pan within the overflow so the
    /// image edge can never be dragged past the frame edge.
    /// </summary>
    public static SKRect ComputeContentRect(float srcW, float srcH, SKRect frame, PhotoCropMode mode, ImageAdjustments adjustments)
    {
        if (srcW <= 0 || srcH <= 0 || frame.Width <= 0 || frame.Height <= 0)
        {
            return frame;
        }

        var (orientedW, orientedH) = GetOrientedSize(srcW, srcH, adjustments);
        float zoom = (float)Math.Clamp(adjustments.Zoom, ImageAdjustments.MinZoom, ImageAdjustments.MaxZoom);

        float w;
        float h;
        switch (mode)
        {
            case PhotoCropMode.Stretch:
                w = frame.Width * zoom;
                h = frame.Height * zoom;
                break;

            case PhotoCropMode.AspectFit:
            {
                float scale = Math.Min(frame.Width / orientedW, frame.Height / orientedH);
                w = orientedW * scale * zoom;
                h = orientedH * scale * zoom;
                break;
            }

            case PhotoCropMode.AspectFill:
            default:
            {
                float scale = Math.Max(frame.Width / orientedW, frame.Height / orientedH);
                w = orientedW * scale * zoom;
                h = orientedH * scale * zoom;
                break;
            }
        }

        float panX = (float)Math.Clamp(adjustments.OffsetX, -1.0, 1.0) * Math.Abs(w - frame.Width) / 2.0f;
        float panY = (float)Math.Clamp(adjustments.OffsetY, -1.0, 1.0) * Math.Abs(h - frame.Height) / 2.0f;

        float left = frame.Left + (frame.Width - w) / 2.0f + panX;
        float top = frame.Top + (frame.Height - h) / 2.0f + panY;
        return new SKRect(left, top, left + w, top + h);
    }

    /// <summary>
    /// Returns adjustments panned by a frame-space delta (same units as the frame size).
    /// Axes without overflow are left unchanged.
    /// </summary>
    public static ImageAdjustments Pan(
        float srcW, float srcH, float frameW, float frameH,
        PhotoCropMode mode, ImageAdjustments adjustments, double deltaX, double deltaY)
    {
        var centered = ComputeContentRect(srcW, srcH, new SKRect(0, 0, frameW, frameH), mode,
            adjustments with { OffsetX = 0, OffsetY = 0 });

        double overflowX = Math.Abs(centered.Width - frameW) / 2.0;
        double overflowY = Math.Abs(centered.Height - frameH) / 2.0;

        return adjustments with
        {
            OffsetX = overflowX > 1e-6 ? Math.Clamp(adjustments.OffsetX + deltaX / overflowX, -1.0, 1.0) : adjustments.OffsetX,
            OffsetY = overflowY > 1e-6 ? Math.Clamp(adjustments.OffsetY + deltaY / overflowY, -1.0, 1.0) : adjustments.OffsetY
        };
    }

    /// <summary>
    /// The inverse of <see cref="ComputeContentRect"/>: the zoom and pan that put the image at
    /// <paramref name="content"/> inside <paramref name="frame"/>, as closely as the zoom range allows.
    /// Used by the crop box, where resizing the frame must leave the picture where it already sits on the card so
    /// that dragging an edge reveals or hides image, instead of rescaling what is shown.
    /// </summary>
    public static ImageAdjustments FitContentRect(
        float srcW, float srcH, SKRect frame, PhotoCropMode mode, ImageAdjustments adjustments, SKRect content)
    {
        if (srcW <= 0 || srcH <= 0 || frame.Width <= 0 || frame.Height <= 0 || content.Width <= 0 || content.Height <= 0)
        {
            return adjustments;
        }

        // Size the image takes in this frame at zoom 1: the baseline both zoom and pan are measured against
        var baseRect = ComputeContentRect(srcW, srcH, frame, mode, adjustments with { Zoom = 1.0, OffsetX = 0, OffsetY = 0 });
        if (baseRect.Width <= 0 || baseRect.Height <= 0)
        {
            return adjustments;
        }

        // Aspect-preserving modes scale both axes together, so the two ratios agree. Stretch can disagree; the
        // larger ratio is taken so the frame never ends up with a gap the crop box didn't ask for.
        double zoom = Math.Max(content.Width / baseRect.Width, content.Height / baseRect.Height);
        zoom = Math.Clamp(zoom, ImageAdjustments.MinZoom, ImageAdjustments.MaxZoom);

        float w = (float)(baseRect.Width * zoom);
        float h = (float)(baseRect.Height * zoom);
        double overflowX = Math.Abs(w - frame.Width) / 2.0;
        double overflowY = Math.Abs(h - frame.Height) / 2.0;

        // Pan is the gap between the image's centre and the frame's, as a fraction of how far it can travel
        double panX = (content.MidX - frame.MidX);
        double panY = (content.MidY - frame.MidY);

        return adjustments with
        {
            Zoom = zoom,
            OffsetX = overflowX > 1e-6 ? Math.Clamp(panX / overflowX, -1.0, 1.0) : 0.0,
            OffsetY = overflowY > 1e-6 ? Math.Clamp(panY / overflowY, -1.0, 1.0) : 0.0
        };
    }

    /// <summary>
    /// Computes the centered source cropping rectangle for AspectFill (cover mode).
    /// Prevents aspect ratio distortion by cropping excess symmetrically.
    /// </summary>
    public static SKRect ComputeAspectFillCrop(int srcW, int srcH, float destW, float destH)
    {
        if (srcW <= 0 || srcH <= 0 || destW <= 0 || destH <= 0)
        {
            return new SKRect(0, 0, srcW, srcH);
        }

        float targetAspect = destW / destH;
        float srcAspect = (float)srcW / srcH;

        if (srcAspect > targetAspect)
        {
            // Source is wider than target aspect ratio - crop horizontal sides symmetrically
            float cropH = srcH;
            float cropW = cropH * targetAspect;
            float cropX = (srcW - cropW) / 2.0f;
            return new SKRect(cropX, 0, cropX + cropW, cropH);
        }
        else
        {
            // Source is taller than target aspect ratio - crop top and bottom symmetrically
            float cropW = srcW;
            float cropH = cropW / targetAspect;
            float cropY = (srcH - cropH) / 2.0f;
            return new SKRect(0, cropY, cropW, cropY + cropH);
        }
    }
}
