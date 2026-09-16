namespace BadgeForge.Core.Data.Models;

/// <summary>
/// Configuration options for resolving photo files on disk for badge records.
/// </summary>
public class PhotoMatchingOptions
{
    /// <summary>
    /// Base directory where badge portrait photos are stored.
    /// </summary>
    public string PhotoDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Filename pattern supporting template tokens, e.g. "{EmployeeId}.jpg", "{LastName}_{FirstName}.*", or "{Photo}".
    /// Defaults to "{EmployeeId}.*".
    /// </summary>
    public string FilenamePattern { get; set; } = "{EmployeeId}.*";

    /// <summary>
    /// File extensions searched when a pattern uses ".*" or omits an extension.
    /// </summary>
    public IReadOnlyList<string> AllowedExtensions { get; set; } = new[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp"
    };

    /// <summary>
    /// Fallback default placeholder photo path if a record's photo cannot be found on disk.
    /// </summary>
    public string? FallbackImagePath { get; set; }
}
