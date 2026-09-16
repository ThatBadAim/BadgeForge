using BadgeForge.Core.Data.Models;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Converts between loosely-labelled roster rows and <see cref="Cardholder"/> records. Spreadsheets name the same
/// column many ways ("Name", "Full Name", "Employee Name"; "Title", "Position"; "Dept"…); the mapper recognises the
/// common spellings so a template written with <c>{{FullName}}</c> or <c>{{JobTitle}}</c> works on them unchanged.
/// </summary>
public static class CardholderMapper
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [Cardholder.IdToken] = new[] { "id", "employeeid", "employeenumber", "employeeno", "badgeid", "badgenumber", "staffid", "studentid", "cardnumber", "personid" },
        [Cardholder.FullNameToken] = new[] { "fullname", "name", "displayname", "employeename", "personname", "cardholder", "cardholdername" },
        [Cardholder.FirstNameToken] = new[] { "firstname", "givenname", "forename", "first" },
        [Cardholder.LastNameToken] = new[] { "lastname", "surname", "familyname", "last" },
        [Cardholder.JobTitleToken] = new[] { "jobtitle", "title", "position", "role", "designation" },
        [Cardholder.DepartmentToken] = new[] { "department", "dept", "division", "team", "unit" },
        [Cardholder.PhotoToken] = new[] { "photo", "photopath", "photofile", "picture", "image", "portrait" },
    };

    /// <summary>
    /// Builds a cardholder from a roster row. Recognised columns fill the typed properties; everything else is kept
    /// in <see cref="Cardholder.AdditionalFields"/>.
    /// </summary>
    public static Cardholder FromRecord(BadgeRecord record, bool isSelected = true)
    {
        ArgumentNullException.ThrowIfNull(record);

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Take(string token)
        {
            foreach (var (key, value) in record.Fields)
            {
                if (IsAliasOf(key, token))
                {
                    consumed.Add(Normalize(key));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return string.Empty;
        }

        var cardholder = new Cardholder
        {
            Id = Take(Cardholder.IdToken),
            FullName = Take(Cardholder.FullNameToken),
            JobTitle = Take(Cardholder.JobTitleToken),
            Department = Take(Cardholder.DepartmentToken),
            IsSelected = isSelected
        };

        string photo = Take(Cardholder.PhotoToken);
        cardholder.PhotoPath = !string.IsNullOrWhiteSpace(record.ResolvedPhotoPath)
            ? record.ResolvedPhotoPath
            : photo.Length > 0 ? photo : null;

        // Readers store "First Name" and its compacted "FirstName" copy; keep one of each, preferring the compact
        // spelling because that is the one a template token can name
        var seen = new HashSet<string>(consumed, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in record.Fields.OrderBy(kv => kv.Key.Any(c => !char.IsLetterOrDigit(c))))
        {
            if (seen.Add(Normalize(key)))
            {
                cardholder.AdditionalFields[key] = value ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(cardholder.FullName))
        {
            cardholder.FullName = $"{Take(Cardholder.FirstNameToken)} {Take(Cardholder.LastNameToken)}".Trim();
        }

        return cardholder;
    }

    /// <summary>
    /// Builds a roster row from a cardholder, with every token value as a field and the photo as the resolved path.
    /// </summary>
    public static BadgeRecord ToRecord(Cardholder cardholder, int rowNumber = 0, int batchIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(cardholder);

        var fields = new Dictionary<string, string>(cardholder.ToFieldData(), StringComparer.OrdinalIgnoreCase);
        fields.Remove(Cardholder.PhotoToken);
        return new BadgeRecord
        {
            RowNumber = rowNumber,
            BatchIndex = batchIndex,
            Fields = fields,
            ResolvedPhotoPath = cardholder.PhotoPath
        };
    }

    /// <summary>
    /// Adds the standard cardholder tokens to a field dictionary where they are missing or empty, taking their values
    /// from alias columns ("Title" → JobTitle, "Dept" → Department, …) and deriving names. Values already present are
    /// never replaced.
    /// </summary>
    /// <param name="fieldData">Values to add to.</param>
    /// <param name="includePhoto">Also fill Photo from a picture column; callers that resolve photo files themselves pass false.</param>
    public static void AddStandardTokens(IDictionary<string, string> fieldData, bool includePhoto = true)
    {
        ArgumentNullException.ThrowIfNull(fieldData);

        foreach (string token in Aliases.Keys)
        {
            if ((!includePhoto && token == Cardholder.PhotoToken) || HasValue(fieldData, token))
            {
                continue;
            }

            var alias = fieldData.FirstOrDefault(kv => IsAliasOf(kv.Key, token) && !string.IsNullOrWhiteSpace(kv.Value));
            if (alias.Key != null)
            {
                fieldData[token] = alias.Value.Trim();
            }
        }

        FillDerivedNames(fieldData);
    }

    /// <summary>
    /// Fills FullName from FirstName + LastName, or FirstName/LastName from FullName, when one side is missing.
    /// The last word of a full name is taken as the last name.
    /// </summary>
    public static void FillDerivedNames(IDictionary<string, string> fieldData)
    {
        bool hasFull = HasValue(fieldData, Cardholder.FullNameToken);
        bool hasFirst = HasValue(fieldData, Cardholder.FirstNameToken);
        bool hasLast = HasValue(fieldData, Cardholder.LastNameToken);

        if (!hasFull && (hasFirst || hasLast))
        {
            fieldData[Cardholder.FullNameToken] =
                $"{Get(fieldData, Cardholder.FirstNameToken)} {Get(fieldData, Cardholder.LastNameToken)}".Trim();
        }
        else if (hasFull && !hasFirst && !hasLast)
        {
            string[] words = Get(fieldData, Cardholder.FullNameToken).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            fieldData[Cardholder.FirstNameToken] = words.Length > 1 ? string.Join(' ', words[..^1]) : words.FirstOrDefault() ?? string.Empty;
            fieldData[Cardholder.LastNameToken] = words.Length > 1 ? words[^1] : string.Empty;
        }
    }

    /// <summary>
    /// True when a column header is one of the recognised spellings of a standard token.
    /// </summary>
    public static bool IsAliasOf(string columnHeader, string token) =>
        Aliases.TryGetValue(token, out var names) && names.Contains(Normalize(columnHeader));

    private static bool HasValue(IDictionary<string, string> data, string key) =>
        data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    private static string Get(IDictionary<string, string> data, string key) =>
        data.TryGetValue(key, out var value) ? value?.Trim() ?? string.Empty : string.Empty;

    // "Job Title", "job_title" and "JOB-TITLE" all read as "jobtitle"
    private static string Normalize(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
