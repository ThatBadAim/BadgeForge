using System.Text;
using BadgeForge.Core.Data.Models;

namespace BadgeForge.Core.Data.Services;

/// <summary>
/// Turns the raw rows of a roster file into a <see cref="RosterTable"/>. The first non-blank row names the columns;
/// every row after it becomes a <see cref="BadgeRecord"/>.
/// </summary>
/// <remarks>
/// All roster sanitising lives here and nowhere else, so a CSV and the same sheet saved as .xlsx import identically:
/// <list type="bullet">
///   <item>headings and values lose leading/trailing whitespace, the BOM and zero-width marks a spreadsheet export
///   leaves on the first heading, and the non-breaking spaces Excel writes in place of ordinary ones;</item>
///   <item>a blank heading becomes <c>Column3</c> (its 1-based position), and a repeated heading becomes
///   <c>Name (2)</c>, so no column is silently swallowed by another;</item>
///   <item>rows shorter than the heading row are padded with empty values, and rows longer than it widen the table —
///   the extra columns are named rather than dropped;</item>
///   <item>entirely empty rows are skipped and counted.</item>
/// </list>
/// Each value is also stored under the heading with spaces and underscores removed ("Full Name" → "FullName"), so a
/// template written against <c>{{FullName}}</c> binds to a roster that spells the column out. Those aliases never
/// overwrite a real column of the same name.
/// </remarks>
public static class RosterTableBuilder
{
    /// <summary>
    /// Builds a table from raw rows.
    /// </summary>
    /// <param name="rows">Every row of the sheet, in file order, including the heading row.</param>
    /// <param name="mapping">Optional column-to-token mapping; each value is additionally stored under its token.</param>
    public static RosterTable Build(
        IReadOnlyList<RosterRow> rows,
        ColumnMapping? mapping = null,
        string sourcePath = "",
        RosterFileFormat format = RosterFileFormat.Csv)
    {
        ArgumentNullException.ThrowIfNull(rows);

        int headerIndex = 0;
        while (headerIndex < rows.Count && IsBlank(rows[headerIndex].Cells))
        {
            headerIndex++;
        }

        if (headerIndex >= rows.Count)
        {
            return RosterTable.Empty with { SourcePath = sourcePath, Format = format };
        }

        var dataRows = rows.Skip(headerIndex + 1).Where(r => !IsBlank(r.Cells)).ToList();
        int width = Math.Max(rows[headerIndex].Cells.Length, dataRows.Count == 0 ? 0 : dataRows.Max(r => r.Cells.Length));

        var generated = new List<string>();
        var columns = BuildColumnNames(rows[headerIndex].Cells, width, generated);

        var records = new List<BadgeRecord>(dataRows.Count);
        foreach (var row in dataRows)
        {
            records.Add(BuildRecord(row, columns, mapping, records.Count));
        }

        return new RosterTable
        {
            Columns = columns,
            Records = records,
            SourcePath = sourcePath,
            Format = format,
            GeneratedColumns = generated,
            SkippedBlankRows = rows.Count - headerIndex - 1 - dataRows.Count
        };
    }

    /// <summary>
    /// Names every column of the table: cleaned headings, invented names for blank ones and for cells that run past
    /// the heading row, and a " (2)" suffix on a heading that repeats.
    /// </summary>
    private static List<string> BuildColumnNames(string[] header, int width, List<string> generated)
    {
        var columns = new List<string>(width);
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < width; i++)
        {
            string name = i < header.Length ? CleanHeading(header[i]) : string.Empty;
            bool invented = name.Length == 0;
            if (invented)
            {
                name = $"Column{i + 1}";
            }

            if (used.TryGetValue(name, out int seen))
            {
                // "Name", "Name" → "Name", "Name (2)". Keep bumping in case "Name (2)" is itself a real heading.
                string candidate;
                do
                {
                    candidate = $"{name} ({++seen})";
                }
                while (used.ContainsKey(candidate));

                used[name] = seen;
                name = candidate;
            }

            used[name] = 1;
            columns.Add(name);
            if (invented)
            {
                generated.Add(name);
            }
        }

        return columns;
    }

    private static BadgeRecord BuildRecord(RosterRow row, List<string> columns, ColumnMapping? mapping, int batchIndex)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Real columns first, so an alias or a mapped token can never overwrite a column the file actually has
        for (int c = 0; c < columns.Count; c++)
        {
            fields[columns[c]] = c < row.Cells.Length ? CleanValue(row.Cells[c]) : string.Empty;
        }

        for (int c = 0; c < columns.Count; c++)
        {
            string value = fields[columns[c]];
            string alias = mapping != null ? mapping.ResolveToken(columns[c]) : Compact(columns[c]);
            if (alias.Length > 0 && !string.Equals(alias, columns[c], StringComparison.OrdinalIgnoreCase))
            {
                fields.TryAdd(alias, value);
            }
        }

        return new BadgeRecord
        {
            RowNumber = row.Number,
            BatchIndex = batchIndex,
            Fields = fields
        };
    }

    private static string Compact(string heading) => heading.Replace(" ", string.Empty).Replace("_", string.Empty);

    private static bool IsBlank(string[] cells) => cells.All(c => CleanValue(c).Length == 0);

    /// <summary>
    /// A heading with its invisible baggage removed and internal runs of whitespace collapsed, so "Employee  Id "
    /// and "Employee Id" are the same column.
    /// </summary>
    private static string CleanHeading(string? raw)
    {
        string value = CleanValue(raw);
        if (!value.Any(char.IsWhiteSpace))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        bool pendingSpace = false;
        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A cell value with the characters a spreadsheet export smuggles in normalised away, then trimmed. Internal
    /// spacing is left alone — an address line's layout is the user's business.
    /// </summary>
    private static string CleanValue(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            switch (c)
            {
                case '﻿': // byte-order mark, left on the first heading by many exporters
                case '​': // zero-width space
                case '‌':
                case '‍':
                    continue;
                case ' ': // non-breaking space — indistinguishable on screen, not to a dictionary lookup
                case ' ':
                case ' ':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString().Trim();
    }
}
