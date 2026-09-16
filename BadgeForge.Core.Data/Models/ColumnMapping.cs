namespace BadgeForge.Core.Data.Models;

/// <summary>
/// Configures mapping from CSV column header names to badge template token keys.
/// </summary>
public class ColumnMapping
{
    private readonly Dictionary<string, string> _mappings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Explicit mappings from CSV column headers to template tokens.
    /// </summary>
    public IReadOnlyDictionary<string, string> Mappings => _mappings;

    /// <summary>
    /// Adds or updates a mapping between a CSV column header and a template token.
    /// </summary>
    /// <param name="csvColumn">Header name in the CSV file (e.g. "Emp Number").</param>
    /// <param name="templateToken">Target token name used in templates (e.g. "EmployeeId").</param>
    public ColumnMapping Map(string csvColumn, string templateToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateToken);

        var cleanToken = templateToken.Trim().TrimStart('{').TrimEnd('}').Trim();
        _mappings[csvColumn.Trim()] = cleanToken;
        return this;
    }

    /// <summary>
    /// Resolves the template token for a given CSV column header.
    /// If no explicit mapping exists, returns the sanitized column header.
    /// </summary>
    public string ResolveToken(string csvColumn)
    {
        if (string.IsNullOrWhiteSpace(csvColumn))
            return string.Empty;

        var trimmed = csvColumn.Trim();
        if (_mappings.TryGetValue(trimmed, out var mappedToken))
        {
            return mappedToken;
        }

        // Default heuristic: remove spaces and non-alphanumeric characters for clean token matching
        return trimmed.Replace(" ", "").Replace("_", "");
    }
}
