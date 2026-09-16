using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BadgeForge.Core.Templates.Migration;
using BadgeForge.Core.Templates.Models;
using BadgeForge.Core.Templates.Registry;

namespace BadgeForge.Core.Templates.Storage;

/// <summary>
/// JSON implementation of ITemplateStorage.
/// Supports schema version migration and dynamic layer type resolution.
/// </summary>
public class JsonTemplateStorage : ITemplateStorage
{
    private readonly ILayerRegistry _layerRegistry;
    private readonly TemplateMigrationPipeline _migrationPipeline;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonTemplateStorage(ILayerRegistry? layerRegistry = null, TemplateMigrationPipeline? migrationPipeline = null)
    {
        _layerRegistry = layerRegistry ?? LayerRegistry.Default;
        _migrationPipeline = migrationPipeline ?? new TemplateMigrationPipeline();

        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        _serializerOptions.Converters.Add(new LayerJsonConverter(_layerRegistry));
    }

    public string Serialize(TemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return JsonSerializer.Serialize(template, _serializerOptions);
    }

    public TemplateDefinition Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var node = JsonNode.Parse(json);
        if (node == null)
        {
            throw new JsonException("Failed to parse JSON string into a valid JSON node.");
        }

        // Migrate template to current schema version if necessary
        var migratedNode = _migrationPipeline.MigrateTo(node, TemplateDefinition.CurrentSchemaVersion);

        var template = migratedNode.Deserialize<TemplateDefinition>(_serializerOptions);
        if (template == null)
        {
            throw new JsonException("Deserialized template definition is null.");
        }

        return template;
    }

    public void Save(TemplateDefinition template, string filePath)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = Serialize(template);
        AtomicFileWriter.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public TemplateDefinition Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template file not found at '{filePath}'.", filePath);
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize(json);
    }

    public async Task SaveAsync(TemplateDefinition template, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = Serialize(template);
        await AtomicFileWriter.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
    }

    public async Task<TemplateDefinition> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template file not found at '{filePath}'.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return Deserialize(json);
    }
}
