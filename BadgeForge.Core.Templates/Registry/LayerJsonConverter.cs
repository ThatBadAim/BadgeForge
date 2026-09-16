using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.Core.Templates.Registry;

/// <summary>
/// Polymorphic JSON converter for TemplateLayer instances.
/// Utilizes ILayerRegistry to dynamically resolve concrete classes based on the "LayerType" property,
/// enabling new layer types to be added without modifying serialization logic.
/// </summary>
public class LayerJsonConverter : JsonConverter<TemplateLayer>
{
    private readonly ILayerRegistry _registry;
    private static readonly ConcurrentDictionary<JsonSerializerOptions, JsonSerializerOptions> _innerOptionsCache = new();

    public LayerJsonConverter() : this(LayerRegistry.Default)
    {
    }

    public LayerJsonConverter(ILayerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(TemplateLayer).IsAssignableFrom(typeToConvert);
    }

    public override TemplateLayer? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        // Discover layer type from LayerType or Type discriminator (case-insensitive)
        string? layerType = null;
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "LayerType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "Type", StringComparison.OrdinalIgnoreCase))
            {
                layerType = prop.Value.GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(layerType))
        {
            throw new JsonException("Failed to deserialize TemplateLayer: missing 'LayerType' discriminator.");
        }

        var concreteType = _registry.Resolve(layerType);
        if (concreteType == null)
        {
            throw new JsonException($"Unrecognized or unregistered layer type '{layerType}'. Register this type with ILayerRegistry before deserializing.");
        }

        var innerOptions = GetInnerOptions(options);
        return (TemplateLayer?)JsonSerializer.Deserialize(root.GetRawText(), concreteType, innerOptions);
    }

    public override void Write(Utf8JsonWriter writer, TemplateLayer value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        var concreteType = value.GetType();
        var innerOptions = GetInnerOptions(options);

        var node = JsonSerializer.SerializeToNode(value, concreteType, innerOptions);
        if (node is JsonObject obj)
        {
            // Ensure LayerType discriminator is explicitly present
            var discriminator = _registry.ResolveDiscriminator(concreteType) ?? value.LayerType;
            obj["LayerType"] = discriminator;
            obj.WriteTo(writer, options);
        }
        else
        {
            node?.WriteTo(writer, options);
        }
    }

    private JsonSerializerOptions GetInnerOptions(JsonSerializerOptions options)
    {
        return _innerOptionsCache.GetOrAdd(options, opt =>
        {
            var copy = new JsonSerializerOptions(opt);
            for (int i = copy.Converters.Count - 1; i >= 0; i--)
            {
                if (copy.Converters[i] is LayerJsonConverter)
                {
                    copy.Converters.RemoveAt(i);
                }
            }
            return copy;
        });
    }
}
