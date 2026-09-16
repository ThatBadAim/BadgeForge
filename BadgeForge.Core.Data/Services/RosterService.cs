using System.Globalization;
using System.Text;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Templates.Storage;
using CsvHelper;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Saves an ordered people list (badge records) to CSV with each person's photo file, and restores
/// photos when such a list is read back, so a print list built by hand can be reopened later.
/// </summary>
public class RosterService
{
    /// <summary>
    /// Column holding each person's photo file path.
    /// </summary>
    public const string PhotoColumn = "Photo";

    /// <summary>
    /// Writes the records, in order, to a CSV file with the given field columns followed by a Photo column.
    /// </summary>
    public void SaveToFile(string filePath, IEnumerable<BadgeRecord> records, IReadOnlyList<string> fieldColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        AtomicFileWriter.WriteAllText(filePath, SaveToText(records, fieldColumns), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Formats the records, in order, as CSV text with the given field columns followed by a Photo column.
    /// </summary>
    public string SaveToText(IEnumerable<BadgeRecord> records, IReadOnlyList<string> fieldColumns)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fieldColumns);

        var columns = fieldColumns
            .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, PhotoColumn, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var column in columns)
        {
            csv.WriteField(column);
        }
        csv.WriteField(PhotoColumn);
        csv.NextRecord();

        foreach (var record in records)
        {
            foreach (var column in columns)
            {
                csv.WriteField(record.GetValue(column) ?? string.Empty);
            }
            csv.WriteField(record.ResolvedPhotoPath ?? record.GetValue(PhotoColumn) ?? string.Empty);
            csv.NextRecord();
        }

        csv.Flush();
        return writer.ToString();
    }

    /// <summary>
    /// Points each record at the photo file named in its photo column (falling back to the "Photo" column)
    /// when that file exists, either as an absolute path or relative to <paramref name="baseDirectory"/>.
    /// Records whose file can't be found are left unchanged. Returns how many photos were found.
    /// </summary>
    public int ResolvePhotoColumn(IEnumerable<BadgeRecord> records, string? baseDirectory, string photoColumn = PhotoColumn)
    {
        ArgumentNullException.ThrowIfNull(records);

        int found = 0;
        foreach (var record in records)
        {
            string? value = record.GetValue(photoColumn);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = record.GetValue(PhotoColumn);
            }

            value = value?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            string? path = Path.IsPathRooted(value)
                ? value
                : string.IsNullOrEmpty(baseDirectory) ? null : Path.Combine(baseDirectory, value);

            if (path != null && File.Exists(path))
            {
                record.ResolvedPhotoPath = Path.GetFullPath(path);
                found++;
            }
        }

        return found;
    }

    /// <summary>
    /// Guesses a person's name from a photo file name: "jane_smith.jpg" becomes ("Jane", "Smith").
    /// The last word is the last name; a single word is treated as the first name.
    /// </summary>
    public static (string FirstName, string LastName) GuessNameFromFileName(string filePath)
    {
        string stem = Path.GetFileNameWithoutExtension(filePath ?? string.Empty);
        string[] words = stem
            .Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(TidyCase)
            .ToArray();

        return words.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (words[0], string.Empty),
            _ => (string.Join(' ', words[..^1]), words[^1])
        };
    }

    private static string TidyCase(string word)
    {
        // Mixed-case words such as "McDonald" are already deliberate; only tidy all-lower or ALL-CAPS words
        bool hasLower = word.Any(char.IsLower);
        bool hasUpper = word.Any(char.IsUpper);
        if (hasLower && hasUpper)
        {
            return word;
        }

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }
}
