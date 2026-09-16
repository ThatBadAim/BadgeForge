using Avalonia.Controls;
using Avalonia.Interactivity;
using BadgeForge.App.ViewModels;

namespace BadgeForge.App.Views;

/// <summary>
/// Shows what a roster file was found to contain — its columns, its row count and a short preview — and asks whether
/// to add it to the print list or replace the list with it. Closing the window any other way cancels the import.
/// </summary>
public partial class ImportSummaryDialog : Window
{
    private ImportChoice _choice = ImportChoice.Cancel;

    public ImportSummaryDialog()
    {
        InitializeComponent();
    }

    public ImportSummaryDialog(ImportSummaryViewModel summary) : this()
    {
        DataContext = summary;
    }

    /// <summary>
    /// Shows the dialog and returns what the user chose.
    /// </summary>
    public static async System.Threading.Tasks.Task<ImportChoice> AskAsync(Window owner, ImportSummaryViewModel summary)
    {
        var dialog = new ImportSummaryDialog(summary);
        await dialog.ShowDialog(owner);
        return dialog._choice;
    }

    private void OnAppendClick(object? sender, RoutedEventArgs e) => Complete(ImportChoice.Append);

    private void OnReplaceClick(object? sender, RoutedEventArgs e) => Complete(ImportChoice.Replace);

    private void Complete(ImportChoice choice)
    {
        _choice = choice;
        Close();
    }
}
