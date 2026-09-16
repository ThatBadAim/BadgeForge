using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.Core.Templates.Registry;

/// <summary>
/// Defines a contract for registering and resolving polymorphic badge template layer types.
/// Enables adding arbitrary custom layer types at runtime without modifying existing core classes.
/// </summary>
public interface ILayerRegistry
{
    /// <summary>
    /// Registers a layer type with its unique discriminator string.
    /// </summary>
    void Register<TLayer>(string layerType) where TLayer : TemplateLayer;

    /// <summary>
    /// Registers a layer type with its unique discriminator string.
    /// </summary>
    void Register(string layerType, Type layerClass);

    /// <summary>
    /// Resolves the concrete Type corresponding to a layer type discriminator string.
    /// </summary>
    Type? Resolve(string layerType);

    /// <summary>
    /// Resolves the discriminator string for a concrete layer Type.
    /// </summary>
    string? ResolveDiscriminator(Type layerClass);

    /// <summary>
    /// Returns whether the given discriminator is registered.
    /// </summary>
    bool IsRegistered(string layerType);

    /// <summary>
    /// Gets all registered layer type discriminators and their implementing classes.
    /// </summary>
    IReadOnlyDictionary<string, Type> GetAllRegistered();
}
