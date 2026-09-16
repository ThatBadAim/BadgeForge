using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;
using Xunit;

namespace BadgeForge.Tests;

public class BatchPrintEngineFaultHandlingTests
{
    private static List<BatchCardItem> CreateTestCards(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new BatchCardItem
            {
                RecordId = $"REC-{i:D4}",
                BatchIndex = i,
                Label = $"Employee Badge #{i}",
                Format = CardFormat.CR80,
                FrontPixelBuffer = new byte[100]
            })
            .ToList();

    [Fact]
    public async Task FaultBeforeTheCardIsSent_PausesOnError_AndRetriesAfterResume()
    {
        using var mockDriver = new MockCardPrinterDriver();
        mockDriver.MockStatus.ForceState(PrinterState.CoverOpen, "Cover open");
        var engine = new BatchPrintEngine();
        var pauseReasons = new List<BatchPauseReason>();

        engine.StateChanged += (_, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                pauseReasons.Add(e.PauseReason);
                // Resume the instant the pause is announced: the signal must not be lost
                _ = engine.ResumeAsync();
            }
        };

        var summary = await engine.ExecuteBatchAsync(new BatchPrintRequest { Cards = CreateTestCards(2), Driver = mockDriver });

        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(new[] { BatchPauseReason.HardwareError }, pauseReasons);
        Assert.Equal(new[] { 1, 2 }, mockDriver.SubmittedJobs.Select(j => j.BatchIndex));
    }

    [Fact]
    public async Task FaultAfterTheCardIsAccepted_ResumeDoesNotPrintItAgain()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var driver = new JamAfterFirstCardDriver(mockDriver);
        var engine = new BatchPrintEngine();
        BatchPauseReason? pauseReason = null;

        engine.StateChanged += (_, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                pauseReason = e.PauseReason;
                _ = engine.ResumeAsync(); // operator checked the card: it came out fine
            }
        };

        var summary = await engine.ExecuteBatchAsync(new BatchPrintRequest { Cards = CreateTestCards(2), Driver = driver });

        Assert.Equal(BatchPauseReason.CardOutcomeUnknown, pauseReason);
        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(2, summary.CompletedCards);
        Assert.Equal(new[] { 1, 2 }, mockDriver.SubmittedJobs.Select(j => j.BatchIndex));
    }

    [Fact]
    public async Task FaultAfterTheCardIsAccepted_ReprintSendsItAgain()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var driver = new JamAfterFirstCardDriver(mockDriver);
        var engine = new BatchPrintEngine();

        engine.StateChanged += (_, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                _ = engine.ReprintCardAsync(); // operator checked the card: it was ruined
            }
        };

        var summary = await engine.ExecuteBatchAsync(new BatchPrintRequest { Cards = CreateTestCards(2), Driver = driver });

        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(new[] { 1, 1, 2 }, mockDriver.SubmittedJobs.Select(j => j.BatchIndex));
        Assert.Equal(1, summary.Checkpoint.Records[0].RetryCount);
    }

    [Fact]
    public async Task LongBatch_EmptiesTheHopperAndReloadsCards_AndFinishes()
    {
        // Previously the simulated input hopper never refilled, so the batch died at card 82
        using var mockDriver = new MockCardPrinterDriver();
        var engine = new BatchPrintEngine();
        int hopperPauses = 0;
        int errorPauses = 0;

        engine.StateChanged += (_, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedForHopper)
            {
                hopperPauses++;
                _ = engine.ResumeAsync();
            }
            else if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                errorPauses++;
                _ = engine.ResumeAsync(); // cards loaded
            }
        };

        var summary = await engine.ExecuteBatchAsync(new BatchPrintRequest
        {
            Cards = CreateTestCards(100),
            Driver = mockDriver,
            StatusPollingInterval = TimeSpan.FromMilliseconds(1)
        });

        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(100, summary.CompletedCards);
        Assert.Equal(100, mockDriver.SubmittedJobs.Count);
        Assert.Equal(4, hopperPauses);  // after cards 20, 40, 60 and 80
        Assert.Equal(1, errorPauses);   // input hopper empty after card 80
    }

    [Fact]
    public async Task SecondRunOnTheSameEngine_IsRejectedWhileTheFirstIsActive()
    {
        using var mockDriver = new MockCardPrinterDriver();
        mockDriver.MockStatus.ForceState(PrinterState.CoverOpen, "Cover open");
        var engine = new BatchPrintEngine();
        var pausedOnError = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.StateChanged += (_, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                pausedOnError.TrySetResult();
            }
        };

        var firstRun = engine.ExecuteBatchAsync(new BatchPrintRequest { Cards = CreateTestCards(1), Driver = mockDriver });
        await pausedOnError.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteBatchAsync(new BatchPrintRequest { Cards = CreateTestCards(1), Driver = mockDriver }));

        await engine.CancelAsync();
        var summary = await firstRun.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Empty(mockDriver.SubmittedJobs);
    }

    [Fact]
    public async Task CheckpointFromADifferentBatch_IsRejected_AndNothingPrints()
    {
        string checkpointPath = Path.Combine(Path.GetTempPath(), $"other_batch_{Guid.NewGuid():N}.json");
        try
        {
            await new JsonBatchCheckpointStore().SaveCheckpointAsync(new BatchCheckpoint
            {
                BatchName = "Yesterday's batch",
                TotalCards = 1,
                Records = { new BatchRecordCheckpoint { RecordId = "SOMEONE-ELSE", Status = BatchRecordStatus.Success } }
            }, checkpointPath);

            using var mockDriver = new MockCardPrinterDriver();
            var engine = new BatchPrintEngine();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ExecuteBatchAsync(new BatchPrintRequest
            {
                Cards = CreateTestCards(3),
                Driver = mockDriver,
                CheckpointFilePath = checkpointPath
            }));

            Assert.Contains("different batch", ex.Message);
            Assert.Empty(mockDriver.SubmittedJobs);
            Assert.Equal(BatchExecutionState.Idle, engine.State);
        }
        finally
        {
            File.Delete(checkpointPath);
        }
    }

    [Fact]
    public async Task RepeatedRecordIds_AreRejectedBeforeAnythingPrints()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var cards = CreateTestCards(2);
        cards[1] = cards[1] with { RecordId = cards[0].RecordId };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new BatchPrintEngine().ExecuteBatchAsync(new BatchPrintRequest { Cards = cards, Driver = mockDriver }));

        Assert.Empty(mockDriver.SubmittedJobs);
    }

    [Fact]
    public async Task CardImages_AreRenderedOnlyWhenTheEngineReachesThem()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var renderedBeforeSubmission = new List<int>();
        var cards = Enumerable.Range(1, 3).Select(i => new BatchCardItem
        {
            RecordId = $"REC-{i}",
            BatchIndex = i,
            Format = CardFormat.CR80,
            RenderFrontAsync = _ =>
            {
                renderedBeforeSubmission.Add(mockDriver.SubmittedJobs.Count);
                return Task.FromResult(new byte[] { (byte)i });
            }
        }).ToList();

        await new BatchPrintEngine().ExecuteBatchAsync(new BatchPrintRequest { Cards = cards, Driver = mockDriver });

        Assert.Equal(new[] { 0, 1, 2 }, renderedBeforeSubmission);
        Assert.Equal(new byte[] { 3 }, mockDriver.SubmittedJobs[2].FrontPixelBuffer);
    }

    /// <summary>
    /// Takes the first card, then reports a jam while it is being ejected.
    /// </summary>
    private sealed class JamAfterFirstCardDriver : ICardPrinterDriver
    {
        private readonly MockCardPrinterDriver _inner;
        private bool _jammed;

        public JamAfterFirstCardDriver(MockCardPrinterDriver inner) => _inner = inner;

        public string DriverId => _inner.DriverId;
        public string DisplayName => _inner.DisplayName;
        public string? TargetPrinterName => _inner.TargetPrinterName;
        public IRibbonModeController RibbonController => _inner.RibbonController;
        public ICardPrinterStatusMonitor StatusMonitor => _inner.StatusMonitor;

        public Task<PrinterCapabilities> ProbeCapabilitiesAsync(string printerName, CancellationToken ct = default) =>
            _inner.ProbeCapabilitiesAsync(printerName, ct);

        public async Task<CardPrintResult> SubmitCardAsync(CardPrintJob job, CancellationToken ct = default)
        {
            var result = await _inner.SubmitCardAsync(job, ct);
            if (!_jammed)
            {
                _jammed = true;
                _inner.MockStatus.ForceState(PrinterState.PaperJam, "Jam while ejecting the card");
            }

            return result;
        }

        public void Dispose()
        {
        }
    }
}
