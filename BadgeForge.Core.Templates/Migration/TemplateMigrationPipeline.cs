using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BadgeForge.Core.Templates.Migration;

/// <summary>
/// Orchestrates schema migrations across template versions to ensure backward compatibility.
/// </summary>
public class TemplateMigrationPipeline
{
    private readonly ConcurrentDictionary<(int From, int To), ITemplateMigrator> _migrators = new();

    public TemplateMigrationPipeline()
    {
        Register(new V1NoOpMigrator());
    }

    /// <summary>
    /// Registers a schema migrator.
    /// </summary>
    public void Register(ITemplateMigrator migrator)
    {
        ArgumentNullException.ThrowIfNull(migrator);
        _migrators[(migrator.SourceVersion, migrator.TargetVersion)] = migrator;
    }

    /// <summary>
    /// Inspects the SchemaVersion of the input JSON node and executes chained migrations
    /// until the target version is reached.
    /// </summary>
    public JsonNode MigrateTo(JsonNode rootNode, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(rootNode);

        int currentVersion = ExtractSchemaVersion(rootNode);

        if (currentVersion == targetVersion)
        {
            // If there is an exact same-version migrator (e.g. V1NoOpMigrator), execute it
            if (_migrators.TryGetValue((currentVersion, targetVersion), out var identityMigrator))
            {
                return identityMigrator.Migrate(rootNode);
            }
            return rootNode;
        }

        if (currentVersion > targetVersion)
        {
            throw new InvalidOperationException(
                $"Template schema version {currentVersion} is newer than supported version {targetVersion}. Please update BadgeForge.");
        }

        var currentNode = rootNode;
        while (currentVersion < targetVersion)
        {
            // Find a migrator whose SourceVersion matches currentVersion
            var step = _migrators.Values
                .Where(m => m.SourceVersion == currentVersion && m.TargetVersion > currentVersion)
                .OrderByDescending(m => m.TargetVersion)
                .FirstOrDefault();

            if (step == null)
            {
                throw new InvalidOperationException(
                    $"No migration path available to upgrade template schema from v{currentVersion} to v{targetVersion}.");
            }

            currentNode = step.Migrate(currentNode);
            currentVersion = step.TargetVersion;

            // Ensure SchemaVersion in JSON reflects the new version
            if (currentNode is JsonObject obj)
            {
                obj["SchemaVersion"] = currentVersion;
            }
        }

        return currentNode;
    }

    /// <summary>
    /// Helper to safely extract integer SchemaVersion from JSON node. Defaults to 1 if absent.
    /// Accepts a whole number written as a number ("SchemaVersion": 1) or as text ("SchemaVersion": "1").
    /// </summary>
    /// <exception cref="JsonException">SchemaVersion is present but isn't a whole number.</exception>
    public static int ExtractSchemaVersion(JsonNode rootNode)
    {
        if (rootNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("SchemaVersion", out var versionNode) ||
                obj.TryGetPropertyValue("schemaVersion", out versionNode))
            {
                if (versionNode == null)
                {
                    return 1;
                }

                if (versionNode is JsonValue value)
                {
                    if (value.TryGetValue<int>(out int number))
                    {
                        return number;
                    }

                    if (value.TryGetValue<double>(out double real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue)
                    {
                        return (int)real;
                    }

                    if (value.TryGetValue<string>(out string? text) &&
                        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        return parsed;
                    }
                }

                throw new JsonException($"Template SchemaVersion {versionNode.ToJsonString()} is not a whole number.");
            }
        }
        return 1;
    }
}
