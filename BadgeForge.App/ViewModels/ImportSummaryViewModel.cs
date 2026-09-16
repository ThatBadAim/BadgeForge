using System;
using System.Collections.Generic;
using System.Linq;
using BadgeForge.Core.Data.Models;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// What the user chose to do with a roster file they were shown a summary of.
/// </summary>
public enum ImportChoice
{
    Cancel,
    Append,
    Replace
}

/// <summary>
/// One cell of the import preview table.
/// </summary>
public sealed record ImportPreviewCell(string Text)
{
    public bool IsEmpty => Text.Length == 0;
}

/// <summary>
/// One row of the import preview table, numbered as the user's spreadsheet numbers it.
/// </summary>
public sealed record ImportPreviewRow(string Number, IReadOnlyList<ImportPreviewCell> Cells);

/// <summary>
/// A detected column, shown as a chip in the import summary.
/// </summary>
/// <param name="Name">The column heading as it will be stored on every record.</param>
/// <param name="IsGenerated">True when the parser had to name the column itself (blank or missing heading).</param>
public sealed record ImportColumnSummary(string Name, bool IsGenerated);

/// <summary>
/// Read-only description of a roster file for the import summary dialog: what was detected in it, a short preview,
/// and what appending it would do to the list already loaded.
/// </summary>
public sealed class ImportSummaryViewModel
{
    /// <summary>How many rows the preview table shows.</summary>
    public const int PreviewRowCount = 3;

    public ImportSummaryViewModel(RosterTable table, int existingPeopleCount)
    {
        ArgumentNullException.ThrowIfNull(table);

        Table = table;
        ExistingPeopleCount = Math.Max(0, existingPeopleCount);

        var generated = new HashSet<string>(table.GeneratedColumns, StringComparer.OrdinalIgnoreCase);
        Columns = table.Columns.Select(c => new ImportColumnSummary(c, generated.Contains(c))).ToList();

        PreviewRows = table.PreviewRows(PreviewRowCount)
            .Select((values, i) => new ImportPreviewRow(
                table.Records[i].RowNumber.ToString(),
                values.Select(v => new ImportPreviewCell(v)).ToList()))
            .ToList();

        NoteText = BuildNote();
    }

    public RosterTable Table { get; }

    public int ExistingPeopleCount { get; }

    public string FileName => string.IsNullOrEmpty(Table.SourceName) ? "Roster" : Table.SourceName;

    public string FilePath => Table.SourcePath;

    public string FormatText => Table.Format == RosterFileFormat.Xlsx ? "Excel workbook" : "CSV spreadsheet";

    public IReadOnlyList<ImportColumnSummary> Columns { get; }

    public IReadOnlyList<ImportPreviewRow> PreviewRows { get; }

    public bool HasRows => Table.RowCount > 0;

    public bool HasPreview => PreviewRows.Count > 0;

    public string HeadlineText =>
        $"{Count(Table.RowCount, "person", "people")} · {Count(Table.ColumnCount, "column", "columns")}";

    public string PreviewCaption => Table.RowCount <= PreviewRows.Count
        ? "Every row:"
        : $"First {PreviewRows.Count} of {Table.RowCount} rows:";

    /// <summary>
    /// Anything worth warning about before the file lands on the list — nothing to say is the normal case.
    /// </summary>
    public string? NoteText { get; }

    public bool HasNote => NoteText != null;

    /// <summary>
    /// How wide the preview grid needs to be for its columns to stay readable. Past that the preview scrolls
    /// sideways rather than squeezing twenty columns into the dialog.
    /// </summary>
    public double PreviewWidth => RowNumberColumnWidth + (MinPreviewColumnWidth * Math.Max(1, Table.ColumnCount));

    private const double RowNumberColumnWidth = 40;
    private const double MinPreviewColumnWidth = 120;

    private string? BuildNote()
    {
        var notes = new List<string>();
        int generated = Columns.Count(c => c.IsGenerated);
        if (generated > 0)
        {
            notes.Add($"{Count(generated, "column", "columns")} had no heading and {(generated == 1 ? "was" : "were")} named by position");
        }

        if (Table.SkippedBlankRows > 0)
        {
            notes.Add($"{Count(Table.SkippedBlankRows, "empty row", "empty rows")} skipped");
        }

        if (notes.Count == 0)
        {
            return null;
        }

        string text = string.Join(" · ", notes);
        return char.ToUpperInvariant(text[0]) + text[1..] + ".";
    }

    /// <summary>
    /// Adding to the list only makes sense when there is a list to add to.
    /// </summary>
    public bool CanAppend => ExistingPeopleCount > 0 && HasRows;

    public string AppendButtonText => $"Add to the {Count(ExistingPeopleCount, "person", "people")} already listed";

    public string ReplaceButtonText => CanAppend ? "Replace the list" : "Import";

    private static string Count(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count:N0} {plural}";
}
