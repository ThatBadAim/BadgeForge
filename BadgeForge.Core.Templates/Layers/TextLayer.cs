using BadgeForge.Core.Templates.Enums;

namespace BadgeForge.Core.Templates.Layers;

/// <summary>
/// Represents a text element on the badge, supporting static text, dynamic token placeholders (e.g. "{FirstName}"),
/// typography settings, and pure-black routing for K-resin ribbon panels.
/// </summary>
public record TextLayer : TemplateLayer
{
    public const string TypeDiscriminator = "Text";

    public override string LayerType => TypeDiscriminator;

    /// <summary>
    /// Content string, which may contain tokens such as "{FirstName} {LastName}" or static text.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Font family name (e.g. "Arial", "Times New Roman", "Courier New"). These three are guaranteed to
    /// render consistently on every platform via bundled fallback fonts — see
    /// BadgeForge.Core.Rendering.Elements.BundledFonts.
    /// </summary>
    public string FontFamily { get; init; } = "Arial";

    /// <summary>
    /// Font size in points or canvas units.
    /// </summary>
    public double FontSize { get; init; } = 12.0;

    /// <summary>
    /// Font weight (e.g. "Normal", "Bold", "SemiBold", "Light").
    /// </summary>
    public string FontWeight { get; init; } = "Normal";

    /// <summary>
    /// Hex color string (e.g. "#000000").
    /// </summary>
    public string ColorHex { get; init; } = "#000000";

    /// <summary>
    /// Horizontal text alignment within the bounding box.
    /// </summary>
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;

    /// <summary>
    /// How text that doesn't fit the frame is handled. Defaults to shrinking so a long name never prints past the
    /// edge of the badge.
    /// </summary>
    public TextOverflowMode Overflow { get; init; } = TextOverflowMode.ShrinkToFit;

    /// <summary>
    /// Smallest font size, in points, that <see cref="TextOverflowMode.ShrinkToFit"/> and
    /// <see cref="TextOverflowMode.Wrap"/> shrink to before cutting the text with an ellipsis.
    /// </summary>
    public double MinFontSize { get; init; } = 5.0;

    /// <summary>
    /// Distance between baselines of consecutive lines, as a multiple of the font size.
    /// </summary>
    public double LineHeight { get; init; } = 1.2;

    /// <summary>
    /// If true, rendering forces 1-bit pure black (#000000) with anti-aliasing disabled to route to the
    /// thermal transfer K-resin ribbon panel rather than dye-sublimation YMC panels.
    /// </summary>
    public bool IsPureBlackKResin { get; init; } = true;

    /// <summary>
    /// When true, any bound tokens in this layer must evaluate to non-empty values during pre-flight validation.
    /// </summary>
    public bool IsRequired { get; init; } = false;

    public override IEnumerable<string> GetReferencedTokens()
    {
        return ExtractTokensFromString(Text);
    }
}
