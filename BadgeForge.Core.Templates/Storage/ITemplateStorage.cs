using BadgeForge.Core.Templates.Models;

namespace BadgeForge.Core.Templates.Storage;

/// <summary>
/// Service interface for serializing, deserializing, saving, and loading BadgeForge templates.
/// </summary>
public interface ITemplateStorage
{
    /// <summary>
    /// Serializes a template definition into formatted JSON.
    /// </summary>
    string Serialize(TemplateDefinition template);

    /// <summary>
    /// Deserializes template JSON into a TemplateDefinition, automatically migrating older schemas if needed.
    /// </summary>
    TemplateDefinition Deserialize(string json);

    /// <summary>
    /// Saves a template definition to a file on disk.
    /// </summary>
    void Save(TemplateDefinition template, string filePath);

    /// <summary>
    /// Loads a template definition from a file on disk.
    /// </summary>
    TemplateDefinition Load(string filePath);

    /// <summary>
    /// Asynchronously saves a template definition to disk.
    /// </summary>
    Task SaveAsync(TemplateDefinition template, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously loads a template definition from disk.
    /// </summary>
    Task<TemplateDefinition> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
