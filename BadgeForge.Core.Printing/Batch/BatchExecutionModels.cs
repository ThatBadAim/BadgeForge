using BadgeForge.Core.Printing.Models;
using SkiaSharp;

namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Status of an individual card record within a batch execution run.
/// </summary>
public enum BatchRecordStatus
{
    Pending,
    Printing,
    Success,
    Failed,
    Skipped
}

/// <summary>
/// Current state of the batch printing state machine.
/// </summary>
public enum BatchExecutionState
{
    Idle,
    Running,
    PausedForHopper,
    PausedOnError,
    Completed,
    Cancelled,
    PausedByUser
}

/// <summary>
/// Reason why the batch execution was paused.
/// </summary>
public enum BatchPauseReason
{
    None,
    HopperLimitReached,
    HardwareError,
    StatusTimeout,
    UserRequested,

    /// <summary>
    /// A fault was reported after the printer accepted the card, so only the operator can tell whether it printed.
    /// </summary>
    CardOutcomeUnknown,

    /// <summary>
    /// The operator stopped the run while this card was in the printer. The run is held right there until they say
    /// whether to let the card finish or to eject it as it is.
    /// </summary>
    StopRequested
}

/// <summary>
/// Encapsulates a single card item ready for sequential submission.
/// </summary>
public record BatchCardItem
{
    public string RecordId { get; init; } = string.Empty;
    public int BatchIndex { get; init; } = 1;
    public string Label { get; init; } = "Badge";
    public CardFormat Format { get; init; } = CardFormat.CR80;
    public SKBitmap? FrontBitmap { get; init; }
    public SKBitmap? BackBitmap { get; init; }
    public byte[] FrontPixelBuffer { get; init; } = Array.Empty<byte>();
    public byte[]? BackPixelBuffer { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Renders the front image (encoded PNG) when the engine reaches this card, if no front buffer or bitmap was given.
    /// Lets a large batch avoid holding every rendered card in memory at once.
    /// </summary>
    public Func<CancellationToken, Task<byte[]>>? RenderFrontAsync { get; init; }

    /// <summary>
    /// Converts this batch card item into a driver-ready CardPrintJob, rendering the front on demand if needed.
    /// </summary>
    public async Task<CardPrintJob> ToPrintJobAsync(string? customJobId = null, CancellationToken ct = default)
    {
        var job = ToPrintJob(customJobId);
        if (job.FrontPixelBuffer.Length == 0 && RenderFrontAsync != null)
        {
            job = job with { FrontPixelBuffer = await RenderFrontAsync(ct) };
        }

        return job;
    }

    /// <summary>
    /// Converts this batch card item into a driver-ready CardPrintJob.
    /// </summary>
    public CardPrintJob ToPrintJob(string? customJobId = null)
    {
        byte[] frontBytes = FrontPixelBuffer;
        if (frontBytes.Length == 0 && FrontBitmap != null)
        {
            using var image = SKImage.FromBitmap(FrontBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            frontBytes = data.ToArray();
        }

        byte[]? backBytes = BackPixelBuffer;
        if (backBytes is null or { Length: 0 } && BackBitmap != null)
        {
            using var image = SKImage.FromBitmap(BackBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            backBytes = data.ToArray();
        }

        return new CardPrintJob
        {
            JobId = customJobId ?? Guid.NewGuid().ToString("N"),
            Format = Format,
            Label = Label,
            BatchIndex = BatchIndex,
            FrontPixelBuffer = frontBytes,
            BackPixelBuffer = backBytes,
            Metadata = Metadata
        };
    }
}

/// <summary>
/// Persisted state for a single record in the batch checkpoint.
/// </summary>
public record BatchRecordCheckpoint
{
    public string RecordId { get; init; } = string.Empty;
    public int BatchIndex { get; init; }
    public string Label { get; init; } = string.Empty;
    public BatchRecordStatus Status { get; set; } = BatchRecordStatus.Pending;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CorrelatedJobName { get; set; }
    public string? SpoolerJobId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// Root checkpoint object persisted to disk during batch execution.
/// Enables seamless crash-recovery and resumption.
/// </summary>
public record BatchCheckpoint
{
    public string BatchRunId { get; init; } = Guid.NewGuid().ToString("N");
    public string BatchName { get; init; } = "Batch Print Run";
    public string TargetPrinterName { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUpdatedAtUtc { get; set; }
    public int TotalCards { get; init; }
    public List<BatchRecordCheckpoint> Records { get; init; } = new();

    public int CompletedCards => Records.Count(r => r.Status == BatchRecordStatus.Success);
    public int FailedCards => Records.Count(r => r.Status == BatchRecordStatus.Failed);
    public int PendingCards => Records.Count(r => r.Status == BatchRecordStatus.Pending);
    public bool IsComplete => Records.Count > 0 && Records.All(r => r.Status is BatchRecordStatus.Success or BatchRecordStatus.Skipped);
}

/// <summary>
/// Event arguments delivered when the batch execution state changes.
/// </summary>
public class BatchExecutionEventArgs : EventArgs
{
    public BatchExecutionState PreviousState { get; }
    public BatchExecutionState CurrentState { get; }
    public BatchPauseReason PauseReason { get; }
    public string Message { get; }
    public int CurrentCardIndex { get; }
    public int TotalCards { get; }
    public BatchRecordCheckpoint? CurrentRecord { get; }

    public BatchExecutionEventArgs(
        BatchExecutionState previousState,
        BatchExecutionState currentState,
        BatchPauseReason pauseReason,
        string message,
        int currentCardIndex,
        int totalCards,
        BatchRecordCheckpoint? currentRecord = null)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        PauseReason = pauseReason;
        Message = message;
        CurrentCardIndex = currentCardIndex;
        TotalCards = totalCards;
        CurrentRecord = currentRecord;
    }
}

/// <summary>
/// Final summary report of a batch execution run.
/// </summary>
public record BatchExecutionSummary
{
    public string BatchRunId { get; init; } = string.Empty;
    public BatchExecutionState FinalState { get; init; }
    public int TotalCards { get; init; }
    public int CompletedCards { get; init; }
    public int FailedCards { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public string? TerminalMessage { get; init; }
    public BatchCheckpoint Checkpoint { get; init; } = new();
}
