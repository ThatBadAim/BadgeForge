using System.Text.Json.Serialization;

namespace BadgeForge.Core.Templates.Models;

/// <summary>
/// Non-destructive edits applied to an image when it is drawn into its layer frame.
/// The source image is never modified; these values are re-applied on every render.
/// Immutable — derive changed copies with <c>with</c> so duplicated layers never share mutable state.
/// </summary>
public record ImageAdjustments
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 5.0;

    public static ImageAdjustments None { get; } = new();

    /// <summary>
    /// Magnification on top of the crop mode's base fit (1.0 = no extra zoom, up to 5.0).
    /// </summary>
    public double Zoom { get; init; } = 1.0;

    /// <summary>
    /// Horizontal pan from -1.0 to 1.0 as a fraction of the image's overflow past the frame (0 = centered).
    /// </summary>
    public double OffsetX { get; init; } = 0.0;

    /// <summary>
    /// Vertical pan from -1.0 to 1.0 as a fraction of the image's overflow past the frame (0 = centered).
    /// </summary>
    public double OffsetY { get; init; } = 0.0;

    /// <summary>
    /// Clockwise rotation in degrees: 0, 90, 180 or 270.
    /// </summary>
    public int Rotation { get; init; } = 0;

    /// <summary>
    /// Mirror the image left-to-right (applied after rotation).
    /// </summary>
    public bool FlipHorizontal { get; init; } = false;

    /// <summary>
    /// Mirror the image top-to-bottom (applied after rotation).
    /// </summary>
    public bool FlipVertical { get; init; } = false;

    /// <summary>
    /// Brightness shift from -1.0 (black) to 1.0 (white).
    /// </summary>
    public double Brightness { get; init; } = 0.0;

    /// <summary>
    /// Contrast change from -1.0 (flat grey) to 1.0 (double contrast).
    /// </summary>
    public double Contrast { get; init; } = 0.0;

    /// <summary>
    /// Saturation change from -1.0 (black and white) to 1.0 (double saturation).
    /// </summary>
    public double Saturation { get; init; } = 0.0;

    /// <summary>
    /// True when the image keeps its default placement (no zoom, pan, rotation or flip).
    /// </summary>
    [JsonIgnore]
    public bool IsIdentityTransform =>
        Zoom == 1.0 && OffsetX == 0.0 && OffsetY == 0.0 && NormalizedRotation == 0 && !FlipHorizontal && !FlipVertical;

    /// <summary>
    /// True when any color correction is active.
    /// </summary>
    [JsonIgnore]
    public bool HasColorAdjustments => Brightness != 0.0 || Contrast != 0.0 || Saturation != 0.0;

    /// <summary>
    /// Rotation snapped to the nearest quarter turn in the 0–270 range.
    /// </summary>
    [JsonIgnore]
    public int NormalizedRotation => ((int)Math.Round(Rotation / 90.0) % 4 + 4) % 4 * 90;

    /// <summary>
    /// Returns a copy with every value clamped to its valid range.
    /// </summary>
    public ImageAdjustments Normalized() => this with
    {
        Zoom = Math.Clamp(Zoom, MinZoom, MaxZoom),
        OffsetX = Math.Clamp(OffsetX, -1.0, 1.0),
        OffsetY = Math.Clamp(OffsetY, -1.0, 1.0),
        Rotation = NormalizedRotation,
        Brightness = Math.Clamp(Brightness, -1.0, 1.0),
        Contrast = Math.Clamp(Contrast, -1.0, 1.0),
        Saturation = Math.Clamp(Saturation, -1.0, 1.0)
    };
}
