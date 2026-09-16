using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Core.Templates.Layers;

/// <summary>
/// Represents a dynamic photo element (such as an employee or attendee portrait) bound to a record key/token.
/// </summary>
public record PhotoLayer : TemplateLayer, IImageLayer
{
    public const string TypeDiscriminator = "Photo";

    public override string LayerType => TypeDiscriminator;

    /// <summary>
    /// Token representing the photo reference (e.g. "{Photo}" or "{EmployeeId}").
    /// </summary>
    public string SourceToken { get; init; } = "{Photo}";

    /// <summary>
    /// Optional fallback image if the record photo is missing: a file path or an embedded data URI
    /// (<c>data:image/...;base64,...</c>). Also used as the design-time preview photo.
    /// </summary>
    public string? FallbackImagePath { get; init; }

    /// <summary>
    /// Corner border radius in millimeters/pixels for rounded photo cutouts.
    /// </summary>
    public double BorderRadius { get; init; } = 0.0;

    /// <summary>
    /// Cropping and scaling behavior (AspectFill, AspectFit, Stretch).
    /// </summary>
    public PhotoCropMode CropMode { get; init; } = PhotoCropMode.AspectFill;

    /// <summary>
    /// If true, pre-flight validation requires a matching photo file on disk for every record. Defaults to false.
    /// </summary>
    public bool IsRequired { get; init; } = false;

    /// <summary>
    /// Zoom, pan, rotation, flip and color adjustments applied to every record's photo at render time.
    /// </summary>
    public ImageAdjustments Adjustments { get; init; } = ImageAdjustments.None;

    public override IEnumerable<string> GetReferencedTokens()
    {
        return ExtractTokensFromString(SourceToken);
    }
}
