using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.Core.Templates.Models;

/// <summary>
/// Root model representing a badge template definition.
/// Fully JSON-serializable with schema versioning, target card format reference,
/// and an extensible polymorphic layer collection.
/// </summary>
public record TemplateDefinition
{
    /// <summary>
    /// Current schema version of the template format.
    /// Used by the migration pipeline to upgrade older template files transparently.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Unique identifier for this template definition.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Human-readable template name (e.g., "Staff ID Badge 2026", "Visitor Pass Landscape").
    /// </summary>
    public string Name { get; init; } = "New Badge Template";

    /// <summary>
    /// Schema version of this template payload.
    /// </summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Target physical card format and resolution profile (e.g. CR80 300 DPI, CR79).
    /// </summary>
    public CardFormat TargetFormat { get; init; } = CardFormat.CR80;

    /// <summary>
    /// Ordered collection of visual badge layers.
    /// </summary>
    public List<TemplateLayer> Layers { get; init; } = new();

    /// <summary>
    /// Timestamp when the template was created (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the template was last modified (UTC).
    /// </summary>
    public DateTime ModifiedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional arbitrary metadata or designer settings.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Returns all distinct token names referenced by any layer in this template (e.g. "FirstName", "EmployeeId").
    /// </summary>
    public IReadOnlySet<string> GetAllReferencedTokens()
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in Layers)
        {
            foreach (var token in layer.GetReferencedTokens())
            {
                tokens.Add(token);
            }
        }
        return tokens;
    }
}
