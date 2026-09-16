using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Interfaces;

/// <summary>
/// Event arguments delivered when printer state changes.
/// </summary>
public class PrinterStatusChangedEventArgs : EventArgs
{
    public PrinterStatus PreviousStatus { get; }
    public PrinterStatus CurrentStatus { get; }

    public PrinterStatusChangedEventArgs(PrinterStatus previousStatus, PrinterStatus currentStatus)
    {
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
    }
}

/// <summary>
/// Monitors physical printer status, input/output hopper capacity, and spooler queue state.
/// Supports native vendor SDK querying when available, with automatic fallback to Windows spooler polling.
/// </summary>
public interface ICardPrinterStatusMonitor : IDisposable
{
    /// <summary>
    /// Most recently polled status snapshot.
    /// </summary>
    PrinterStatus CurrentStatus { get; }

    /// <summary>
    /// Raised whenever a state change is detected during polling or via hardware event.
    /// </summary>
    event EventHandler<PrinterStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Polls the printer or spooler for an immediate status update.
    /// </summary>
    Task<PrinterStatus> PollStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts background polling at the specified interval.
    /// </summary>
    void StartMonitoring(TimeSpan interval);

    /// <summary>
    /// Stops background monitoring.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// True if background monitoring is active.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Acknowledges an operator pause and resets the pause counter (e.g. after emptying output hopper).
    /// </summary>
    void ResetBatchPauseCounter();

    /// <summary>
    /// Manually notifies the monitor that a physical card has ejected (for tracking output hopper limit).
    /// </summary>
    void RecordCardEjected();

    /// <summary>
    /// Called when the operator has dealt with a fault and pressed Resume. Real hardware reports its own recovery,
    /// so by default this does nothing; simulated monitors use it to clear the simulated fault.
    /// </summary>
    void AcknowledgeOperatorIntervention()
    {
    }
}
