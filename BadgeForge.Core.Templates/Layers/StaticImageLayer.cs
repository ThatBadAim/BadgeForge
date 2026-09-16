using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Core.Templates.Layers;

/// <summary>
/// Represents a static logo or background graphic that remains identical across all printed badges in a run.
/// </summary>
public record StaticImageLayer : TemplateLayer, IImageLayer
{
    public const string TypeDiscriminator = "StaticImage";

    public override string LayerType => TypeDiscriminator;

    /// <summary>
    /// Relative or absolute path to the local image file.
    /// </summary>
    public string? ImagePath { get; init; }

    /// <summary>
    /// Optional embedded Base64-encoded image payload (useful for self-contained template bundles).
    /// </summary>
    public string? Base64Data { get; init; }

    /// <summary>
    /// Original file name of an embedded image, shown in the designer.
    /// </summary>
    public string? SourceFileName { get; init; }

    /// <summary>
    /// How the image is sized into the layer frame (defaults to fitting the whole image).
    /// </summary>
    public PhotoCropMode CropMode { get; init; } = PhotoCropMode.AspectFit;

    /// <summary>
    /// Corner radius in millimeters for rounded image cutouts.
    /// </summary>
    public double BorderRadius { get; init; } = 0.0;

    /// <summary>
    /// Zoom, pan, rotation, flip and color adjustments applied at render time.
    /// </summary>
    public ImageAdjustments Adjustments { get; init; } = ImageAdjustments.None;

    /// <summary>
    /// Legacy toggle kept for templates saved before <see cref="CropMode"/> existed:
    /// true maps to a non-stretching mode, false maps to <see cref="PhotoCropMode.Stretch"/>.
    /// </summary>
    public bool MaintainAspectRatio
    {
        get => CropMode != PhotoCropMode.Stretch;
        init => CropMode = value
            ? (CropMode == PhotoCropMode.Stretch ? PhotoCropMode.AspectFit : CropMode)
            : PhotoCropMode.Stretch;
    }

    /// <summary>
    /// True when the layer has an image file or embedded payload assigned.
    /// </summary>
    public bool HasImage => !string.IsNullOrWhiteSpace(Base64Data) || !string.IsNullOrWhiteSpace(ImagePath);
}
