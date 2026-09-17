using System.Text.RegularExpressions;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Templates.Tokens;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Resolves badge photo files on disk for records based on configurable filename patterns and tokens.
/// </summary>
public class PhotoMatchingService
{
    /// <summary>
    /// Builds a fast case-insensitive lookup index of all filenames present in the given photo directory.
    /// Key: filename with extension (e.g. "john_doe.jpg"). Value: full absolute path to the file.
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildDirectoryIndex(string? directory)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return dict;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(entry);
                dict.TryAdd(name, Path.GetFullPath(entry));
            }
        }
        catch
        {
            // Return whatever was indexed if directory enumeration fails
        }

        return dict;
    }

    /// <summary>
    /// Resolves the absolute path to the photo file for a given record.
    /// If an existing <paramref name="directoryIndex"/> is provided, it avoids scanning the filesystem.
    /// Returns null if no matching photo file is found.
    /// </summary>
    public string? ResolvePhotoPath(BadgeRecord record, PhotoMatchingOptions options, IReadOnlyDictionary<string, string>? directoryIndex = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PhotoDirectory) || !Directory.Exists(options.PhotoDirectory))
        {
            return options.FallbackImagePath;
        }

        directoryIndex ??= BuildDirectoryIndex(options.PhotoDirectory);

        // Expand tokens in the filename pattern using record fields
        string expandedPattern = TokenSyntax.Replace(options.FilenamePattern, tokenName => record.GetValue(tokenName) ?? string.Empty);

        if (string.IsNullOrWhiteSpace(expandedPattern))
        {
            return options.FallbackImagePath;
        }

        // Handle path if pattern resolved to a complete filename or relative path
        bool hasWildcardExt = expandedPattern.EndsWith(".*", StringComparison.OrdinalIgnoreCase);
        string baseName = hasWildcardExt
            ? expandedPattern[..^2]
            : expandedPattern;

        bool hasExplicitExtension = !hasWildcardExt && Path.HasExtension(baseName);

        if (hasExplicitExtension)
        {
            if (directoryIndex.TryGetValue(baseName, out var matchedPath))
            {
                return matchedPath;
            }

            var directPath = Path.Combine(options.PhotoDirectory, baseName);
            if (File.Exists(directPath))
            {
                return Path.GetFullPath(directPath);
            }
        }
        else
        {
            // Search across allowed extensions
            foreach (var ext in options.AllowedExtensions)
            {
                var candidateName = baseName + ext;
                if (directoryIndex.TryGetValue(candidateName, out var matchedPath))
                {
                    return matchedPath;
                }

                var candidatePath = Path.Combine(options.PhotoDirectory, candidateName);
                if (File.Exists(candidatePath))
                {
                    return Path.GetFullPath(candidatePath);
                }
            }
        }

        return options.FallbackImagePath;
    }
}
