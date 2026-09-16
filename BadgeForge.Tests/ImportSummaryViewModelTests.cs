using System;
using System.IO;
using System.Linq;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// What the import summary tells the user before a roster is allowed anywhere near the print list.
/// </summary>
public class ImportSummaryViewModelTests
{
    private static RosterTable Read(string csv)
    {
        string path = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, csv);
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
    public void Summary_ReportsColumnsRowCountAndAThreeRowPreview()
    {
        var summary = new ImportSummaryViewModel(Read("""
            Name,Role,ID Number,QR Data
            Ada,Engineer,1001,BF-1001
            Grace,Admiral,1002,BF-1002
            Katherine,Mathematician,1003,BF-1003
            Margaret,Director,1004,BF-1004
            """), existingPeopleCount: 0);

        Assert.Equal(new[] { "Name", "Role", "ID Number", "QR Data" }, summary.Columns.Select(c => c.Name));
        Assert.DoesNotContain(summary.Columns, c => c.IsGenerated);
        Assert.Equal("4 people · 4 columns", summary.HeadlineText);

        Assert.Equal(3, summary.PreviewRows.Count);
        Assert.Equal("First 3 of 4 rows:", summary.PreviewCaption);
        Assert.Equal(new[] { "Ada", "Engineer", "1001", "BF-1001" }, summary.PreviewRows[0].Cells.Select(c => c.Text));
        Assert.Equal("2", summary.PreviewRows[0].Number);
        Assert.All(summary.PreviewRows, r => Assert.Equal(summary.Columns.Count, r.Cells.Count));
        Assert.Null(summary.NoteText);
    }

    [Fact]
    public void Summary_ShortFile_SaysTheWholeFileIsShown()
    {
        var summary = new ImportSummaryViewModel(Read("Name\nAda\nGrace\n"), existingPeopleCount: 0);

        Assert.Equal("Every row:", summary.PreviewCaption);
        Assert.Equal("2 people · 1 column", summary.HeadlineText);
    }

    [Fact]
    public void Summary_FlagsInventedColumnsAndSkippedRows()
    {
        var summary = new ImportSummaryViewModel(Read("Name,\nAda,x\n\n\nGrace,y\n"), existingPeopleCount: 0);

        Assert.Contains(summary.Columns, c => c is { Name: "Column2", IsGenerated: true });
        Assert.True(summary.HasNote);
        Assert.Equal("1 column had no heading and was named by position · 2 empty rows skipped.", summary.NoteText);
    }

    [Fact]
    public void Summary_OffersToAppendOnlyWhenThereIsAlreadyAList()
    {
        var table = Read("Name\nAda\n");

        var toEmpty = new ImportSummaryViewModel(table, existingPeopleCount: 0);
        Assert.False(toEmpty.CanAppend);
        Assert.Equal("Import", toEmpty.ReplaceButtonText);

        var toExisting = new ImportSummaryViewModel(table, existingPeopleCount: 12);
        Assert.True(toExisting.CanAppend);
        Assert.Equal("Add to the 12 people already listed", toExisting.AppendButtonText);
        Assert.Equal("Replace the list", toExisting.ReplaceButtonText);
    }

    [Fact]
    public void Summary_HeadingsButNoRows_OffersNothingToImport()
    {
        var summary = new ImportSummaryViewModel(Read("Name,Role\n"), existingPeopleCount: 5);

        Assert.False(summary.HasRows);
        Assert.False(summary.HasPreview);
        Assert.False(summary.CanAppend);
        Assert.Equal(2, summary.Columns.Count);
    }

    [Fact]
    public void Summary_EmptyCellsAreMarkedSoThePreviewCanDimThem()
    {
        var summary = new ImportSummaryViewModel(Read("Name,Role\nAda,\n"), existingPeopleCount: 0);

        var cells = summary.PreviewRows.Single().Cells;
        Assert.False(cells[0].IsEmpty);
        Assert.True(cells[1].IsEmpty);
    }
}
