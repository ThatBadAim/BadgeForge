namespace BadgeForge.Core.Data.Models;

/// <summary>
/// Represents a single data row ingested for badge printing, containing key-value column tokens
/// and resolved asset paths.
/// </summary>
public record BadgeRecord
{
    /// <summary>
    /// 1-based row number originating from the CSV or data source (e.g. Row 2 for the first data line).
    /// </summary>
    public int RowNumber { get; init; }

    /// <summary>
    /// Zero-based index within the batch dataset.
    /// </summary>
    public int BatchIndex { get; init; }

    /// <summary>
    /// Case-insensitive dictionary of field names/tokens to cell string values.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Absolute or verified path to the resolved photo file on disk, if matched.
    /// </summary>
    public string? ResolvedPhotoPath { get; set; }

    /// <summary>
    /// Retrieves a field value by token or column name (with or without curly braces, e.g. "FirstName" or "{FirstName}").
    /// </summary>
    public string? GetValue(string tokenName)
    {
        if (string.IsNullOrWhiteSpace(tokenName))
            return null;

        var cleanKey = tokenName.Trim().TrimStart('{').TrimEnd('}').Trim();
        return Fields.TryGetValue(cleanKey, out var value) ? value : null;
    }

    /// <summary>
    /// Returns the primary identifier value for this record, checking common ID keys.
    /// </summary>
    public string GetPrimaryIdentifier(string? preferredKey = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredKey))
        {
            var preferredVal = GetValue(preferredKey);
            if (!string.IsNullOrWhiteSpace(preferredVal))
                return preferredVal;
        }

        var candidateKeys = new[] { "Id", "ID", "EmployeeId", "EmployeeID", "BadgeId", "BadgeID", "StudentId", "Barcode" };
        foreach (var key in candidateKeys)
        {
            var val = GetValue(key);
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }

        return $"Row-{RowNumber}";
    }
}
