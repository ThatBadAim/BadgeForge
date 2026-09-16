namespace BadgeForge.Core.Data.Models;

/// <summary>
/// How a roster file was read.
/// </summary>
public enum RosterFileFormat
{
    Csv,
    Xlsx,
    LegacyXls
}

/// <summary>
/// One raw row of a roster file: its cells, and the row number the user sees in their spreadsheet.
/// </summary>
public readonly record struct RosterRow(int Number, string[] Cells);

/// <summary>
/// A parsed roster file: the columns detected in it and one record per data row.
/// </summary>
/// <remarks>
/// <see cref="Columns"/> is the file's own column order — it is part of the data schema, and every record keys its
/// values by these names. The People grid's display order is separate view state and never feeds back into here.
/// </remarks>
public sealed record RosterTable
{
    public static readonly RosterTable Empty = new();

    /// <summary>
    /// Detected column headings, in file order, de-duplicated and with blanks filled in.
    /// </summary>
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// One record per data row, values keyed by <see cref="Columns"/>.
    /// </summary>
    public IReadOnlyList<BadgeRecord> Records { get; init; } = Array.Empty<BadgeRecord>();

    /// <summary>
    /// The file the table was read from, when it came from one.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    public RosterFileFormat Format { get; init; } = RosterFileFormat.Csv;

    /// <summary>
    /// Columns the parser had to name itself: blank headings, and cells that ran past the heading row.
    /// </summary>
    public IReadOnlyList<string> GeneratedColumns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// How many rows were dropped for being entirely empty.
    /// </summary>
    public int SkippedBlankRows { get; init; }

    public int RowCount => Records.Count;

    public int ColumnCount => Columns.Count;

    public string SourceName => string.IsNullOrEmpty(SourcePath) ? string.Empty : Path.GetFileName(SourcePath);

    /// <summary>
    /// The first few rows as a rectangular grid of cell text, one value per column in <see cref="Columns"/> order,
    /// for showing the user what the file looks like before they commit to importing it.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> PreviewRows(int maxRows = 3)
    {
        var preview = new List<IReadOnlyList<string>>(Math.Min(maxRows, Records.Count));
        foreach (var record in Records.Take(Math.Max(0, maxRows)))
        {
            preview.Add(Columns.Select(c => record.GetValue(c) ?? string.Empty).ToList());
        }

        return preview;
    }
}
