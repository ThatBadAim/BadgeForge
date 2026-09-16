using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App.ViewModels;
using BadgeForge.App.Views;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using Xunit;

namespace BadgeForge.UiTests;

/// <summary>
/// Renders the real import summary dialog, so its bindings are proved against the view model rather than assumed.
/// </summary>
public class ImportSummaryDialogUiTests
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

    private static IEnumerable<string> VisibleText(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text ?? string.Empty);

    [Fact]
    public Task Dialog_ShowsTheColumnsRowCountAndPreview() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var table = Read("""
            Name,Role,ID Number,QR Data
            Ada,Engineer,1001,BF-1001
            Grace,Admiral,1002,BF-1002
            Katherine,Mathematician,1003,BF-1003
            Margaret,Director,1004,BF-1004
            """);

        var dialog = new ImportSummaryDialog(new ImportSummaryViewModel(table, existingPeopleCount: 7));
        dialog.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var text = VisibleText(dialog).ToList();

            Assert.Contains("4 people · 4 columns", text);
            Assert.Contains("First 3 of 4 rows:", text);

            // Column headings appear as chips and again above the preview
            Assert.Equal(2, text.Count(t => t == "QR Data"));

            // Three preview rows, the fourth person left out
            Assert.Contains("Ada", text);
            Assert.Contains("Katherine", text);
            Assert.DoesNotContain("Margaret", text);

            var buttons = dialog.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible).ToList();
            Assert.Contains(buttons, b => (b.Content as string) == "Add to the 7 people already listed");
            Assert.Contains(buttons, b => (b.Content as string) == "Replace the list");
        }
        finally
        {
            dialog.Close();
        }
    });

    [Fact]
    public Task Dialog_WithNoListLoaded_OffersOnlyImport() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var dialog = new ImportSummaryDialog(new ImportSummaryViewModel(Read("Name\nAda\n"), existingPeopleCount: 0));
        dialog.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var buttons = dialog.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible).ToList();

            Assert.Contains(buttons, b => (b.Content as string) == "Import");
            Assert.DoesNotContain(buttons, b => (b.Content as string)?.StartsWith("Add to", StringComparison.Ordinal) == true);
            Assert.Contains("Every row:", VisibleText(dialog));
        }
        finally
        {
            dialog.Close();
        }
    });

    [Fact]
    public Task Dialog_WithNothingToImport_DisablesTheImportButton() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var dialog = new ImportSummaryDialog(new ImportSummaryViewModel(Read("Name,Role\n"), existingPeopleCount: 3));
        dialog.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();

            var import = dialog.GetVisualDescendants().OfType<Button>().Single(b => (b.Content as string) == "Import");
            Assert.False(import.IsEnabled);
            Assert.Contains("No data rows were found under the headings.", VisibleText(dialog));
        }
        finally
        {
            dialog.Close();
        }
    });
}
