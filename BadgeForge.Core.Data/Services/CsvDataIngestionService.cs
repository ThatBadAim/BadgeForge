using System.Globalization;
using System.Text;
using BadgeForge.Core.Data.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Reads delimited text (CSV) with CsvHelper. It only splits the file into rows — detecting the heading row,
/// naming columns and sanitising values is <see cref="RosterTableBuilder"/>'s job, shared with the Excel reader.
/// </summary>
public class CsvDataIngestionService
{
    /// <summary>
    /// Reads every row of a CSV file, heading row included, without interpreting any of them.
    /// </summary>
    public IReadOnlyList<RosterRow> ReadRowsFromFile(string filePath, string delimiter = ",")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSV data file not found at '{filePath}'.", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return ReadRowsFromStream(stream, delimiter);
    }

    /// <inheritdoc cref="ReadRowsFromFile"/>
    public IReadOnlyList<RosterRow> ReadRowsFromStream(Stream stream, string delimiter = ",")
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return ReadRows(reader, delimiter);
    }

    /// <inheritdoc cref="ReadRowsFromFile"/>
    public IReadOnlyList<RosterRow> ReadRowsFromText(string csvText, string delimiter = ",")
    {
        ArgumentNullException.ThrowIfNull(csvText);

        using var reader = new StringReader(csvText);
        return ReadRows(reader, delimiter);
    }

    /// <summary>
    /// Ingests records from a CSV file path.
    /// </summary>
    public IReadOnlyList<BadgeRecord> IngestFromFile(string filePath, ColumnMapping? mapping = null, string delimiter = ",") =>
        RosterTableBuilder.Build(ReadRowsFromFile(filePath, delimiter), mapping, filePath).Records;

    /// <summary>
    /// Ingests records from raw CSV text.
    /// </summary>
    public IReadOnlyList<BadgeRecord> IngestFromText(string csvText, ColumnMapping? mapping = null, string delimiter = ",") =>
        RosterTableBuilder.Build(ReadRowsFromText(csvText, delimiter), mapping).Records;

    /// <summary>
    /// Ingests records from an open Stream.
    /// </summary>
    public IReadOnlyList<BadgeRecord> IngestFromStream(Stream stream, ColumnMapping? mapping = null, string delimiter = ",") =>
        RosterTableBuilder.Build(ReadRowsFromStream(stream, delimiter), mapping).Records;

    private static IReadOnlyList<RosterRow> ReadRows(TextReader textReader, string delimiter)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            // Every row is read the same way; which one holds the headings is decided downstream
            HasHeaderRecord = false,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            IgnoreBlankLines = false
        };

        using var parser = new CsvParser(textReader, config);

        var rows = new List<RosterRow>();
        while (parser.Read())
        {
            rows.Add(new RosterRow(parser.Row, parser.Record ?? Array.Empty<string>()));
        }

        return rows;
    }
}
