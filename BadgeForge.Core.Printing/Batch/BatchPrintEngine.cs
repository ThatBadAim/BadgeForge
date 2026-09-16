using System.Diagnostics;
using System.Text.Json;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Implementation of IBatchPrintEngine coordinating sequential, one-card-at-a-time printing.
/// Enforces physical status monitor gating, job correlation, mandatory 20-card output hopper pauses,
/// atomic checkpoint persistence to disk, and pause-on-error behavior.
/// A card the printer has already accepted is never sent again unless the operator asks for a reprint.
/// </summary>
public class BatchPrintEngine : IBatchPrintEngine
{
    private enum ResumeDecision
    {
        Continue,
        Reprint,

        /// <summary>Answer to a stop prompt: let the card in the printer finish.</summary>
        FinishCard,

        /// <summary>Answer to a stop prompt: eject the card in the printer as it is.</summary>
        AbortCard
    }

    /// <summary>How a wait for the printer ended.</summary>
    private enum WaitOutcome
    {
        Ready,
        StopRequested
    }

    /// <summary>What happened to one card, and whether the run carries on afterwards.</summary>
    private enum CardOutcome
    {
        Printed,
        StoppedBeforeCard,
        StoppedAfterCardFinished,
        StoppedCardAborted,
        StoppedCardUnknown
    }

    private readonly IBatchCheckpointStore _checkpointStore;
    private readonly ISpoolerJobCorrelator _correlator;
    private readonly Action<string>? _logAction;

    private readonly object _stateLock = new();
    private BatchExecutionState _state = BatchExecutionState.Idle;
    private BatchCheckpoint? _currentCheckpoint;
    private TaskCompletionSource<ResumeDecision>? _resumeTcs;
    private CancellationTokenSource? _internalCts;
    private int _cardsPrintedSinceLastPause;
    private volatile bool _pauseRequested;

    // Checked at every step of every wait loop, so a stop lands on the card being printed rather than after it
    private volatile bool _stopRequested;
    private BatchPauseReason _pauseReason = BatchPauseReason.None;
    private int _isExecuting;

    public BatchExecutionState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
        private set
        {
            BatchExecutionState previous;
            lock (_stateLock)
            {
                previous = _state;
                _state = value;
            }

            if (previous != value)
            {
                Log($"State transition: {previous} -> {value}");
            }
        }
    }

    public BatchCheckpoint? CurrentCheckpoint
    {
        get
        {
            lock (_stateLock)
            {
                return _currentCheckpoint;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                _currentCheckpoint = value;
            }
        }
    }

    public event EventHandler<BatchExecutionEventArgs>? StateChanged;

    public BatchPrintEngine(
        IBatchCheckpointStore? checkpointStore = null,
        ISpoolerJobCorrelator? correlator = null,
        Action<string>? logAction = null)
    {
        _checkpointStore = checkpointStore ?? new JsonBatchCheckpointStore();
        _correlator = correlator ?? new SpoolerJobCorrelator(logAction);
        _logAction = logAction;
    }

    public async Task<BatchExecutionSummary> ExecuteBatchAsync(BatchPrintRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Driver);
        EnsureUniqueRecordIds(request.Cards);

        if (request.Cards.Count == 0)
        {
            return new BatchExecutionSummary
            {
                BatchRunId = request.BatchRunId,
                FinalState = BatchExecutionState.Completed,
                TotalCards = 0,
                CompletedCards = 0,
                FailedCards = 0,
                ElapsedTime = TimeSpan.Zero,
                TerminalMessage = "Batch request contained no card items."
            };
        }

        // Two runs sharing one engine would share pause signals, counters and cancellation
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
        {
            throw new InvalidOperationException("A batch is already running on this print engine. Wait for it to finish or cancel it first.");
        }

        try
        {
            return await ExecuteCoreAsync(request, ct);
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0);
        }
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_state != BatchExecutionState.Running)
            {
                Log($"Pause requested but engine is in state '{_state}'. Ignoring.");
                return Task.CompletedTask;
            }

            Log("Operator invoked PauseAsync. Batch will pause before the next card.");
            _pauseRequested = true;
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken ct = default) => SignalOperatorDecision(ResumeDecision.Continue);

    public Task ReprintCardAsync(CancellationToken ct = default) => SignalOperatorDecision(ResumeDecision.Reprint);

    public Task StopAsync(CancellationToken ct = default)
    {
        TaskCompletionSource<ResumeDecision>? waitingPause = null;
        lock (_stateLock)
        {
            if (_state is BatchExecutionState.Idle or BatchExecutionState.Completed or BatchExecutionState.Cancelled)
            {
                Log($"Stop requested but engine is in state '{_state}'. Ignoring.");
                return Task.CompletedTask;
            }

            if (_pauseReason == BatchPauseReason.StopRequested)
            {
                // Already asking the operator what to do with the card in the printer
                return Task.CompletedTask;
            }

            Log("Operator invoked StopAsync. The run stops at the card in the printer now.");
            _stopRequested = true;

            // A run already holding at a pause has no card in the printer, so there is nothing to decide: end it
            if (_state is BatchExecutionState.PausedForHopper or BatchExecutionState.PausedOnError or BatchExecutionState.PausedByUser)
            {
                waitingPause = _resumeTcs;
            }
        }

        waitingPause?.TrySetCanceled();
        return Task.CompletedTask;
    }

    public Task FinishCurrentCardAsync(CancellationToken ct = default) => SignalOperatorDecision(ResumeDecision.FinishCard);

    public Task AbortCurrentCardAsync(CancellationToken ct = default) => SignalOperatorDecision(ResumeDecision.AbortCard);

    public Task CancelAsync(CancellationToken ct = default)
    {
        CancellationTokenSource? cts;
        TaskCompletionSource<ResumeDecision>? resumeTcs;
        lock (_stateLock)
        {
            Log("Operator invoked CancelAsync.");
            cts = _internalCts;
            resumeTcs = _resumeTcs;
        }

        // Cancel outside the lock: cancellation callbacks may run engine code inline
        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run finished while cancelling
        }

        resumeTcs?.TrySetCanceled();
        return Task.CompletedTask;
    }

    private async Task<BatchExecutionSummary> ExecuteCoreAsync(BatchPrintRequest request, CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linkedCts.Token;
        var stopwatch = Stopwatch.StartNew();
        BatchCheckpoint? checkpoint = null;

        lock (_stateLock)
        {
            _internalCts = linkedCts;
        }

        _cardsPrintedSinceLastPause = 0;
        _pauseRequested = false;
        _stopRequested = false;
        State = BatchExecutionState.Running;

        try
        {
            token.ThrowIfCancellationRequested();

            // 1. Initialize or reconcile checkpoint from disk
            checkpoint = await InitializeOrLoadCheckpointAsync(request, token);
            CurrentCheckpoint = checkpoint;
            var recordsById = checkpoint.Records.ToDictionary(r => r.RecordId, StringComparer.Ordinal);

            RaiseStateChanged(
                BatchExecutionState.Idle,
                BatchExecutionState.Running,
                BatchPauseReason.None,
                $"Batch '{request.BatchName}' started. Total cards: {checkpoint.TotalCards}.",
                checkpoint.CompletedCards,
                checkpoint.TotalCards);

            // 2. Sequential execution loop — strictly one card at a time!
            for (int i = 0; i < request.Cards.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var card = request.Cards[i];
                var record = recordsById[card.RecordId];
                bool isLastCard = i == request.Cards.Count - 1;

                // Skip already successfully printed cards (crash-resume capability)
                if (record.Status == BatchRecordStatus.Success)
                {
                    Log($"Skipping card #{card.BatchIndex} ('{card.RecordId}'): already successfully printed in prior run.");
                    continue;
                }

                // A previous run stopped after this card was sent; only the operator can see whether it came out
                if (record.Status == BatchRecordStatus.Printing)
                {
                    var decision = await HandleOutcomeUnknownPauseAsync(
                        request, checkpoint, record, card.BatchIndex,
                        "the previous run stopped before it was confirmed as printed", token);

                    if (decision == ResumeDecision.Continue)
                    {
                        await CompleteCardAsync(request, checkpoint, card, record, operatorPauseTriggered: false, isLastCard, token);
                        continue;
                    }
                }

                var outcome = await PrintCardAsync(request, checkpoint, card, record, isLastCard, token);
                if (outcome != CardOutcome.Printed)
                {
                    stopwatch.Stop();
                    return BuildStoppedSummary(checkpoint, outcome, card.BatchIndex, stopwatch.Elapsed);
                }
            }

            State = BatchExecutionState.Completed;
            stopwatch.Stop();

            RaiseStateChanged(
                BatchExecutionState.Running,
                BatchExecutionState.Completed,
                BatchPauseReason.None,
                $"Batch execution completed. {checkpoint.CompletedCards} cards printed successfully.",
                checkpoint.TotalCards,
                checkpoint.TotalCards);

            return new BatchExecutionSummary
            {
                BatchRunId = checkpoint.BatchRunId,
                FinalState = BatchExecutionState.Completed,
                TotalCards = checkpoint.TotalCards,
                CompletedCards = checkpoint.CompletedCards,
                FailedCards = checkpoint.FailedCards,
                ElapsedTime = stopwatch.Elapsed,
                TerminalMessage = $"Batch completed successfully. {checkpoint.CompletedCards} of {checkpoint.TotalCards} cards printed.",
                Checkpoint = checkpoint
            };
        }
        catch (OperationCanceledException)
        {
            var previousState = State;
            State = BatchExecutionState.Cancelled;
            stopwatch.Stop();

            RaiseStateChanged(
                previousState,
                BatchExecutionState.Cancelled,
                BatchPauseReason.UserRequested,
                "Batch execution cancelled by user.",
                checkpoint?.CompletedCards ?? 0,
                checkpoint?.TotalCards ?? request.Cards.Count);

            return new BatchExecutionSummary
            {
                BatchRunId = checkpoint?.BatchRunId ?? request.BatchRunId,
                FinalState = BatchExecutionState.Cancelled,
                TotalCards = checkpoint?.TotalCards ?? request.Cards.Count,
                CompletedCards = checkpoint?.CompletedCards ?? 0,
                FailedCards = checkpoint?.FailedCards ?? 0,
                ElapsedTime = stopwatch.Elapsed,
                TerminalMessage = "Batch cancelled by user.",
                Checkpoint = checkpoint ?? new BatchCheckpoint { BatchRunId = request.BatchRunId, TotalCards = request.Cards.Count }
            };
        }
        catch (Exception ex)
        {
            // Anything not recoverable per card (such as an unreadable checkpoint) ends the run; never leave the
            // engine reporting Running with nobody driving it
            var previousState = State;
            State = BatchExecutionState.Idle;
            Log($"Batch stopped: {ex}");

            RaiseStateChanged(
                previousState,
                BatchExecutionState.Idle,
                BatchPauseReason.None,
                $"Batch stopped: {ex.Message}",
                checkpoint?.CompletedCards ?? 0,
                checkpoint?.TotalCards ?? request.Cards.Count);

            throw;
        }
        finally
        {
            lock (_stateLock)
            {
                _internalCts = null;
                _resumeTcs = null;
                _pauseReason = BatchPauseReason.None;
            }

            _pauseRequested = false;
            _stopRequested = false;
        }
    }

    /// <summary>
    /// Ends the run the operator stopped, saying plainly what happened to the card that was in the printer.
    /// </summary>
    private BatchExecutionSummary BuildStoppedSummary(BatchCheckpoint checkpoint, CardOutcome outcome, int cardIndex, TimeSpan elapsed)
    {
        string message = outcome switch
        {
            CardOutcome.StoppedBeforeCard =>
                $"Stopped before card {cardIndex} of {checkpoint.TotalCards}. {checkpoint.CompletedCards} printed, nothing was left in the printer.",
            CardOutcome.StoppedAfterCardFinished =>
                $"Stopped after card {cardIndex} of {checkpoint.TotalCards} finished. {checkpoint.CompletedCards} printed.",
            CardOutcome.StoppedCardAborted =>
                $"Stopped at card {cardIndex} of {checkpoint.TotalCards}: it was ejected part-printed and needs printing again. {checkpoint.CompletedCards} printed.",
            _ =>
                $"Stopped at card {cardIndex} of {checkpoint.TotalCards}, but the printer never confirmed what happened to it — check the card that came out. {checkpoint.CompletedCards} printed."
        };

        var previousState = State;
        State = BatchExecutionState.Cancelled;

        RaiseStateChanged(
            previousState,
            BatchExecutionState.Cancelled,
            BatchPauseReason.UserRequested,
            message,
            cardIndex,
            checkpoint.TotalCards);

        return new BatchExecutionSummary
        {
            BatchRunId = checkpoint.BatchRunId,
            FinalState = BatchExecutionState.Cancelled,
            TotalCards = checkpoint.TotalCards,
            CompletedCards = checkpoint.CompletedCards,
            FailedCards = checkpoint.FailedCards,
            ElapsedTime = elapsed,
            TerminalMessage = message,
            Checkpoint = checkpoint
        };
    }

    /// <summary>
    /// Prints one card, retrying after faults that happened before the printer took it. Every fault becomes an
    /// operator pause rather than ending the batch.
    /// </summary>
    private async Task<CardOutcome> PrintCardAsync(
        BatchPrintRequest request,
        BatchCheckpoint checkpoint,
        BatchCardItem card,
        BatchRecordCheckpoint record,
        bool isLastCard,
        CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();

            bool cardAccepted = false;
            bool operatorPauseTriggered = false;

            try
            {
                // Operator pause is honored only between cards, never mid-print
                if (_pauseRequested)
                {
                    await HandleUserPauseAsync(card.BatchIndex, checkpoint.TotalCards, token);
                }

                // Nothing has been sent for this card yet, so a stop asked for now needs no decision
                if (_stopRequested)
                {
                    return CardOutcome.StoppedBeforeCard;
                }

                // 2a. Gate on printer readiness before beginning card
                if (await GatePrinterReadinessAsync(request, token) == WaitOutcome.StopRequested)
                {
                    return CardOutcome.StoppedBeforeCard;
                }

                // 2b. Check mandatory 20-card pause threshold before sending card
                if (_cardsPrintedSinceLastPause >= request.MandatoryPauseThreshold)
                {
                    await HandleMandatoryHopperPauseAsync(request, card.BatchIndex, checkpoint.TotalCards, token);
                }

                // 2c. Generate uniquely identifiable job name (GUID + record ID) and prepare the card image
                string correlatedJobName = _correlator.GenerateJobName(card.RecordId, request.BatchRunId);
                var printJob = await card.ToPrintJobAsync(correlatedJobName, token);

                // 2d. Mark card status as Printing and flush checkpoint immediately to disk
                record.Status = BatchRecordStatus.Printing;
                record.SubmittedAtUtc = DateTimeOffset.UtcNow;
                record.CorrelatedJobName = correlatedJobName;
                record.ErrorMessage = null;
                await SaveCheckpointAsync(checkpoint, request.CheckpointFilePath);

                RaiseStateChanged(
                    BatchExecutionState.Running,
                    BatchExecutionState.Running,
                    BatchPauseReason.None,
                    $"Printing card {card.BatchIndex} of {checkpoint.TotalCards}: '{card.Label}' ({card.RecordId})",
                    card.BatchIndex,
                    checkpoint.TotalCards,
                    record);

                // 2e. Submit card to driver
                var printResult = await request.Driver.SubmitCardAsync(printJob, token);
                if (!printResult.Success)
                {
                    throw new PrinterException(printResult.ErrorMessage ?? $"The printer driver rejected card #{card.BatchIndex}.");
                }

                cardAccepted = true;
                operatorPauseTriggered = printResult.OperatorPauseTriggered;
                string? driverJobId = printResult.SpoolerJobId;

                // 2f. Correlate job in queue/driver
                var correlation = await _correlator.CorrelateJobAsync(
                    request.Driver.TargetPrinterName ?? "Printer",
                    correlatedJobName,
                    customLookup: () => Task.FromResult(driverJobId),
                    timeout: TimeSpan.FromSeconds(3),
                    ct: token);

                record.SpoolerJobId = correlation.SpoolerJobId ?? driverJobId;

                // 2g. Gate "ready for next card" on driver status monitor. This is where a stop lands while a card
                // is physically printing, and the only person who can decide its fate is the operator.
                if (await WaitForCardPhysicalCompletionAsync(request, token) == WaitOutcome.StopRequested)
                {
                    var stopOutcome = await HandleStopDecisionAsync(request, checkpoint, card, record, token);
                    if (stopOutcome == CardOutcome.StoppedAfterCardFinished)
                    {
                        // isLastCard: the run is over, so don't hold the operator at a hopper pause on the way out
                        await CompleteCardAsync(request, checkpoint, card, record, operatorPauseTriggered, isLastCard: true, token);
                    }

                    return stopOutcome;
                }
            }
            catch (OperatorPauseRequiredException) when (!cardAccepted)
            {
                // Driver signaled mandatory hopper pause before taking the card; nothing was printed
                record.Status = BatchRecordStatus.Pending;
                await HandleMandatoryHopperPauseAsync(request, card.BatchIndex, checkpoint.TotalCards, token);
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !cardAccepted)
            {
                // 2j. Pause-on-error: the card never reached the printer, so resuming retries it
                record.Status = BatchRecordStatus.Failed;
                record.ErrorMessage = ex.Message;
                record.CompletedAtUtc = DateTimeOffset.UtcNow;
                await SaveCheckpointAsync(checkpoint, request.CheckpointFilePath);

                Log($"ERROR on card #{card.BatchIndex} ('{card.RecordId}'): {ex.Message}");

                await HandleErrorPauseAsync(request, checkpoint, record, card.BatchIndex, ex.Message, token);
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The printer already has this card. Sending it again without the operator checking could produce a
                // duplicate badge, so the record stays "Printing" (outcome unknown) until they decide.
                record.ErrorMessage = $"The card was sent to the printer, but then: {ex.Message}";
                await SaveCheckpointAsync(checkpoint, request.CheckpointFilePath);

                Log($"FAULT AFTER SUBMISSION on card #{card.BatchIndex} ('{card.RecordId}'): {ex.Message}");

                var decision = await HandleOutcomeUnknownPauseAsync(request, checkpoint, record, card.BatchIndex, ex.Message, token);
                if (decision == ResumeDecision.Reprint)
                {
                    continue;
                }
            }

            await CompleteCardAsync(request, checkpoint, card, record, operatorPauseTriggered, isLastCard, token);
            return CardOutcome.Printed;
        }
    }

    /// <summary>
    /// Holds the run on the card that is in the printer and asks the operator whether to let it finish or to eject
    /// it as it is. Everything before this point has already been written to the checkpoint, so either answer leaves
    /// an accurate record of what came out of the printer.
    /// </summary>
    private async Task<CardOutcome> HandleStopDecisionAsync(
        BatchPrintRequest request,
        BatchCheckpoint checkpoint,
        BatchCardItem card,
        BatchRecordCheckpoint record,
        CancellationToken token)
    {
        string promptMessage =
            $"Stopping at card {card.BatchIndex} of {checkpoint.TotalCards} ('{card.Label}'), which is in the printer now. " +
            "Let this card finish, or eject it as it is and print it again later.";

        var decision = await WaitForOperatorAsync(
            BatchExecutionState.PausedByUser,
            BatchPauseReason.StopRequested,
            promptMessage,
            card.BatchIndex,
            checkpoint.TotalCards,
            record,
            token);

        if (decision == ResumeDecision.FinishCard)
        {
            Log($"Operator chose to let card #{card.BatchIndex} finish before the run stops.");
            try
            {
                // The stop is already granted, so it must not cut this wait short
                await WaitForCardPhysicalCompletionAsync(request, token, honourStop: false);
                return CardOutcome.StoppedAfterCardFinished;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The card stays "Printing": the printer never confirmed it, so only the operator can tell
                record.ErrorMessage = $"The run was stopped while this card was printing, and then: {ex.Message}";
                await SaveCheckpointAsync(checkpoint, request.CheckpointFilePath);
                Log($"Card #{card.BatchIndex} could not be confirmed after the operator let it finish: {ex.Message}");
                AnnounceRecord(record, card.BatchIndex, checkpoint.TotalCards, record.ErrorMessage);
                return CardOutcome.StoppedCardUnknown;
            }
        }

        bool ejected = await AbortCardInPrinterAsync(request, card.BatchIndex);

        record.Status = BatchRecordStatus.Failed;
        record.CompletedAtUtc = DateTimeOffset.UtcNow;
        record.ErrorMessage = ejected
            ? "Stopped by the operator: the card was ejected part-printed and needs printing again."
            : "Stopped by the operator, but this printer can't interrupt a card, so it may have finished. Check the card that came out.";
        await SaveCheckpointAsync(checkpoint, request.CheckpointFilePath);

        // Announced like any other card outcome, so the list shows what became of it rather than leaving it "printing"
        AnnounceRecord(record, card.BatchIndex, checkpoint.TotalCards, record.ErrorMessage);

        return ejected ? CardOutcome.StoppedCardAborted : CardOutcome.StoppedCardUnknown;
    }

    /// <summary>
    /// Reports what became of one card while the run is still going, so listeners can show its outcome.
    /// </summary>
    private void AnnounceRecord(BatchRecordCheckpoint record, int cardIndex, int totalCards, string message) =>
        RaiseStateChanged(BatchExecutionState.Running, BatchExecutionState.Running, BatchPauseReason.None,
            message, cardIndex, totalCards, record);

    /// <summary>
    /// Tells the printer to give up on the card it is working on. Never throws: a printer that can't be interrupted
    /// is reported back so the operator is told to check the card rather than left thinking it was stopped.
    /// </summary>
    private async Task<bool> AbortCardInPrinterAsync(BatchPrintRequest request, int cardIndex)
    {
        try
        {
            // Not cancellable: the printer still has to be told even while the run is being torn down
            bool ejected = await request.Driver.AbortCurrentCardAsync(CancellationToken.None);
            Log(ejected
                ? $"Card #{cardIndex} was cancelled at the printer and ejected as it is."
                : $"Card #{cardIndex} could not be cancelled at the printer; it may finish on its own.");
            return ejected;
        }
        catch (Exception ex)
        {
            Log($"Couldn't cancel card #{cardIndex} at the printer: {ex.Message}");
            return false;
        }
    }

    private async Task CompleteCardAsync(
        BatchPrintRequest request,
        BatchCheckpoint checkpoint,
        BatchCardItem card,
        BatchRecordCheckpoint record,
        bool operatorPauseTriggered,
        bool isLastCard,
        CancellationToken token)
    {
        // 2h. Mark card as Success and flush checkpoint immediately
        record.Status = BatchRecordStatus.Success;
        record.CompletedAtUtc = DateTimeOffset.UtcNow;
        record.ErrorMessage = null;
        _cardsPrintedSinceLastPause++;

        await SaveCheckpointAsync(checkpoint, request.CheckpointFilePath);

        RaiseStateChanged(
            BatchExecutionState.Running,
            BatchExecutionState.Running,
            BatchPauseReason.None,
            $"Completed card {card.BatchIndex} of {checkpoint.TotalCards}: '{card.Label}'",
            card.BatchIndex,
            checkpoint.TotalCards,
            record);

        // 2i. Check if the driver triggered the 20-card hopper pause on ejection; only pause if cards remain
        if ((operatorPauseTriggered || _cardsPrintedSinceLastPause >= request.MandatoryPauseThreshold) && !isLastCard)
        {
            await HandleMandatoryHopperPauseAsync(request, card.BatchIndex, checkpoint.TotalCards, token);
        }
    }

    /// <summary>
    /// Waits for the printer to be able to take the next card. Nothing has been sent yet, so a stop request here
    /// simply ends the run.
    /// </summary>
    private async Task<WaitOutcome> GatePrinterReadinessAsync(BatchPrintRequest request, CancellationToken ct)
    {
        var timeout = request.CardCompletionTimeout;
        var stopwatch = Stopwatch.StartNew();
        PrinterStatus? lastStatus = null;

        while (stopwatch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            if (_stopRequested)
            {
                return WaitOutcome.StopRequested;
            }

            var status = await request.Driver.StatusMonitor.PollStatusAsync(ct);
            lastStatus = status;

            if (status.State is PrinterState.Ready or PrinterState.OperatorPauseRequired)
            {
                return WaitOutcome.Ready; // Ready to proceed; a hopper pause is handled by the pause logic
            }

            if (status.RequiresOperatorAttention)
            {
                throw new PrinterHardwareFaultException(
                    $"Printer hardware fault: {status.State} ({status.StatusMessage})",
                    status.State.ToString());
            }

            await Task.Delay(request.StatusPollingInterval, ct);
        }

        throw new TimeoutException(
            $"Timed out after {timeout.TotalSeconds:0}s waiting for the printer to become ready (last status: {lastStatus?.State} - {lastStatus?.StatusMessage}).");
    }

    /// <summary>
    /// Waits for the card the printer has taken to come out. The card is physically in the machine, so a stop
    /// request here is reported back rather than acted on: only the operator can say whether to let it finish.
    /// <paramref name="honourStop"/> is false once they have chosen to let it finish.
    /// </summary>
    private async Task<WaitOutcome> WaitForCardPhysicalCompletionAsync(BatchPrintRequest request, CancellationToken ct, bool honourStop = true)
    {
        var timeout = request.CardCompletionTimeout;
        var stopwatch = Stopwatch.StartNew();
        PrinterStatus? lastStatus = null;

        while (stopwatch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            if (honourStop && _stopRequested)
            {
                return WaitOutcome.StopRequested;
            }

            var status = await request.Driver.StatusMonitor.PollStatusAsync(ct);
            lastStatus = status;

            // Faults that put the card being printed at risk. An empty input hopper or a due hopper pause only stop
            // the next card, so they are left to the readiness gate.
            if (status.State is PrinterState.PaperJam or PrinterState.CoverOpen or PrinterState.OutOfRibbon
                or PrinterState.Offline or PrinterState.Error)
            {
                throw new PrinterHardwareFaultException(
                    $"Hardware fault during card ejection: {status.State} ({status.StatusMessage})",
                    status.State.ToString());
            }

            if (status.IsNativeStatus)
            {
                // Gated on native status: Ready (or a state that only blocks the next card) means the card has ejected
                if (status.State is PrinterState.Ready or PrinterState.OperatorPauseRequired or PrinterState.OutOfCards)
                {
                    return WaitOutcome.Ready;
                }
            }
            else if (status.ActiveJobId == null)
            {
                // Spooler fallback: the job has left the print queue
                return WaitOutcome.Ready;
            }

            await Task.Delay(request.StatusPollingInterval, ct);
        }

        throw new TimeoutException(
            $"Timed out after {timeout.TotalSeconds:0}s waiting for the card to finish (last status: {lastStatus?.State} - {lastStatus?.StatusMessage}).");
    }

    private async Task HandleMandatoryHopperPauseAsync(
        BatchPrintRequest request,
        int currentCardIndex,
        int totalCards,
        CancellationToken ct)
    {
        string promptMessage = $"Mandatory operator pause: {request.MandatoryPauseThreshold} cards processed. " +
                               "Please empty the output hopper and press Resume to continue.";

        await WaitForOperatorAsync(
            BatchExecutionState.PausedForHopper,
            BatchPauseReason.HopperLimitReached,
            promptMessage,
            currentCardIndex,
            totalCards,
            record: null,
            ct);

        // Reset pause counters upon operator resume
        request.Driver.StatusMonitor.ResetBatchPauseCounter();
        _cardsPrintedSinceLastPause = 0;

        ResumeRunning(
            BatchExecutionState.PausedForHopper,
            "Operator resumed batch execution after clearing output hopper.",
            currentCardIndex,
            totalCards);
    }

    private async Task HandleUserPauseAsync(int nextCardIndex, int totalCards, CancellationToken ct)
    {
        _pauseRequested = false;

        await WaitForOperatorAsync(
            BatchExecutionState.PausedByUser,
            BatchPauseReason.UserRequested,
            $"Batch paused before card {nextCardIndex} of {totalCards}. Press Resume to continue.",
            nextCardIndex,
            totalCards,
            record: null,
            ct);

        ResumeRunning(BatchExecutionState.PausedByUser, "Operator resumed batch execution.", nextCardIndex, totalCards);
    }

    private async Task HandleErrorPauseAsync(
        BatchPrintRequest request,
        BatchCheckpoint checkpoint,
        BatchRecordCheckpoint record,
        int currentCardIndex,
        string errorMessage,
        CancellationToken ct)
    {
        string promptMessage = $"Batch paused on error for card #{currentCardIndex} ('{record.RecordId}'): {errorMessage}. " +
                               "Resolve the fault and press Resume to retry, or Cancel to abort.";

        await WaitForOperatorAsync(
            BatchExecutionState.PausedOnError,
            BatchPauseReason.HardwareError,
            promptMessage,
            currentCardIndex,
            checkpoint.TotalCards,
            record,
            ct);

        request.Driver.StatusMonitor.AcknowledgeOperatorIntervention();
        record.RetryCount++;
        record.Status = BatchRecordStatus.Pending;

        ResumeRunning(
            BatchExecutionState.PausedOnError,
            $"Operator resumed batch to retry card #{currentCardIndex}.",
            currentCardIndex,
            checkpoint.TotalCards,
            record);
    }

    private async Task<ResumeDecision> HandleOutcomeUnknownPauseAsync(
        BatchPrintRequest request,
        BatchCheckpoint checkpoint,
        BatchRecordCheckpoint record,
        int currentCardIndex,
        string reason,
        CancellationToken ct)
    {
        string promptMessage = $"Card #{currentCardIndex} ('{record.RecordId}') was sent to the printer, but then: {reason}. " +
                               "Check the card that came out: press Resume if it printed correctly, or Reprint to print it again.";

        var decision = await WaitForOperatorAsync(
            BatchExecutionState.PausedOnError,
            BatchPauseReason.CardOutcomeUnknown,
            promptMessage,
            currentCardIndex,
            checkpoint.TotalCards,
            record,
            ct);

        request.Driver.StatusMonitor.AcknowledgeOperatorIntervention();

        if (decision == ResumeDecision.Reprint)
        {
            record.RetryCount++;
            record.Status = BatchRecordStatus.Pending;
        }

        ResumeRunning(
            BatchExecutionState.PausedOnError,
            decision == ResumeDecision.Reprint
                ? $"Operator asked to reprint card #{currentCardIndex}."
                : $"Operator confirmed card #{currentCardIndex} printed correctly.",
            currentCardIndex,
            checkpoint.TotalCards,
            record);

        return decision;
    }

    /// <summary>
    /// Enters a pause state and waits for the operator's Resume, Reprint or Cancel.
    /// </summary>
    private async Task<ResumeDecision> WaitForOperatorAsync(
        BatchExecutionState pausedState,
        BatchPauseReason reason,
        string message,
        int cardIndex,
        int totalCards,
        BatchRecordCheckpoint? record,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<ResumeDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        BatchExecutionState previousState;
        lock (_stateLock)
        {
            // Arm the resume signal together with the pause state, so a Resume issued the moment the pause is
            // announced is never lost
            _resumeTcs = tcs;
            _pauseReason = reason;
            previousState = _state;
            _state = pausedState;
        }

        Log($"State transition: {previousState} -> {pausedState}");
        Log($"PAUSE ({reason}): {message}");

        RaiseStateChanged(previousState, pausedState, reason, message, cardIndex, totalCards, record);

        try
        {
            using (ct.Register(() => tcs.TrySetCanceled(ct)))
            {
                return await tcs.Task;
            }
        }
        finally
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_resumeTcs, tcs))
                {
                    _resumeTcs = null;
                    _pauseReason = BatchPauseReason.None;
                }
            }
        }
    }

    private void ResumeRunning(BatchExecutionState pausedState, string message, int cardIndex, int totalCards, BatchRecordCheckpoint? record = null)
    {
        State = BatchExecutionState.Running;
        RaiseStateChanged(pausedState, BatchExecutionState.Running, BatchPauseReason.None, message, cardIndex, totalCards, record);
    }

    private Task SignalOperatorDecision(ResumeDecision decision)
    {
        lock (_stateLock)
        {
            if (_state is not (BatchExecutionState.PausedForHopper or BatchExecutionState.PausedOnError or BatchExecutionState.PausedByUser)
                || _resumeTcs == null)
            {
                Log($"{decision} requested but engine is in state '{_state}'. Ignoring.");
                return Task.CompletedTask;
            }

            // Finish/Abort answer a stop prompt; Resume/Reprint answer every other pause. Crossing them would let a
            // stray Resume decide the fate of a card that is still in the printer.
            bool answersStop = decision is ResumeDecision.FinishCard or ResumeDecision.AbortCard;
            if (answersStop != (_pauseReason == BatchPauseReason.StopRequested))
            {
                Log($"{decision} requested but the engine is paused on '{_pauseReason}'. Ignoring.");
                return Task.CompletedTask;
            }

            Log($"Operator chose {decision}. Releasing pause signal.");
            _resumeTcs.TrySetResult(decision);
        }

        return Task.CompletedTask;
    }

    private async Task<BatchCheckpoint> InitializeOrLoadCheckpointAsync(BatchPrintRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.CheckpointFilePath) && request.AutoResumeFromCheckpoint)
        {
            BatchCheckpoint? loaded;
            try
            {
                loaded = await _checkpointStore.LoadCheckpointAsync(request.CheckpointFilePath, ct);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"The checkpoint file '{request.CheckpointFilePath}' is damaged and can't be read. " +
                    "Check which cards already printed, then delete the file to start again.", ex);
            }

            if (loaded != null && loaded.Records.Count > 0)
            {
                EnsureCheckpointMatchesRequest(loaded, request, request.CheckpointFilePath);
                Log($"Loaded existing checkpoint from '{request.CheckpointFilePath}' ({loaded.CompletedCards}/{loaded.TotalCards} completed).");
                return loaded;
            }
        }

        // Create fresh checkpoint
        var fresh = new BatchCheckpoint
        {
            BatchRunId = request.BatchRunId,
            BatchName = request.BatchName,
            TargetPrinterName = request.Driver.TargetPrinterName ?? string.Empty,
            StartedAtUtc = DateTimeOffset.UtcNow,
            TotalCards = request.Cards.Count,
            Records = request.Cards.Select(c => new BatchRecordCheckpoint
            {
                RecordId = c.RecordId,
                BatchIndex = c.BatchIndex,
                Label = c.Label,
                Status = BatchRecordStatus.Pending
            }).ToList()
        };

        if (!string.IsNullOrWhiteSpace(request.CheckpointFilePath))
        {
            await _checkpointStore.SaveCheckpointAsync(fresh, request.CheckpointFilePath, ct);
        }

        return fresh;
    }

    /// <summary>
    /// Refuses to resume from a checkpoint written for a different set of cards, which would otherwise skip cards
    /// that were never printed.
    /// </summary>
    private static void EnsureCheckpointMatchesRequest(BatchCheckpoint checkpoint, BatchPrintRequest request, string filePath)
    {
        var requestIds = request.Cards.Select(c => c.RecordId).ToHashSet(StringComparer.Ordinal);
        var savedIds = checkpoint.Records.Select(r => r.RecordId).ToList();

        bool matches = savedIds.Count == requestIds.Count
                       && savedIds.Distinct(StringComparer.Ordinal).Count() == savedIds.Count
                       && savedIds.All(requestIds.Contains);

        if (!matches)
        {
            throw new InvalidOperationException(
                $"The checkpoint file '{filePath}' belongs to a different batch ('{checkpoint.BatchName}', {savedIds.Count} cards) " +
                $"than the one being started ({request.Cards.Count} cards). Use a new checkpoint file, or delete it if that batch is finished.");
        }
    }

    private static void EnsureUniqueRecordIds(IReadOnlyList<BatchCardItem> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card.RecordId))
            {
                throw new ArgumentException(
                    $"Card #{card.BatchIndex} has no RecordId. Every card in a batch needs a unique RecordId.", "request");
            }

            if (!seen.Add(card.RecordId))
            {
                throw new ArgumentException(
                    $"RecordId '{card.RecordId}' appears more than once. Every card in a batch needs a unique RecordId, " +
                    "otherwise later copies would be skipped as already printed.", "request");
            }
        }
    }

    private async Task SaveCheckpointAsync(BatchCheckpoint checkpoint, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            // Not cancellable: the state reached before a cancel must still be recorded
            await _checkpointStore.SaveCheckpointAsync(checkpoint, filePath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log($"Failed to persist checkpoint to '{filePath}': {ex.Message}");
        }
    }

    private void RaiseStateChanged(
        BatchExecutionState prev,
        BatchExecutionState curr,
        BatchPauseReason reason,
        string msg,
        int cardIdx,
        int total,
        BatchRecordCheckpoint? record = null)
    {
        StateChanged?.Invoke(this, new BatchExecutionEventArgs(prev, curr, reason, msg, cardIdx, total, record));
    }

    private void Log(string message)
    {
        _logAction?.Invoke($"[BatchPrintEngine] {DateTime.UtcNow:HH:mm:ss.fff} {message}");
    }
}
