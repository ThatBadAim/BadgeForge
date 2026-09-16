using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Reads the cell text of the first worksheet in an Excel workbook (.xlsx, Office Open XML).
/// Deliberately small and dependency-free: it understands shared strings, inline strings, booleans, numbers and
/// date-formatted numbers — everything a roster sheet holds — and ignores formatting, formulas' definitions and
/// other sheets. Legacy binary .xls files are not supported.
/// </summary>
public class XlsxWorksheetReader
{
    // Built-in number formats that display a date or time (ECMA-376 Part 1, 18.8.30)
    private static readonly HashSet<int> BuiltInDateFormats = new() { 14, 15, 16, 17, 18, 19, 20, 21, 22, 45, 46, 47 };

    /// <summary>
    /// Returns the rows of the first worksheet, top to bottom, each as its cell texts from column A onwards.
    /// Gaps between cells are filled with empty strings; trailing empty rows are dropped.
    /// </summary>
    /// <exception cref="InvalidDataException">The file isn't a readable .xlsx workbook.</exception>
    public IReadOnlyList<string[]> ReadFirstWorksheet(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return ReadFirstWorksheet(stream);
    }

    /// <inheritdoc cref="ReadFirstWorksheet(string)"/>
    public IReadOnlyList<string[]> ReadFirstWorksheet(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sharedStrings = ReadSharedStrings(zip);
            var dateStyles = ReadDateStyleIndexes(zip);
            string sheetPath = FindFirstSheetPath(zip);

            var sheetEntry = zip.GetEntry(sheetPath) ?? throw new InvalidDataException("The workbook has no worksheets.");
            using var sheetStream = sheetEntry.Open();
            return ReadRows(sheetStream, sharedStrings, dateStyles);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException and not FileNotFoundException)
        {
            throw new InvalidDataException($"This file isn't a readable Excel workbook (.xlsx): {ex.Message}", ex);
        }
    }

    private static List<string[]> ReadRows(Stream sheetStream, IReadOnlyList<string> sharedStrings, HashSet<int> dateStyles)
    {
        var rows = new SortedDictionary<int, SortedDictionary<int, string>>();
        int nextRow = 1;

        var sheet = XDocument.Load(sheetStream);
        foreach (var row in sheet.Descendants().Where(e => e.Name.LocalName == "row"))
        {
            int rowIndex = int.TryParse(row.Attribute("r")?.Value, out int r) ? r : nextRow;
            nextRow = rowIndex + 1;

            var cells = new SortedDictionary<int, string>();
            int nextColumn = 0;
            foreach (var cell in row.Elements().Where(e => e.Name.LocalName == "c"))
            {
                int column = ColumnIndex(cell.Attribute("r")?.Value) ?? nextColumn;
                nextColumn = column + 1;
                cells[column] = CellText(cell, sharedStrings, dateStyles);
            }

            if (cells.Values.Any(v => v.Length > 0))
            {
                rows[rowIndex] = cells;
            }
        }

        var result = new List<string[]>();
        if (rows.Count == 0)
        {
            return result;
        }

        // Keep blank rows between data rows so row numbers stay meaningful to the caller
        for (int rowIndex = 1; rowIndex <= rows.Keys.Max(); rowIndex++)
        {
            if (!rows.TryGetValue(rowIndex, out var cells))
            {
                result.Add(Array.Empty<string>());
                continue;
            }

            var values = new string[cells.Keys.Max() + 1];
            Array.Fill(values, string.Empty);
            foreach (var (column, text) in cells)
            {
                values[column] = text;
            }

            result.Add(values);
        }

        return result;
    }

    private static string CellText(XElement cell, IReadOnlyList<string> sharedStrings, HashSet<int> dateStyles)
    {
        string type = cell.Attribute("t")?.Value ?? "n";
        string? raw = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value;

        switch (type)
        {
            case "s":
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < sharedStrings.Count
                    ? sharedStrings[index]
                    : string.Empty;

            case "inlineStr":
                return TextOf(cell.Elements().FirstOrDefault(e => e.Name.LocalName == "is"));

            case "b":
                return raw == "1" ? "TRUE" : raw == "0" ? "FALSE" : raw ?? string.Empty;

            case "str":
            case "e":
                return raw ?? string.Empty;

            default:
                if (raw == null)
                {
                    return string.Empty;
                }

                bool isDate = int.TryParse(cell.Attribute("s")?.Value, out int style) && dateStyles.Contains(style);
                if (isDate && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial)
                    && serial is > -657435.0 and < 2958466.0)
                {
                    var date = DateTime.FromOADate(serial);
                    return date.TimeOfDay == TimeSpan.Zero
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                }

                // Excel stores "12" as "12" but 0.1 as "0.10000000000000001"; show what the user typed
                return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    ? number.ToString("G15", CultureInfo.InvariantCulture)
                    : raw;
        }
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var strings = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return strings;
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        strings.AddRange(doc.Root?.Elements().Where(e => e.Name.LocalName == "si").Select(TextOf) ?? Enumerable.Empty<string>());
        return strings;
    }

    /// <summary>
    /// Indexes into the cell style list whose number format shows a date.
    /// </summary>
    private static HashSet<int> ReadDateStyleIndexes(ZipArchive zip)
    {
        var dateStyles = new HashSet<int>();
        var entry = zip.GetEntry("xl/styles.xml");
        if (entry == null)
        {
            return dateStyles;
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);

        var customDateFormats = doc.Descendants()
            .Where(e => e.Name.LocalName == "numFmt")
            .Where(e => LooksLikeDateFormat(e.Attribute("formatCode")?.Value))
            .Select(e => int.TryParse(e.Attribute("numFmtId")?.Value, out int id) ? id : -1)
            .ToHashSet();

        var cellXfs = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "cellXfs");
        int styleIndex = 0;
        foreach (var xf in cellXfs?.Elements().Where(e => e.Name.LocalName == "xf") ?? Enumerable.Empty<XElement>())
        {
            if (int.TryParse(xf.Attribute("numFmtId")?.Value, out int formatId)
                && (BuiltInDateFormats.Contains(formatId) || customDateFormats.Contains(formatId)))
            {
                dateStyles.Add(styleIndex);
            }

            styleIndex++;
        }

        return dateStyles;
    }

    private static bool LooksLikeDateFormat(string? formatCode)
    {
        if (string.IsNullOrEmpty(formatCode))
        {
            return false;
        }

        // Ignore quoted literals and [colour]/[locale] sections, then look for day, month, year or hour codes
        bool inQuotes = false, inBrackets = false;
        foreach (char c in formatCode)
        {
            switch (c)
            {
                case '"': inQuotes = !inQuotes; continue;
                case '[': inBrackets = true; continue;
                case ']': inBrackets = false; continue;
            }

            if (!inQuotes && !inBrackets && "dmyhDMYH".Contains(c))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindFirstSheetPath(ZipArchive zip)
    {
        const string fallback = "xl/worksheets/sheet1.xml";

        var workbookEntry = zip.GetEntry("xl/workbook.xml");
        var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry == null || relsEntry == null)
        {
            return fallback;
        }

        XDocument workbook, rels;
        using (var s = workbookEntry.Open()) workbook = XDocument.Load(s);
        using (var s = relsEntry.Open()) rels = XDocument.Load(s);

        string? relationshipId = workbook.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "sheet")?
            .Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;

        string? target = rels.Descendants()
            .Where(e => e.Name.LocalName == "Relationship")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == relationshipId)?
            .Attribute("Target")?.Value;

        if (string.IsNullOrEmpty(target))
        {
            return fallback;
        }

        // Targets are relative to xl/ unless absolute within the package
        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
    }

    private static string TextOf(XElement? element) =>
        element == null
            ? string.Empty
            : string.Concat(element.Descendants()
                // Phonetic guides (<rPh>) repeat the text in another script; skip them
                .Where(e => e.Name.LocalName == "t" && e.Ancestors().All(a => a.Name.LocalName != "rPh"))
                .Select(e => e.Value));

    /// <summary>
    /// Zero-based column of a cell reference: "A1" → 0, "AB12" → 27.
    /// </summary>
    internal static int? ColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
        {
            return null;
        }

        int column = 0;
        int letters = 0;
        foreach (char c in cellReference)
        {
            if (c is >= 'A' and <= 'Z')
            {
                column = column * 26 + (c - 'A' + 1);
                letters++;
            }
            else if (c is >= 'a' and <= 'z')
            {
                column = column * 26 + (c - 'a' + 1);
                letters++;
            }
            else
            {
                break;
            }
        }

        return letters == 0 ? null : column - 1;
    }
}
