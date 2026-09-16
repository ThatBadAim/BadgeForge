using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Drivers;

/// <summary>
/// In-memory mock status monitor allowing unit tests and UI to simulate physical printer states,
/// hopper fullness, ribbon depletion, and operator pauses without hardware.
/// </summary>
public class MockStatusMonitor : ICardPrinterStatusMonitor
{
    private readonly object _lock = new();
    private PrinterStatus _currentStatus;
    private bool _isMonitoring;

    public const int MandatoryPauseThreshold = 20;
    public const int MaxOutputHopperCapacity = 25;
    public const int MaxInputHopperCapacity = 80;

    public PrinterStatus CurrentStatus
    {
        get
        {
            lock (_lock)
            {
                return _currentStatus;
            }
        }
    }

    public event EventHandler<PrinterStatusChangedEventArgs>? StatusChanged;
    public bool IsMonitoring => _isMonitoring;

    public MockStatusMonitor(int initialInputHopper = 80, int initialRibbonPercent = 100)
    {
        _currentStatus = new PrinterStatus
        {
            State = PrinterState.Ready,
            StatusMessage = "Mock printer ready",
            InputHopperCount = initialInputHopper,
            OutputHopperCount = 0,
            CardsPrintedSincePause = 0,
            RibbonRemainingPercent = initialRibbonPercent,
            IsNativeStatus = true,
            TimestampUtc = DateTimeOffset.UtcNow
        };
    }

    public Task<PrinterStatus> PollStatusAsync(CancellationToken ct = default)
    {
        return Task.FromResult(CurrentStatus);
    }

    public void RecordCardEjected()
    {
        Update(status =>
        {
            int newPrinted = status.CardsPrintedSincePause + 1;
            int newOutput = status.OutputHopperCount + 1;
            int newInput = Math.Max(0, (status.InputHopperCount ?? MaxInputHopperCapacity) - 1);
            int newRibbon = Math.Max(0, (status.RibbonRemainingPercent ?? 100) - 1);

            PrinterState newState = status.State;
            string message = status.StatusMessage;

            if (newPrinted >= MandatoryPauseThreshold)
            {
                newState = PrinterState.OperatorPauseRequired;
                message = $"Mandatory operator pause: {MandatoryPauseThreshold} cards printed. Output hopper capacity reached ({newOutput}/{MaxOutputHopperCapacity}).";
            }
            else if (newInput <= 0)
            {
                // This card came out fine; the next one can't be fed
                newState = PrinterState.OutOfCards;
                message = "Input hopper empty.";
            }

            return status with
            {
                State = newState,
                StatusMessage = message,
                CardsPrintedSincePause = newPrinted,
                OutputHopperCount = newOutput,
                InputHopperCount = newInput,
                RibbonRemainingPercent = newRibbon,
                TimestampUtc = DateTimeOffset.UtcNow
            };
        });
    }

    public void ResetBatchPauseCounter()
    {
        Update(status =>
        {
            bool outOfCards = (status.InputHopperCount ?? MaxInputHopperCapacity) <= 0;
            return status with
            {
                State = outOfCards ? PrinterState.OutOfCards : PrinterState.Ready,
                StatusMessage = outOfCards ? "Input hopper empty." : "Ready to print (operator pause cleared)",
                CardsPrintedSincePause = 0,
                OutputHopperCount = 0,
                TimestampUtc = DateTimeOffset.UtcNow
            };
        });
    }

    /// <summary>
    /// Simulates the operator fixing whatever stopped the printer: cards are loaded, an exhausted ribbon is replaced
    /// and a jam or open cover is cleared.
    /// </summary>
    public void AcknowledgeOperatorIntervention()
    {
        Update(status =>
        {
            bool pauseDue = status.CardsPrintedSincePause >= MandatoryPauseThreshold;
            return status with
            {
                InputHopperCount = (status.InputHopperCount ?? MaxInputHopperCapacity) <= 0 ? MaxInputHopperCapacity : status.InputHopperCount,
                RibbonRemainingPercent = (status.RibbonRemainingPercent ?? 100) <= 0 ? 100 : status.RibbonRemainingPercent,
                State = pauseDue ? PrinterState.OperatorPauseRequired : PrinterState.Ready,
                StatusMessage = pauseDue ? status.StatusMessage : "Ready to print (operator cleared the fault)",
                TimestampUtc = DateTimeOffset.UtcNow
            };
        });
    }

    /// <summary>
    /// Forces a specific state for testing error handling and recovery.
    /// </summary>
    public void ForceState(PrinterState state, string message)
    {
        Update(status => status with
        {
            State = state,
            StatusMessage = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }

    public void StartMonitoring(TimeSpan interval) => _isMonitoring = true;
    public void StopMonitoring() => _isMonitoring = false;
    public void Dispose() => StopMonitoring();

    private void Update(Func<PrinterStatus, PrinterStatus> change)
    {
        PrinterStatus previous;
        PrinterStatus next;
        lock (_lock)
        {
            previous = _currentStatus;
            next = change(previous);
            _currentStatus = next;
        }

        // Raised outside the lock so a subscriber that waits on another thread can't deadlock the monitor
        if (previous.State != next.State ||
            previous.CardsPrintedSincePause != next.CardsPrintedSincePause ||
            previous.OutputHopperCount != next.OutputHopperCount ||
            previous.InputHopperCount != next.InputHopperCount)
        {
            StatusChanged?.Invoke(this, new PrinterStatusChangedEventArgs(previous, next));
        }
    }
}
