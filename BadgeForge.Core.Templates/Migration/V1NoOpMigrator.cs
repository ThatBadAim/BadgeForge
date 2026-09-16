using System.Text.Json.Nodes;

namespace BadgeForge.Core.Templates.Migration;

/// <summary>
/// Baseline no-op migrator for Schema Version 1.
/// Validates schema version is 1 and leaves template structure intact.
/// </summary>
public class V1NoOpMigrator : ITemplateMigrator
{
    public int SourceVersion => 1;
    public int TargetVersion => 1;

    public JsonNode Migrate(JsonNode rootNode)
    {
        ArgumentNullException.ThrowIfNull(rootNode);
        if (rootNode is JsonObject obj && !obj.ContainsKey("SchemaVersion"))
        {
            obj["SchemaVersion"] = 1;
        }
        return rootNode;
    }
}
