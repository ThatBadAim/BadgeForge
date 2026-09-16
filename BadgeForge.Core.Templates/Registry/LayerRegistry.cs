using System.Collections.Concurrent;
using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.Core.Templates.Registry;

/// <summary>
/// Thread-safe layer registry implementation.
/// Maps layer type discriminator strings to concrete TemplateLayer subclasses.
/// </summary>
public class LayerRegistry : ILayerRegistry
{
    private readonly ConcurrentDictionary<string, Type> _typeByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, string> _nameByType = new();

    private static readonly Lazy<LayerRegistry> _defaultInstance = new(() =>
    {
        var registry = new LayerRegistry();
        registry.RegisterDefaultBuiltInLayers();
        return registry;
    });

    /// <summary>
    /// Global default layer registry instance pre-populated with standard built-in layers.
    /// </summary>
    public static LayerRegistry Default => _defaultInstance.Value;

    public LayerRegistry()
    {
    }

    /// <summary>
    /// Pre-populates the registry with standard core layers.
    /// </summary>
    public void RegisterDefaultBuiltInLayers()
    {
        Register<TextLayer>(TextLayer.TypeDiscriminator);
        Register<PhotoLayer>(PhotoLayer.TypeDiscriminator);
        Register<StaticImageLayer>(StaticImageLayer.TypeDiscriminator);
        Register<BarcodeLayer>(BarcodeLayer.TypeDiscriminator);
    }

    public void Register<TLayer>(string layerType) where TLayer : TemplateLayer
    {
        Register(layerType, typeof(TLayer));
    }

    public void Register(string layerType, Type layerClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerType);
        ArgumentNullException.ThrowIfNull(layerClass);

        if (!typeof(TemplateLayer).IsAssignableFrom(layerClass))
        {
            throw new ArgumentException($"Type {layerClass.FullName} does not derive from {nameof(TemplateLayer)}.", nameof(layerClass));
        }

        _typeByName[layerType] = layerClass;
        _nameByType[layerClass] = layerType;
    }

    public Type? Resolve(string layerType)
    {
        if (string.IsNullOrWhiteSpace(layerType))
            return null;

        return _typeByName.TryGetValue(layerType, out var type) ? type : null;
    }

    public string? ResolveDiscriminator(Type layerClass)
    {
        ArgumentNullException.ThrowIfNull(layerClass);
        return _nameByType.TryGetValue(layerClass, out var name) ? name : null;
    }

    public bool IsRegistered(string layerType)
    {
        return !string.IsNullOrWhiteSpace(layerType) && _typeByName.ContainsKey(layerType);
    }

    public IReadOnlyDictionary<string, Type> GetAllRegistered()
    {
        return _typeByName.ToDictionary(k => k.Key, v => v.Value);
    }
}
