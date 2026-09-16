using BadgeForge.Core.Printing.Interfaces;

namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Configuration and card payload submitted to the batch print engine.
/// </summary>
public record BatchPrintRequest
{
    public string BatchRunId { get; init; } = Guid.NewGuid().ToString("N");
    public string BatchName { get; init; } = "Badge Batch";
    public IReadOnlyList<BatchCardItem> Cards { get; init; } = Array.Empty<BatchCardItem>();
    public ICardPrinterDriver Driver { get; init; } = null!;
    public string? CheckpointFilePath { get; init; }
    public bool AutoResumeFromCheckpoint { get; init; } = true;
    public TimeSpan StatusPollingInterval { get; init; } = TimeSpan.FromMilliseconds(50);
    public TimeSpan CardCompletionTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MandatoryPauseThreshold { get; init; } = 20;
}

/// <summary>
/// Engine for sequential batch badge printing.
/// Enforces single-card spooling, job correlation, native status gating,
/// mandatory 20-card output hopper pauses, and crash-resilient disk checkpoints.
/// </summary>
public interface IBatchPrintEngine
{
    /// <summary>
    /// Current state of the batch execution engine.
    /// </summary>
    BatchExecutionState State { get; }

    /// <summary>
    /// Active checkpoint representation of the batch run.
    /// </summary>
    BatchCheckpoint? CurrentCheckpoint { get; }

    /// <summary>
    /// Event raised when batch execution state changes (e.g. hopper pause, error pause, progress).
    /// </summary>
    event EventHandler<BatchExecutionEventArgs>? StateChanged;

    /// <summary>
    /// Executes the full batch run sequentially.
    /// </summary>
    Task<BatchExecutionSummary> ExecuteBatchAsync(BatchPrintRequest request, CancellationToken ct = default);

    /// <summary>
    /// Requests an operator pause. The card currently printing is allowed to finish; the engine
    /// pauses before submitting the next card and waits for <see cref="ResumeAsync"/>.
    /// </summary>
    Task PauseAsync(CancellationToken ct = default);

    /// <summary>
    /// Signals the engine to resume from a pause (such as after clearing the output hopper or resolving a jam).
    /// For a fault before the card reached the printer, the card is tried again. When the card's outcome is unknown
    /// (<see cref="BatchPauseReason.CardOutcomeUnknown"/>), resuming means the operator confirmed it printed correctly,
    /// so it is not sent again.
    /// </summary>
    Task ResumeAsync(CancellationToken ct = default);

    /// <summary>
    /// Resumes from a <see cref="BatchPauseReason.CardOutcomeUnknown"/> pause by printing that card again.
    /// For any other pause this behaves like <see cref="ResumeAsync"/>.
    /// </summary>
    Task ReprintCardAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the run at the card being printed right now, rather than after it finishes.
    /// While a card is in the printer the engine pauses on <see cref="BatchPauseReason.StopRequested"/> and waits for
    /// <see cref="FinishCurrentCardAsync"/> or <see cref="AbortCurrentCardAsync"/>. With no card in the printer
    /// (between cards, or while already paused) the run simply ends.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Answers a <see cref="BatchPauseReason.StopRequested"/> pause by letting the card in the printer finish
    /// printing; the run then stops with that card counted as printed.
    /// </summary>
    Task FinishCurrentCardAsync(CancellationToken ct = default);

    /// <summary>
    /// Answers a <see cref="BatchPauseReason.StopRequested"/> pause by giving up on the card in the printer: it is
    /// ejected with whatever has been printed on it, marked as failed, and the run stops.
    /// </summary>
    Task AbortCurrentCardAsync(CancellationToken ct = default);

    /// <summary>
    /// Cancels active batch execution.
    /// </summary>
    Task CancelAsync(CancellationToken ct = default);
}
