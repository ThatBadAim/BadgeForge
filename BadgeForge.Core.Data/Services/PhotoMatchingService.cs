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
    /// Resolves the absolute path to the photo file for a given record.
    /// Returns null if no matching photo file is found.
    /// </summary>
    public string? ResolvePhotoPath(BadgeRecord record, PhotoMatchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PhotoDirectory) || !Directory.Exists(options.PhotoDirectory))
        {
            return options.FallbackImagePath;
        }

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
            var directPath = Path.Combine(options.PhotoDirectory, baseName);
            if (File.Exists(directPath))
            {
                return Path.GetFullPath(directPath);
            }

            // Case-insensitive lookup fallback for Linux filesystems
            var matchingFile = FindFileCaseInsensitive(options.PhotoDirectory, baseName);
            if (matchingFile != null)
            {
                return matchingFile;
            }
        }
        else
        {
            // Search across allowed extensions
            foreach (var ext in options.AllowedExtensions)
            {
                var candidateName = baseName + ext;
                var candidatePath = Path.Combine(options.PhotoDirectory, candidateName);
                if (File.Exists(candidatePath))
                {
                    return Path.GetFullPath(candidatePath);
                }

                var matchingFile = FindFileCaseInsensitive(options.PhotoDirectory, candidateName);
                if (matchingFile != null)
                {
                    return matchingFile;
                }
            }
        }

        return options.FallbackImagePath;
    }

    private static string? FindFileCaseInsensitive(string directory, string targetFileName)
    {
        try
        {
            var entries = Directory.EnumerateFiles(directory);
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(entry);
                }
            }
        }
        catch
        {
            // Return null if directory enumeration fails
        }
        return null;
    }
}
