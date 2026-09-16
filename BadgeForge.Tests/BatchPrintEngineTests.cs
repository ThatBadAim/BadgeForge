using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Models;
using Xunit;

namespace BadgeForge.Tests;

public class BatchPrintEngineTests
{
    private static List<BatchCardItem> CreateTestCards(int count)
    {
        var list = new List<BatchCardItem>(count);
        for (int i = 1; i <= count; i++)
        {
            list.Add(new BatchCardItem
            {
                RecordId = $"REC-{i:D4}",
                BatchIndex = i,
                Label = $"Employee Badge #{i}",
                Format = CardFormat.CR80,
                FrontPixelBuffer = new byte[100], // Minimal payload for mock
                Metadata = new Dictionary<string, string>
                {
                    ["EmployeeId"] = $"EMP-{i:D4}"
                }
            });
        }
        return list;
    }

    [Fact]
    public async Task SequentialExecution_PrintsCardsOneAtATime_AndRecordsCorrelatedJobs()
    {
        // Arrange
        using var mockDriver = new MockCardPrinterDriver();
        var engine = new BatchPrintEngine();
        var cards = CreateTestCards(5);

        var request = new BatchPrintRequest
        {
            BatchName = "Small Test Batch",
            Cards = cards,
            Driver = mockDriver,
            MandatoryPauseThreshold = 20
        };

        // Act
        var summary = await engine.ExecuteBatchAsync(request);

        // Assert
        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(5, summary.TotalCards);
        Assert.Equal(5, summary.CompletedCards);
        Assert.Equal(0, summary.FailedCards);

        // Verify driver received exactly 5 jobs sequentially
        Assert.Equal(5, mockDriver.SubmittedJobs.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i + 1, mockDriver.SubmittedJobs[i].BatchIndex);
            Assert.Contains(cards[i].RecordId, mockDriver.SubmittedJobs[i].JobId);
            Assert.StartsWith("BF_", mockDriver.SubmittedJobs[i].JobId);
        }
    }

    [Fact]
    public async Task UserPause_FinishesCurrentCard_HoldsUntilResume_ThenCompletes()
    {
        using var mockDriver = new MockCardPrinterDriver();
        var engine = new BatchPrintEngine();
        int pauseCount = 0;
        int submittedAtPause = -1;
        int submittedWhilePaused = -1;
        BatchPauseReason pauseReason = BatchPauseReason.None;
        Task? resumeTask = null;

        engine.StateChanged += (_, e) =>
        {
            if (e.CurrentState == BatchExecutionState.Running &&
                e.CurrentRecord?.Status == BatchRecordStatus.Success &&
                e.CurrentCardIndex == 2)
            {
                engine.PauseAsync().GetAwaiter().GetResult();
            }
            else if (e.CurrentState == BatchExecutionState.PausedByUser)
            {
                pauseCount++;
                submittedAtPause = mockDriver.SubmittedJobs.Count;
                pauseReason = e.PauseReason;

                resumeTask = Task.Run(async () =>
                {
                    await Task.Delay(150);
                    submittedWhilePaused = mockDriver.SubmittedJobs.Count;
                    await engine.ResumeAsync();
                });
            }
        };

        var summary = await engine.ExecuteBatchAsync(new BatchPrintRequest
        {
            BatchName = "User Pause Test",
            Cards = CreateTestCards(5),
            Driver = mockDriver,
            MandatoryPauseThreshold = 20
        });
        await resumeTask!;

        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(1, pauseCount);
        Assert.Equal(BatchPauseReason.UserRequested, pauseReason);
        Assert.Equal(2, submittedAtPause);
        Assert.Equal(2, submittedWhilePaused);
        Assert.Equal(5, mockDriver.SubmittedJobs.Count);
    }

    [Fact]
    public async Task PauseAsync_WhenIdle_IsIgnored()
    {
        var engine = new BatchPrintEngine();
        await engine.PauseAsync();
        Assert.Equal(BatchExecutionState.Idle, engine.State);
    }

    [Fact]
    public async Task MandatoryPause_EnforcesPauseAt20Cards_AndResumesSuccessfully()
    {
        // Arrange: 25 cards (should pause after card 20)
        using var mockDriver = new MockCardPrinterDriver();
        var engine = new BatchPrintEngine();
        var cards = CreateTestCards(25);

        bool pauseTriggered = false;
        int cardsAtPause = 0;
        string pausePrompt = string.Empty;

        engine.StateChanged += async (sender, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedForHopper)
            {
                pauseTriggered = true;
                cardsAtPause = mockDriver.SubmittedJobs.Count;
                pausePrompt = e.Message;

                // Emulate operator emptying output hopper and clicking Resume after 100ms
                await Task.Delay(100);
                await engine.ResumeAsync();
            }
        };

        var request = new BatchPrintRequest
        {
            BatchName = "25-Card Hopper Pause Test",
            Cards = cards,
            Driver = mockDriver,
            MandatoryPauseThreshold = 20
        };

        // Act
        var summary = await engine.ExecuteBatchAsync(request);

        // Assert:
        Assert.True(pauseTriggered, "Engine must enforce mandatory hopper pause.");
        Assert.Equal(20, cardsAtPause);
        Assert.Contains("20", pausePrompt);
        Assert.Contains("output hopper", pausePrompt, StringComparison.OrdinalIgnoreCase);

        // All 25 cards eventually finished after resume
        Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
        Assert.Equal(25, summary.CompletedCards);
        Assert.Equal(25, mockDriver.SubmittedJobs.Count);
    }

    [Fact]
    public async Task CrashResume_SkipsAlreadyPrintedCards_AndResumesUnfinishedWork()
    {
        // Arrange
        string tempCheckpointPath = Path.Combine(Path.GetTempPath(), $"checkpoint_{Guid.NewGuid():N}.json");
        var cards = CreateTestCards(8);

        try
        {
            // Run 1: Cancel after 3 cards have printed to simulate sudden crash / termination
            using var mockDriver1 = new MockCardPrinterDriver();
            var engine1 = new BatchPrintEngine();
            using var cts = new CancellationTokenSource();

            int completedInRun1 = 0;
            engine1.StateChanged += (sender, e) =>
            {
                if (e.CurrentRecord?.Status == BatchRecordStatus.Success)
                {
                    completedInRun1++;
                    if (completedInRun1 == 3)
                    {
                        // Simulate crash/halt immediately after card 3 finishes
                        cts.Cancel();
                    }
                }
            };

            var request1 = new BatchPrintRequest
            {
                BatchRunId = "CRASH-TEST-RUN-1",
                BatchName = "Crash Recovery Test",
                Cards = cards,
                Driver = mockDriver1,
                CheckpointFilePath = tempCheckpointPath,
                AutoResumeFromCheckpoint = true
            };

            var summary1 = await engine1.ExecuteBatchAsync(request1, cts.Token);
            Assert.Equal(BatchExecutionState.Cancelled, summary1.FinalState);
            Assert.Equal(3, mockDriver1.SubmittedJobs.Count);

            // Verify checkpoint on disk has 3 completed records
            var store = new JsonBatchCheckpointStore();
            var savedCheckpoint = await store.LoadCheckpointAsync(tempCheckpointPath);
            Assert.NotNull(savedCheckpoint);
            Assert.Equal(3, savedCheckpoint.CompletedCards);
            Assert.Equal(5, savedCheckpoint.PendingCards);

            // Run 2: Fresh engine instance resumes using the exact same checkpoint file
            using var mockDriver2 = new MockCardPrinterDriver();
            var engine2 = new BatchPrintEngine();

            var request2 = new BatchPrintRequest
            {
                BatchRunId = "CRASH-TEST-RUN-1",
                BatchName = "Crash Recovery Test",
                Cards = cards,
                Driver = mockDriver2,
                CheckpointFilePath = tempCheckpointPath,
                AutoResumeFromCheckpoint = true
            };

            var summary2 = await engine2.ExecuteBatchAsync(request2);

            // Assert: Run 2 resumes and finishes all 8 cards
            Assert.Equal(BatchExecutionState.Completed, summary2.FinalState);
            Assert.Equal(8, summary2.TotalCards);
            Assert.Equal(8, summary2.CompletedCards);

            // Crucial: mockDriver2 only received cards 4 through 8 (5 submissions, NO duplicate prints!)
            Assert.Equal(5, mockDriver2.SubmittedJobs.Count);
            Assert.Equal("REC-0004", cards[mockDriver2.SubmittedJobs[0].BatchIndex - 1].RecordId);
            Assert.Equal("REC-0008", cards[mockDriver2.SubmittedJobs[4].BatchIndex - 1].RecordId);
        }
        finally
        {
            if (File.Exists(tempCheckpointPath))
            {
                try { File.Delete(tempCheckpointPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task PauseOnError_HaltsBatch_SurfacesClearMessage_AndAllowsRetryOnResume()
    {
        // Arrange
        using var mockDriver = new MockCardPrinterDriver();
        // Simulate a paper jam on card #3
        mockDriver.SimulatePaperJamAtBatchIndex = 3;

        var engine = new BatchPrintEngine();
        var cards = CreateTestCards(5);

        string tempCheckpoint = Path.Combine(Path.GetTempPath(), $"jam_checkpoint_{Guid.NewGuid():N}.json");

        bool errorPauseTriggered = false;
        string surfacedErrorMessage = string.Empty;
        int failingCardIndex = 0;

        engine.StateChanged += async (sender, e) =>
        {
            if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                errorPauseTriggered = true;
                surfacedErrorMessage = e.Message;
                failingCardIndex = e.CurrentCardIndex;

                // Emulate operator opening printer, clearing jammed card, clearing fault state, then resuming
                mockDriver.SimulatePaperJamAtBatchIndex = 0; // Clear simulated fault
                mockDriver.MockStatus.ForceState(PrinterState.Ready, "Card transport cleared by operator");

                await Task.Delay(100);
                await engine.ResumeAsync();
            }
        };

        var request = new BatchPrintRequest
        {
            BatchName = "Paper Jam Error Recovery",
            Cards = cards,
            Driver = mockDriver,
            CheckpointFilePath = tempCheckpoint
        };

        try
        {
            // Act
            var summary = await engine.ExecuteBatchAsync(request);

            // Assert:
            Assert.True(errorPauseTriggered, "Engine must halt on hardware fault.");
            Assert.Equal(3, failingCardIndex);
            Assert.Contains("Paper transport jam", surfacedErrorMessage);
            Assert.Contains("REC-0003", surfacedErrorMessage);

            // Once resolved and resumed, all 5 cards complete successfully
            Assert.Equal(BatchExecutionState.Completed, summary.FinalState);
            Assert.Equal(5, summary.CompletedCards);
            Assert.Equal(0, summary.FailedCards);
        }
        finally
        {
            if (File.Exists(tempCheckpoint))
            {
                try { File.Delete(tempCheckpoint); } catch { }
            }
        }
    }
}
