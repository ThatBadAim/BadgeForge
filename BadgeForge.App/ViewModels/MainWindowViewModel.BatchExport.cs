using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BadgeForge.Core.Rendering.Batch;
using BadgeForge.Core.Rendering.Output;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// Roster batch selection and exporting the selected people's badges to a single multi-page PDF.
/// Rendering runs on worker threads (see <see cref="BatchCardRenderService"/>); this class only captures inputs on
/// the UI thread, reports progress and publishes the outcome.
/// </summary>
public partial class MainWindowViewModel
{
    private readonly BatchPdfExporter _pdfExporter = new();
    private CancellationTokenSource? _exportCts;
    private bool _isExporting;
    private string _exportProgressText = string.Empty;
    private int _exportProgressPercent;

    // --- Batch selection ---

    public void SelectAllPeople() => SetAllPeopleIncluded(true);

    public void ClearPeopleSelection() => SetAllPeopleIncluded(false);

    public void InvertPeopleSelection()
    {
        foreach (var person in People)
        {
            person.IsIncluded = !person.IsIncluded;
        }
    }

    // --- PDF export ---

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                OnPropertyChanged(nameof(CanExportPdf));
            }
        }
    }

    public bool CanExportPdf => !IsExporting && PeopleToPrintCount > 0;

    public string ExportPdfButtonText => PeopleToPrintCount == 1 ? "Export 1 badge to PDF…" : $"Export {PeopleToPrintCount} badges to PDF…";

    /// <summary>
    /// Progress line such as "Rendering 12 of 150…".
    /// </summary>
    public string ExportProgressText
    {
        get => _exportProgressText;
        private set => SetProperty(ref _exportProgressText, value);
    }

    public int ExportProgressPercent
    {
        get => _exportProgressPercent;
        private set => SetProperty(ref _exportProgressPercent, value);
    }

    /// <summary>
    /// Renders every ticked person's badge at 300 DPI into one PDF, a CR80-sized page each, in list order.
    /// Returns the result, or null when there was nothing to export, an export was already running, or it was
    /// cancelled (in which case no file is written).
    /// </summary>
    public async Task<BatchExportResult?> ExportSelectedToPdfAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (IsExporting)
        {
            return null;
        }

        var people = People.Where(p => p.IsIncluded).ToList();
        if (people.Count == 0)
        {
            StatusText = "Tick at least one person to export.";
            return null;
        }

        // Everything the render threads read is captured here, once: a private copy of the design and each person's
        // token values. Editing the design or the list while the export runs can't change the document.
        var template = SnapshotTemplate();
        var jobs = people.Select(p => new CardRenderJob(p.RecordId, p.Name, GetRecordFieldData(p.Record))).ToList();

        var cts = new CancellationTokenSource();
        _exportCts = cts;
        ExportProgressPercent = 0;
        ExportProgressText = new BatchRenderProgress(0, jobs.Count, null).Message;
        IsExporting = true;

        // Progress<T> calls back on the thread that created it (the UI thread in the app). Reports still queued when
        // the run ends are ignored so they can't overwrite the final message.
        var progress = new Progress<BatchRenderProgress>(report =>
        {
            if (ReferenceEquals(_exportCts, cts))
            {
                ExportProgressPercent = report.Percent;
                ExportProgressText = report.Message;
            }
        });

        try
        {
            var result = await _pdfExporter.ExportAsync(template, jobs, filePath, new BatchRenderOptions(), progress, cts.Token);

            ExportProgressPercent = 100;
            ExportProgressText = $"Exported {CountText(result.CardCount, "badge", "badges")}";
            StatusText = $"Exported {CountText(result.CardCount, "badge", "badges")} to {Path.GetFileName(result.OutputPath)}" +
                         (result.Warnings.Count > 0 ? $" with {CountText(result.Warnings.Count, "warning", "warnings")}." : ".");
            return result;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            ExportProgressText = "Export cancelled";
            StatusText = "PDF export cancelled. No file was written.";
            return null;
        }
        catch
        {
            ExportProgressText = "Export failed";
            throw;
        }
        finally
        {
            _exportCts = null;
            cts.Dispose();
            IsExporting = false;
        }
    }

    /// <summary>
    /// Stops a running export; the partly written file is discarded.
    /// </summary>
    public void CancelExport() => _exportCts?.Cancel();
}
