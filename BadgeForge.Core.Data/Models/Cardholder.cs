using BadgeForge.Core.Data.Services;

namespace BadgeForge.Core.Data.Models;

/// <summary>
/// The identity record of one person a badge is printed for, kept separate from any badge layout.
/// A template refers to these values only through tokens (<c>{{FullName}}</c>, <c>{{Photo}}</c>, …), so the same
/// roster prints on any template and a template prints any roster.
/// </summary>
public class Cardholder
{
    public const string IdToken = "Id";
    public const string FullNameToken = "FullName";
    public const string FirstNameToken = "FirstName";
    public const string LastNameToken = "LastName";
    public const string JobTitleToken = "JobTitle";
    public const string DepartmentToken = "Department";
    public const string PhotoToken = "Photo";

    /// <summary>
    /// Tokens every cardholder provides, in the order the designer offers them.
    /// </summary>
    public static IReadOnlyList<string> StandardTokens { get; } = new[]
    {
        FullNameToken, FirstNameToken, LastNameToken, JobTitleToken, DepartmentToken, IdToken, PhotoToken
    };

    /// <summary>
    /// Badge or employee number identifying the person.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    /// <summary>
    /// Path to the person's photo file, or null when they don't have one yet.
    /// </summary>
    public string? PhotoPath { get; set; }

    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Any other values (e.g. ExpiryDate, AccessLevel), available to templates as tokens of the same name.
    /// </summary>
    public Dictionary<string, string> AdditionalFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the person is queued for the next print or export run.
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// The token values a template renders this person with. First and last names are derived from the full name
    /// (and vice versa) when only one form is known.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToFieldData()
    {
        var data = new Dictionary<string, string>(AdditionalFields, StringComparer.OrdinalIgnoreCase);

        Set(data, IdToken, Id);
        Set(data, FullNameToken, FullName);
        Set(data, JobTitleToken, JobTitle);
        Set(data, DepartmentToken, Department);
        if (!string.IsNullOrWhiteSpace(PhotoPath))
        {
            data[PhotoToken] = PhotoPath;
        }

        CardholderMapper.FillDerivedNames(data);
        return data;
    }

    private static void Set(Dictionary<string, string> data, string key, string value)
    {
        // A value typed into a dedicated property wins; an empty one doesn't hide the same key in AdditionalFields
        if (!string.IsNullOrWhiteSpace(value) || !data.ContainsKey(key))
        {
            data[key] = value ?? string.Empty;
        }
    }
}
