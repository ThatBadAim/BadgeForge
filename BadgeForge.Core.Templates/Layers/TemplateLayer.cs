using System.Text.Json.Serialization;
using BadgeForge.Core.Templates.Registry;
using BadgeForge.Core.Templates.Tokens;

namespace BadgeForge.Core.Templates.Layers;

/// <summary>
/// Abstract base class for all badge template layers.
/// Designed for polymorphic serialization and runtime extensibility via ILayerRegistry.
/// </summary>
[JsonConverter(typeof(LayerJsonConverter))]
public abstract record TemplateLayer
{
    /// <summary>
    /// Unique identifier for this layer instance.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Friendly display name of the layer in the designer.
    /// </summary>
    public string Name { get; init; } = "Layer";

    /// <summary>
    /// Type discriminator used by the LayerRegistry for polymorphic JSON serialization.
    /// </summary>
    public abstract string LayerType { get; }

    /// <summary>
    /// Horizontal position on the card canvas in millimeters or pixels (default millimeter grid).
    /// </summary>
    public double X { get; init; } = 0.0;

    /// <summary>
    /// Vertical position on the card canvas in millimeters or pixels.
    /// </summary>
    public double Y { get; init; } = 0.0;

    /// <summary>
    /// Width of the layer bounding box.
    /// </summary>
    public double Width { get; init; } = 10.0;

    /// <summary>
    /// Height of the layer bounding box.
    /// </summary>
    public double Height { get; init; } = 10.0;

    /// <summary>
    /// Visual stacking order (higher numbers render on top).
    /// </summary>
    public int ZIndex { get; init; } = 0;

    /// <summary>
    /// Whether the layer is visible in preview and output.
    /// </summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>
    /// Whether the layer is locked against accidental edits in the designer.
    /// </summary>
    public bool IsLocked { get; init; } = false;

    /// <summary>
    /// Layer opacity from 0.0 (fully transparent) to 1.0 (fully opaque).
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// Extracts any field tokens embedded in this layer (e.g. "{{FirstName}}", "{{EmployeeId}}").
    /// </summary>
    public virtual IEnumerable<string> GetReferencedTokens() => Enumerable.Empty<string>();

    /// <summary>
    /// True when this layer's content comes from record data through at least one token (e.g. "{{FullName}}").
    /// The designer outlines such layers so bound and static elements are told apart at a glance.
    /// </summary>
    [JsonIgnore]
    public bool HasDynamicTokens => GetReferencedTokens().Any();

    /// <summary>
    /// Helper to extract token names from a template string containing "{{TokenName}}" (or legacy "{TokenName}").
    /// </summary>
    protected static IEnumerable<string> ExtractTokensFromString(string? text) => TokenSyntax.Extract(text);
}
