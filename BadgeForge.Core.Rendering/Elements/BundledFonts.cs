using System.Reflection;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Guarantees consistent, good-looking text rendering regardless of what fonts happen to be installed
/// on the machine running the app. <see cref="SKTypeface.FromFamilyName"/> never returns null for an
/// unknown family — it silently substitutes the platform's fallback typeface, which on a bare Linux
/// install can be an unhinted, low-quality font. This class checks whether the requested family was
/// actually matched and, if not, falls back to a font embedded in this assembly instead of whatever
/// SkiaSharp happened to pick.
/// </summary>
public static class BundledFonts
{
    /// <summary>
    /// Font families offered to users. Each is a metrics-compatible clone of a common commercial font
    /// (Liberation, SIL Open Font License) that is bundled as an embedded resource, so it renders
    /// identically on every platform whether or not it happens to be installed system-wide.
    /// </summary>
    public static readonly IReadOnlyList<string> AvailableFamilies = new[]
    {
        "Arial",
        "Times New Roman",
        "Courier New",
    };

    private static readonly Dictionary<string, string> FamilyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = "Sans",
        ["Helvetica"] = "Sans",
        ["Liberation Sans"] = "Sans",
        ["Times New Roman"] = "Serif",
        ["Times"] = "Serif",
        ["Georgia"] = "Serif",
        ["Liberation Serif"] = "Serif",
        ["Courier New"] = "Mono",
        ["Courier"] = "Mono",
        ["Consolas"] = "Mono",
        ["Liberation Mono"] = "Mono",
    };

    private static readonly Dictionary<string, SKTypeface> Cache = new();
    private static readonly object CacheLock = new();

    /// <summary>
    /// Resolves the best available typeface for the requested family/weight/slant, falling back to a
    /// bundled font when the family isn't genuinely available on this machine. Results are cached for
    /// the process lifetime — both system and bundled typefaces are shared, never disposed per-render,
    /// since <see cref="SKTypeface.FromFamilyName"/> would otherwise leak a native handle on every call.
    /// </summary>
    public static SKTypeface Resolve(string? familyName, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        string cacheKey = $"resolved|{familyName ?? string.Empty}|{weight}|{slant}";

        lock (CacheLock)
        {
            if (Cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var resolved = ResolveUncached(familyName, weight, slant);
            Cache[cacheKey] = resolved;
            return resolved;
        }
    }

    private static SKTypeface ResolveUncached(string? familyName, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        if (!string.IsNullOrWhiteSpace(familyName))
        {
            var systemMatch = SKTypeface.FromFamilyName(familyName, weight, SKFontStyleWidth.Normal, slant);
            if (systemMatch is not null)
            {
                bool genuinelyMatched = string.Equals(systemMatch.FamilyName, familyName, StringComparison.OrdinalIgnoreCase);
                if (genuinelyMatched)
                {
                    return systemMatch;
                }

                systemMatch.Dispose();
            }
        }

        string bundledKey = familyName is not null && FamilyAliases.TryGetValue(familyName, out var alias)
            ? alias
            : "Sans";

        bool bold = weight >= SKFontStyleWeight.SemiBold;
        bool italic = slant != SKFontStyleSlant.Upright;

        return LoadBundled(bundledKey, bold, italic);
    }

    private static SKTypeface LoadBundled(string family, bool bold, bool italic)
    {
        string resourceName = family switch
        {
            "Serif" => $"LiberationSerif-{StyleSuffix(bold, italic)}.ttf",
            "Mono" => $"LiberationMono-{StyleSuffix(bold, italic)}.ttf",
            _ => $"LiberationSans-{StyleSuffix(bold, italic)}.ttf",
        };

        lock (CacheLock)
        {
            if (Cache.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var assembly = typeof(BundledFonts).Assembly;
            string fullResourceName = $"BadgeForge.Core.Rendering.Assets.Fonts.{resourceName}";

            using var stream = assembly.GetManifestResourceStream(fullResourceName)
                ?? throw new InvalidOperationException($"Bundled font resource '{fullResourceName}' was not found.");

            var typeface = SKTypeface.FromStream(stream)
                ?? throw new InvalidOperationException($"Failed to load bundled font '{fullResourceName}'.");

            Cache[resourceName] = typeface;
            return typeface;
        }
    }

    private static string StyleSuffix(bool bold, bool italic) => (bold, italic) switch
    {
        (true, true) => "BoldItalic",
        (true, false) => "Bold",
        (false, true) => "Italic",
        _ => "Regular",
    };
}
