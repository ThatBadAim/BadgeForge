using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Drivers;

/// <summary>
/// Abstraction for querying native hardware status directly from vendor DLLs
/// (e.g. SmartComm2.dll or IDP OCP communication).
/// If unavailable (or when running in 64-bit mode without an out-of-process 32-bit shim),
/// the driver gracefully falls back to Windows print spooler polling.
/// </summary>
public interface INativeIdpStatusProvider
{
    bool IsAvailable { get; }
    Task<PrinterStatus?> QueryNativeStatusAsync(string printerName, CancellationToken ct = default);
}

/// <summary>
/// Default native status provider indicating native SDK status requires real-hardware
/// verification and an out-of-process 32-bit shim if 32-bit vendor DLLs are used.
/// </summary>
public class DefaultNativeIdpStatusProvider : INativeIdpStatusProvider
{
    public bool IsAvailable => false;

    public Task<PrinterStatus?> QueryNativeStatusAsync(string printerName, CancellationToken ct = default)
    {
        // When physical 64-bit IDP SDK or 32-bit shim is present, invoke it here.
        return Task.FromResult<PrinterStatus?>(null);
    }
}

/// <summary>
/// Status monitor for the IDP SMART-31 card printer.
/// Uses native hardware status if an active provider is registered, otherwise reads printer and job status from the
/// Windows print spooler. The spooler can't report hopper or ribbon levels, so those are left unknown rather than
/// guessed; only the mandatory 20-card output hopper pause is counted locally.
/// </summary>
public class IdpSmart31StatusMonitor : ICardPrinterStatusMonitor
{
    private readonly string _printerName;
    private readonly INativeIdpStatusProvider _nativeProvider;
    private readonly object _stateLock = new();
    private readonly HashSet<string> _trackedJobs = new(StringComparer.Ordinal);
    private Timer? _pollTimer;
    private int _pollInProgress;

    private PrinterStatus _currentStatus;
    private bool _isMonitoring;
    private int _cardsPrintedSinceLastPause;
    private int _outputHopperCount;

    public const int MandatoryPauseThreshold = 20;
    public const int MaxOutputHopperCapacity = 25;
    public const int MaxInputHopperCapacity = 80;

    public PrinterStatus CurrentStatus
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStatus;
            }
        }
    }

    /// <summary>
    /// Cards sent since the output hopper was last emptied.
    /// </summary>
    public int CardsPrintedSincePause
    {
        get
        {
            lock (_stateLock)
            {
                return _cardsPrintedSinceLastPause;
            }
        }
    }

    public event EventHandler<PrinterStatusChangedEventArgs>? StatusChanged;

    public bool IsMonitoring
    {
        get
        {
            lock (_stateLock)
            {
                return _isMonitoring;
            }
        }
    }

    public IdpSmart31StatusMonitor(string printerName, INativeIdpStatusProvider? nativeProvider = null)
    {
        _printerName = printerName;
        _nativeProvider = nativeProvider ?? new DefaultNativeIdpStatusProvider();
        _currentStatus = new PrinterStatus
        {
            State = PrinterState.Ready,
            StatusMessage = "Printer status not checked yet (SMART-31)",
            InputHopperCount = null,
            OutputHopperCount = 0,
            CardsPrintedSincePause = 0,
            RibbonRemainingPercent = null,
            IsNativeStatus = false
        };
    }

    public async Task<PrinterStatus> PollStatusAsync(CancellationToken ct = default)
    {
        // 1. Try native vendor status first if available
        if (_nativeProvider.IsAvailable)
        {
            try
            {
                var nativeStatus = await _nativeProvider.QueryNativeStatusAsync(_printerName, ct);
                if (nativeStatus != null)
                {
                    return Publish(_ => nativeStatus);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fall back to spooler polling on native query failure
            }
        }

        // 2. Windows print spooler fallback
        SpoolerSnapshot? snapshot = null;
        string? spoolerError = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                snapshot = WindowsSpooler.TryQuery(_printerName);
            }
            catch (Exception ex)
            {
                spoolerError = ex.Message;
            }
        }

        return Publish(current => BuildSpoolerStatus(current, snapshot, spoolerError));
    }

    /// <summary>
    /// Remembers a print job this driver sent, so it is reported as active until it leaves the spooler queue.
    /// </summary>
    public void TrackSubmittedJob(string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);

        Publish(current =>
        {
            _trackedJobs.Add(documentName);
            return current with
            {
                ActiveJobId = documentName,
                State = current.State == PrinterState.Ready ? PrinterState.Printing : current.State
            };
        });
    }

    /// <summary>
    /// Document names of jobs this driver sent that the spooler hasn't reported as finished yet.
    /// </summary>
    public IReadOnlyCollection<string> GetTrackedJobNames()
    {
        lock (_stateLock)
        {
            return _trackedJobs.ToArray();
        }
    }

    /// <summary>
    /// Forgets the jobs this driver sent, after they have been cancelled in the spooler. Without this the monitor
    /// would keep reporting a deleted job as the active one until the next poll clears it.
    /// </summary>
    public void ForgetTrackedJobs()
    {
        Publish(current =>
        {
            _trackedJobs.Clear();
            return current with
            {
                ActiveJobId = null,
                State = current.State == PrinterState.Printing ? PrinterState.Ready : current.State,
                StatusMessage = current.State == PrinterState.Printing ? "Ready" : current.StatusMessage,
                TimestampUtc = DateTimeOffset.UtcNow
            };
        });
    }

    public void RecordCardEjected()
    {
        Publish(current =>
        {
            _cardsPrintedSinceLastPause++;
            _outputHopperCount++;
            return RefreshCounters(current);
        });
    }

    public void ResetBatchPauseCounter()
    {
        Publish(current =>
        {
            _cardsPrintedSinceLastPause = 0;
            _outputHopperCount = 0;
            return RefreshCounters(current);
        });
    }

    public void StartMonitoring(TimeSpan interval)
    {
        lock (_stateLock)
        {
            _isMonitoring = true;
            _pollTimer?.Dispose();
            _pollTimer = new Timer(_ => _ = PollInBackgroundAsync(), null, interval, interval);
        }
    }

    public void StopMonitoring()
    {
        lock (_stateLock)
        {
            _isMonitoring = false;
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }

    private async Task PollInBackgroundAsync()
    {
        // Skip a tick rather than stack overlapping spooler queries
        if (Interlocked.Exchange(ref _pollInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            await PollStatusAsync();
        }
        catch
        {
            // Swallow background poll exceptions to maintain liveness
        }
        finally
        {
            Interlocked.Exchange(ref _pollInProgress, 0);
        }
    }

    // Runs under _stateLock (called from Publish)
    private PrinterStatus BuildSpoolerStatus(PrinterStatus current, SpoolerSnapshot? snapshot, string? spoolerError)
    {
        PrinterState state;
        string message;
        string? activeJobId = null;

        if (snapshot != null)
        {
            // Forget jobs once they have finished or left the queue
            _trackedJobs.RemoveWhere(name => !snapshot.Jobs.Any(j => j.DocumentName == name && !j.IsFinished));
            var activeJob = snapshot.Jobs.FirstOrDefault(j => !j.IsFinished && _trackedJobs.Contains(j.DocumentName));
            activeJobId = activeJob?.DocumentName;
            (state, message) = SpoolerStatusMapper.Map(snapshot, activeJob);
        }
        else if (!OperatingSystem.IsWindows())
        {
            state = PrinterState.Offline;
            message = "Printer status is only available on Windows.";
        }
        else
        {
            state = PrinterState.Offline;
            message = spoolerError == null
                ? $"Printer '{_printerName}' was not found in the Windows print spooler."
                : $"Couldn't read the status of printer '{_printerName}': {spoolerError}";
        }

        return ApplyPauseCounter(current with
        {
            State = state,
            StatusMessage = message,
            InputHopperCount = null,
            RibbonRemainingPercent = null,
            ActiveJobId = activeJobId,
            IsNativeStatus = false,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }

    // Runs under _stateLock (called from Publish)
    private PrinterStatus RefreshCounters(PrinterStatus current)
    {
        var status = current.State == PrinterState.OperatorPauseRequired && _cardsPrintedSinceLastPause < MandatoryPauseThreshold
            ? current with { State = PrinterState.Ready, StatusMessage = "Ready (operator pause cleared)" }
            : current;

        return ApplyPauseCounter(status with { TimestampUtc = DateTimeOffset.UtcNow });
    }

    // Runs under _stateLock (called from Publish)
    private PrinterStatus ApplyPauseCounter(PrinterStatus status)
    {
        var updated = status with
        {
            CardsPrintedSincePause = _cardsPrintedSinceLastPause,
            OutputHopperCount = _outputHopperCount
        };

        // A hardware fault outranks the hopper pause: it has to be fixed first
        if (_cardsPrintedSinceLastPause >= MandatoryPauseThreshold && status.State is PrinterState.Ready or PrinterState.Printing)
        {
            return updated with
            {
                State = PrinterState.OperatorPauseRequired,
                StatusMessage = $"Mandatory operator pause: {MandatoryPauseThreshold} cards printed. Empty output hopper (capacity: {MaxOutputHopperCapacity})."
            };
        }

        return updated;
    }

    private PrinterStatus Publish(Func<PrinterStatus, PrinterStatus> change)
    {
        PrinterStatus previous;
        PrinterStatus next;
        lock (_stateLock)
        {
            previous = _currentStatus;
            next = change(previous);
            _currentStatus = next;
        }

        // Raised outside the lock so a subscriber that waits on another thread can't deadlock the monitor
        if (previous.State != next.State ||
            previous.CardsPrintedSincePause != next.CardsPrintedSincePause ||
            previous.RequiresOperatorAttention != next.RequiresOperatorAttention ||
            previous.ActiveJobId != next.ActiveJobId)
        {
            StatusChanged?.Invoke(this, new PrinterStatusChangedEventArgs(previous, next));
        }

        return next;
    }
}
