using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using BadgeForge.Core.Templates.Tokens;

namespace BadgeForge.Core.Rendering.Batch;

/// <summary>
/// A template bound to one record: a private copy whose text and barcode layers hold the record's final values.
/// </summary>
/// <param name="Template">The bound copy. Nothing in it is shared-mutable with the source template.</param>
/// <param name="RenderFields">The only values rendering still needs to look up: each photo layer's image source.</param>
/// <param name="PhotoSources">Each visible photo layer with the image source the record supplies for it (null when none).</param>
public sealed record BoundTemplate(
    TemplateDefinition Template,
    IReadOnlyDictionary<string, string> RenderFields,
    IReadOnlyList<(PhotoLayer Layer, string? Source)> PhotoSources);

/// <summary>
/// Clones a template per record and substitutes its tokens with sanitized record values.
/// </summary>
public static class TemplateBinder
{
    // Bound photo layers look their image up under a key starting with a control character. Substituted values are
    // sanitized (control characters removed), so a record value that happens to read "{{Photo}}" can never be
    // re-resolved into a file path when the text renderer resolves the bound copy
    private static readonly string PhotoKeyPrefix = (char)1 + "photo:";

    /// <summary>
    /// Returns a copy of <paramref name="template"/> with every text and barcode token replaced by the record's
    /// sanitized value (unknown tokens stay as written) and each photo layer pointed at the record's photo.
    /// Substitution is single-pass, so braces inside a value are printed literally. The source template is not
    /// modified.
    /// </summary>
    public static BoundTemplate Bind(TemplateDefinition template, IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(fields);

        var renderFields = new Dictionary<string, string>(StringComparer.Ordinal);
        var photos = new List<(PhotoLayer, string?)>();
        var layers = new List<TemplateLayer>(template.Layers.Count);

        for (int i = 0; i < template.Layers.Count; i++)
        {
            // Layers are immutable records: "with" yields an independent copy
            TemplateLayer bound = template.Layers[i] switch
            {
                TextLayer text => text with { Text = TokenSyntax.Replace(text.Text, fields, sanitize: true) },
                BarcodeLayer barcode => barcode with { ContentToken = TokenSyntax.Replace(barcode.ContentToken, fields, sanitize: true) },
                PhotoLayer photo => BindPhoto(photo, i, fields, renderFields, photos),
                var other => other with { }
            };

            layers.Add(bound);
        }

        var copy = template with
        {
            Layers = layers,
            Metadata = new Dictionary<string, string>(template.Metadata)
        };

        return new BoundTemplate(copy, renderFields, photos);
    }

    private static PhotoLayer BindPhoto(
        PhotoLayer photo,
        int index,
        IReadOnlyDictionary<string, string> fields,
        Dictionary<string, string> renderFields,
        List<(PhotoLayer, string?)> photos)
    {
        string token = TokenSyntax.NormalizeName(photo.SourceToken);
        string? source = fields.TryGetValue(token, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim()
            : fields.TryGetValue(photo.SourceToken, out var direct) && !string.IsNullOrWhiteSpace(direct) ? direct.Trim()
            : null;

        string key = PhotoKeyPrefix + index;
        if (source != null)
        {
            renderFields[key] = source;
        }

        var bound = photo with { SourceToken = key };
        if (bound.IsVisible)
        {
            photos.Add((bound, source));
        }

        return bound;
    }
}
