using BadgeForge.Core.Data.Models;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Imports a cardholder roster from a CSV or Excel (.xlsx) file. The first non-empty row holds the column headings.
/// </summary>
public class RosterImportService
{
    private readonly CsvDataIngestionService _csv;
    private readonly XlsxWorksheetReader _xlsx;

    public RosterImportService(CsvDataIngestionService? csv = null, XlsxWorksheetReader? xlsx = null)
    {
        _csv = csv ?? new CsvDataIngestionService();
        _xlsx = xlsx ?? new XlsxWorksheetReader();
    }

    /// <summary>
    /// Reads a roster file as a table: the columns it turned out to have, and one record per data row.
    /// A .xlsx file is read as an Excel workbook. Any other file is recognised by its content — spreadsheet exports
    /// often arrive as .txt, .tmp or with no extension — as a workbook or as CSV text.
    /// </summary>
    /// <exception cref="NotSupportedException">The file is a legacy binary .xls workbook.</exception>
    /// <exception cref="InvalidDataException">A .xlsx file isn't a readable workbook.</exception>
    public RosterTable ReadTable(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Roster file not found at '{filePath}'.", filePath);
        }

        var format = DetectFormat(filePath);
        return format switch
        {
            RosterFileFormat.Xlsx => RosterTableBuilder.Build(NumberRows(_xlsx.ReadFirstWorksheet(filePath)), mapping: null, filePath, format),
            RosterFileFormat.LegacyXls => throw new NotSupportedException(
                "Old-style .xls workbooks can't be read. Save the sheet as .xlsx or CSV and import that."),
            _ => RosterTableBuilder.Build(_csv.ReadRowsFromFile(filePath), mapping: null, filePath, format)
        };
    }

    /// <summary>
    /// Reads every data row of a roster file as a badge record, keyed by its column headings.
    /// </summary>
    /// <inheritdoc cref="ReadTable" path="/exception"/>
    public IReadOnlyList<BadgeRecord> ImportRecords(string filePath) => ReadTable(filePath).Records;

    private static RosterFileFormat DetectFormat(string filePath)
    {
        switch (Path.GetExtension(filePath).ToLowerInvariant())
        {
            case ".xlsx":
                return RosterFileFormat.Xlsx; // A damaged workbook should say so, not be read as CSV
            case ".xls":
                return RosterFileFormat.LegacyXls;
        }

        Span<byte> header = stackalloc byte[4];
        using var stream = File.OpenRead(filePath);
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        return read < header.Length ? RosterFileFormat.Csv
            : header is [0x50, 0x4B, 0x03, 0x04] ? RosterFileFormat.Xlsx       // ZIP container (Office Open XML)
            : header is [0xD0, 0xCF, 0x11, 0xE0] ? RosterFileFormat.LegacyXls  // OLE2 compound file
            : RosterFileFormat.Csv;
    }

    /// <summary>
    /// Reads a roster file as cardholders, with every person selected.
    /// </summary>
    public IReadOnlyList<Cardholder> ImportCardholders(string filePath) =>
        ImportRecords(filePath).Select(record => CardholderMapper.FromRecord(record)).ToList();

    /// <summary>
    /// Turns spreadsheet rows (first non-empty row = headings) into records, the same way CSV rows are read.
    /// </summary>
    public static IReadOnlyList<BadgeRecord> RecordsFromRows(IReadOnlyList<string[]> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return RosterTableBuilder.Build(NumberRows(rows)).Records;
    }

    /// <summary>
    /// Pairs each sheet row with the row number the user sees. The reader emits rows densely from row 1, gaps
    /// included, so the index is the row number.
    /// </summary>
    private static List<RosterRow> NumberRows(IReadOnlyList<string[]> rows) =>
        rows.Select((cells, i) => new RosterRow(i + 1, cells)).ToList();
}
