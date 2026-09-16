using System;
using System.IO;
using System.Linq;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// The parser service: what a roster file is detected to contain, and how messy rows are cleaned up on the way in.
/// </summary>
public class RosterTableImportTests
{
    private static RosterTable ReadCsv(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, text);
        try
        {
            return new RosterImportService().ReadTable(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadTable_DetectsColumnsAndRows()
    {
        var table = ReadCsv("""
            First Name,Last Name,ID Number,QR Data,Clearance
            Ada,Lovelace,1001,BF-1001,Level 3
            Grace,Hopper,1002,BF-1002,Level 4
            """);

        Assert.Equal(new[] { "First Name", "Last Name", "ID Number", "QR Data", "Clearance" }, table.Columns);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(5, table.ColumnCount);
        Assert.Equal(RosterFileFormat.Csv, table.Format);
        Assert.EndsWith(".csv", table.SourceName);

        // Values are reachable both by the heading and by its no-spaces form, so {{FirstName}} binds either way
        Assert.Equal("Ada", table.Records[0].GetValue("First Name"));
        Assert.Equal("Ada", table.Records[0].GetValue("FirstName"));
        Assert.Equal("BF-1002", table.Records[1].GetValue("QRData"));
    }

    [Fact]
    public void ReadTable_TrimsWhitespaceAndNormalisesInvisibleCharacters()
    {
        // A BOM on the first heading, a non-breaking space in the second, and padding everywhere
        var table = ReadCsv("﻿First Name , Role ,  ID \n  Ada  ,\tEngineer\t, 1001 \n");

        Assert.Equal(new[] { "First Name", "Role", "ID" }, table.Columns);
        var record = Assert.Single(table.Records);
        Assert.Equal("Ada", record.GetValue("First Name"));
        Assert.Equal("Engineer", record.GetValue("Role"));
        Assert.Equal("1001", record.GetValue("ID"));
    }

    [Fact]
    public void ReadTable_PadsShortRowsAndKeepsCellsThatRunPastTheHeadings()
    {
        var table = ReadCsv("""
            Name,Role
            Ada
            Grace,Admiral,USN,Yale
            """);

        // The widest row decides the table's width; the extra columns are named rather than dropped
        Assert.Equal(new[] { "Name", "Role", "Column3", "Column4" }, table.Columns);
        Assert.Equal(new[] { "Column3", "Column4" }, table.GeneratedColumns);

        Assert.Equal(string.Empty, table.Records[0].GetValue("Role"));
        Assert.Equal(string.Empty, table.Records[0].GetValue("Column4"));
        Assert.Equal("USN", table.Records[1].GetValue("Column3"));
        Assert.Equal("Yale", table.Records[1].GetValue("Column4"));
    }

    [Fact]
    public void ReadTable_NamesBlankAndRepeatedHeadings()
    {
        var table = ReadCsv("""
            Name,,Name,Name
            Ada,x,y,z
            """);

        Assert.Equal(new[] { "Name", "Column2", "Name (2)", "Name (3)" }, table.Columns);
        Assert.Contains("Column2", table.GeneratedColumns);

        // Each column keeps its own value instead of the last one winning
        var record = Assert.Single(table.Records);
        Assert.Equal("Ada", record.GetValue("Name"));
        Assert.Equal("x", record.GetValue("Column2"));
        Assert.Equal("y", record.GetValue("Name (2)"));
        Assert.Equal("z", record.GetValue("Name (3)"));
    }

    [Fact]
    public void ReadTable_SkipsBlankRowsAndCountsThem()
    {
        var table = ReadCsv("""

            Name,Role
            Ada,Engineer

            ,
            Grace,Admiral
            """);

        Assert.Equal(new[] { "Name", "Role" }, table.Columns);
        Assert.Equal(new[] { "Ada", "Grace" }, table.Records.Select(r => r.GetValue("Name")));
        Assert.Equal(2, table.SkippedBlankRows);

        // Row numbers stay the ones the user sees in their spreadsheet
        Assert.Equal(3, table.Records[0].RowNumber);
        Assert.Equal(6, table.Records[1].RowNumber);
        Assert.Equal(new[] { 0, 1 }, table.Records.Select(r => r.BatchIndex));
    }

    [Fact]
    public void ReadTable_FileWithHeadingsButNoData_ReportsColumnsAndNoRows()
    {
        var table = ReadCsv("Name,Role\n");

        Assert.Equal(new[] { "Name", "Role" }, table.Columns);
        Assert.Equal(0, table.RowCount);
        Assert.Empty(table.PreviewRows());
    }

    [Fact]
    public void ReadTable_EmptyFile_IsAnEmptyTable()
    {
        var table = ReadCsv("");

        Assert.Empty(table.Columns);
        Assert.Empty(table.Records);
    }

    [Fact]
    public void PreviewRows_ReturnsAtMostThreeRowsInColumnOrder()
    {
        var table = ReadCsv("""
            Name,Role
            Ada,Engineer
            Grace,Admiral
            Katherine,Mathematician
            Margaret,Director
            """);

        var preview = table.PreviewRows(3);

        Assert.Equal(3, preview.Count);
        Assert.All(preview, row => Assert.Equal(table.ColumnCount, row.Count));
        Assert.Equal(new[] { "Ada", "Engineer" }, preview[0]);
        Assert.Equal(new[] { "Katherine", "Mathematician" }, preview[2]);
    }

    [Fact]
    public void PreviewRows_FillsMissingCellsSoTheGridStaysRectangular()
    {
        var table = ReadCsv("Name,Role,Site\nAda\n");

        var row = Assert.Single(table.PreviewRows());
        Assert.Equal(new[] { "Ada", string.Empty, string.Empty }, row);
    }

    [Fact]
    public void ReadTable_QuotedFieldsKeepTheirCommasAndNewlines()
    {
        var table = ReadCsv("Name,Notes\n\"Hopper, Grace\",\"line one\nline two\"\nAda,x\n");

        Assert.Equal(2, table.RowCount);
        Assert.Equal("Hopper, Grace", table.Records[0].GetValue("Name"));
        Assert.Contains("line two", table.Records[0].GetValue("Notes"));

        // A record that spanned two physical lines doesn't put the next one's row number out
        Assert.Equal("Ada", table.Records[1].GetValue("Name"));
    }

    [Fact]
    public void Build_ExcelAndCsvRowsProduceTheSameTable()
    {
        var rows = new[]
        {
            new[] { " Full Name ", "Job Title" },
            new[] { "Ada Lovelace", "Analyst" },
            new[] { "Grace Hopper" }
        };

        var fromSheet = RosterTableBuilder.Build(
            rows.Select((cells, i) => new RosterRow(i + 1, cells)).ToList(),
            format: RosterFileFormat.Xlsx);
        var fromCsv = ReadCsv("Full Name,Job Title\nAda Lovelace,Analyst\nGrace Hopper\n");

        Assert.Equal(fromCsv.Columns, fromSheet.Columns);
        Assert.Equal(
            fromCsv.Records.Select(r => r.GetValue("FullName")),
            fromSheet.Records.Select(r => r.GetValue("FullName")));
        Assert.Equal(string.Empty, fromSheet.Records[1].GetValue("Job Title"));
    }
}
