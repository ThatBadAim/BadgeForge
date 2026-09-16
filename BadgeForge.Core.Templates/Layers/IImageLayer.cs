using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Core.Templates.Layers;

/// <summary>
/// A layer that draws a bitmap into its frame and supports fit modes, rounded corners and image adjustments.
/// </summary>
public interface IImageLayer
{
    /// <summary>
    /// How the image is sized into the layer frame.
    /// </summary>
    PhotoCropMode CropMode { get; }

    /// <summary>
    /// Corner radius in millimeters for rounded image cutouts.
    /// </summary>
    double BorderRadius { get; }

    /// <summary>
    /// Zoom, pan, rotation, flip and color adjustments applied at render time.
    /// </summary>
    ImageAdjustments Adjustments { get; }
}
