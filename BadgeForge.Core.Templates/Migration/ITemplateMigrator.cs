using System.Text.Json.Nodes;

namespace BadgeForge.Core.Templates.Migration;

/// <summary>
/// Defines a single step migration that transforms template JSON from SourceVersion to TargetVersion.
/// </summary>
public interface ITemplateMigrator
{
    /// <summary>
    /// Schema version this migrator accepts as input.
    /// </summary>
    int SourceVersion { get; }

    /// <summary>
    /// Schema version produced after this migration.
    /// </summary>
    int TargetVersion { get; }

    /// <summary>
    /// Applies migration transformations to the template JSON node.
    /// </summary>
    /// <param name="rootNode">Root JSON object of the template definition.</param>
    /// <returns>Mutated or new JSON node reflecting TargetVersion schema.</returns>
    JsonNode Migrate(JsonNode rootNode);
}
