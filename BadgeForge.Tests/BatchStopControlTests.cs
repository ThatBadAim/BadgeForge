using System.Diagnostics;
using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Models;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// Stopping a run on the card that is being printed right now, and choosing what happens to that card.
/// Pause is the polite option (finish the card, hold before the next one); Stop is the "something is wrong" option
/// and has to land on the card in the printer, not after it.
/// </summary>
public class BatchStopControlTests
{
    private static List<BatchCardItem> Cards(int count) =>
        Enumerable.Range(1, count).Select(i => new BatchCardItem
        {
            RecordId = $"REC-{i:D4}",
            BatchIndex = i,
            Label = $"Badge #{i}",
            Format = CardFormat.CR80,
            FrontPixelBuffer = new byte[64]
        }).ToList();

    private static BatchPrintRequest Request(MockCardPrinterDriver driver, int cards) => new()
    {
        BatchName = "Stop Test",
        Cards = Cards(cards),
        Driver = driver,
        MandatoryPauseThreshold = 20,
        StatusPollingInterval = TimeSpan.FromMilliseconds(5),
        CardCompletionTimeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Runs the batch, calling <paramref name="onCardPrinting"/> once the given card is in the printer.
    /// </summary>
    private static async Task<(BatchExecutionSummary Summary, List<BatchExecutionEventArgs> Events)> RunAsync(
        BatchPrintEngine engine,
        BatchPrintRequest request,
        int actOnCardIndex,
        Func<Task> onCardPrinting)
    {
        var events = new List<BatchExecutionEventArgs>();
        Task? action = null;

        engine.StateChanged += (_, e) =>
        {
            lock (events)
            {
                events.Add(e);
            }

            if (action == null && e.CurrentCardIndex == actOnCardIndex &&
                e.CurrentRecord?.Status == BatchRecordStatus.Printing)
            {
                action = Task.Run(onCardPrinting);
            }
        };

        var summary = await engine.ExecuteBatchAsync(request);
        if (action != null)
        {
            await action;
        }

        return (summary, events);
    }

    [Fact]
    public async Task Stop_WhileACardIsPrinting_HoldsOnThatCard_AndAsksWhatToDoWithIt()
    {
        using var driver = new MockCardPrinterDriver { HoldCardInPrinterAtBatchIndex = 2 };
        var engine = new BatchPrintEngine();
        int submittedAtPrompt = -1;
        BatchRecordStatus? statusAtPrompt = null;

        engine.StateChanged += (_, e) =>
        {
            if (e.PauseReason == BatchPauseReason.StopRequested)
            {
                submittedAtPrompt = driver.SubmittedJobs.Count;
                statusAtPrompt = e.CurrentRecord?.Status;
            }
        };

        var (summary, _) = await RunAsync(engine, Request(driver, 5), actOnCardIndex: 2, onCardPrinting: async () =>
        {
            await engine.StopAsync();

            // The prompt has to arrive while the card is still in the printer, not after the run unwinds
            await WaitUntil(() => engine.State == BatchExecutionState.PausedByUser);
            await engine.AbortCurrentCardAsync();
        });

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Equal(BatchRecordStatus.Printing, statusAtPrompt);
        Assert.Equal(2, submittedAtPrompt);
        Assert.Equal(2, driver.SubmittedJobs.Count);   // card 3 was never sent
    }

    [Fact]
    public async Task Stop_ThenFinishTheCard_CountsItAsPrinted_AndSendsNoMore()
    {
        using var driver = new MockCardPrinterDriver { HoldCardInPrinterAtBatchIndex = 2 };
        var engine = new BatchPrintEngine();
        BatchPauseReason promptReason = BatchPauseReason.None;
        string promptMessage = string.Empty;

        engine.StateChanged += (_, e) =>
        {
            if (e.PauseReason == BatchPauseReason.StopRequested)
            {
                promptReason = e.PauseReason;
                promptMessage = e.Message;
            }
        };

        var (summary, _) = await RunAsync(engine, Request(driver, 5), actOnCardIndex: 2, onCardPrinting: async () =>
        {
            await engine.StopAsync();
            await WaitUntil(() => engine.State == BatchExecutionState.PausedByUser);

            // The operator lets it finish: the card comes out of the printer, then the run stops
            driver.FinishHeldCard();
            await engine.FinishCurrentCardAsync();
        });

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Equal(BatchPauseReason.StopRequested, promptReason);
        Assert.Contains("card 2 of 5", promptMessage);

        Assert.Equal(2, summary.CompletedCards);
        Assert.Equal(BatchRecordStatus.Success, summary.Checkpoint.Records[1].Status);
        Assert.Equal(BatchRecordStatus.Pending, summary.Checkpoint.Records[2].Status);
        Assert.Equal(2, driver.SubmittedJobs.Count);
        Assert.Equal(0, driver.AbortedCardCount);
        Assert.Contains("Stopped after card 2", summary.TerminalMessage);
    }

    [Fact]
    public async Task Stop_ThenEjectTheCard_TellsThePrinterToGiveUp_AndMarksItForReprinting()
    {
        using var driver = new MockCardPrinterDriver { HoldCardInPrinterAtBatchIndex = 2 };
        var engine = new BatchPrintEngine();

        var (summary, _) = await RunAsync(engine, Request(driver, 5), actOnCardIndex: 2, onCardPrinting: async () =>
        {
            await engine.StopAsync();
            await WaitUntil(() => engine.State == BatchExecutionState.PausedByUser);
            await engine.AbortCurrentCardAsync();
        });

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Equal(1, driver.AbortedCardCount);

        var stopped = summary.Checkpoint.Records[1];
        Assert.Equal(BatchRecordStatus.Failed, stopped.Status);
        Assert.Contains("ejected part-printed", stopped.ErrorMessage);

        // Only the first card printed; the stopped one and everything after it are still to do
        Assert.Equal(1, summary.CompletedCards);
        Assert.Equal(2, driver.SubmittedJobs.Count);
        Assert.Contains("needs printing again", summary.TerminalMessage);
    }

    [Fact]
    public async Task Stop_LandsWithinAPollingTick_NotAfterTheCardCompletionTimeout()
    {
        using var driver = new MockCardPrinterDriver { HoldCardInPrinterAtBatchIndex = 1 };
        var engine = new BatchPrintEngine();
        var request = Request(driver, 3) with { CardCompletionTimeout = TimeSpan.FromSeconds(30) };
        var stopwatch = new Stopwatch();

        var (summary, _) = await RunAsync(engine, request, actOnCardIndex: 1, onCardPrinting: async () =>
        {
            stopwatch.Start();
            await engine.StopAsync();
            await WaitUntil(() => engine.State == BatchExecutionState.PausedByUser);
            stopwatch.Stop();
            await engine.AbortCurrentCardAsync();
        });

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);

        // The old behaviour only noticed between cards — here that would have meant waiting out the 30s card timeout
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Stop took {stopwatch.ElapsedMilliseconds} ms to be honoured.");
    }

    [Fact]
    public async Task Stop_BetweenCards_EndsTheRunWithNothingLeftInThePrinter()
    {
        using var driver = new MockCardPrinterDriver();
        var engine = new BatchPrintEngine();
        var events = new List<BatchExecutionEventArgs>();
        Task? stopTask = null;

        engine.StateChanged += (_, e) =>
        {
            lock (events)
            {
                events.Add(e);
            }

            // Card 2 has come out; stop before card 3 is sent
            if (stopTask == null && e.CurrentCardIndex == 2 && e.CurrentRecord?.Status == BatchRecordStatus.Success)
            {
                stopTask = engine.StopAsync();
            }
        };

        var summary = await engine.ExecuteBatchAsync(Request(driver, 5));
        if (stopTask != null)
        {
            await stopTask;
        }

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Equal(2, driver.SubmittedJobs.Count);
        Assert.Equal(0, driver.AbortedCardCount);

        // No decision is needed when nothing is in the printer
        Assert.DoesNotContain(events, e => e.PauseReason == BatchPauseReason.StopRequested);
        Assert.Contains("nothing was left in the printer", summary.TerminalMessage);
    }

    [Fact]
    public async Task Stop_WhileHoldingAtAHopperPause_EndsTheRun()
    {
        using var driver = new MockCardPrinterDriver();
        var engine = new BatchPrintEngine();
        Task? stopTask = null;

        engine.StateChanged += (_, e) =>
        {
            if (stopTask == null && e.CurrentState == BatchExecutionState.PausedForHopper)
            {
                stopTask = engine.StopAsync();
            }
        };

        var request = Request(driver, 5) with { MandatoryPauseThreshold = 2 };
        var summary = await engine.ExecuteBatchAsync(request);
        if (stopTask != null)
        {
            await stopTask;
        }

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Equal(2, driver.SubmittedJobs.Count);
        Assert.Equal(0, driver.AbortedCardCount);
    }

    [Fact]
    public async Task ResumeCannotAnswerAStopPrompt_AndFinishCannotAnswerAHopperPause()
    {
        using var driver = new MockCardPrinterDriver { HoldCardInPrinterAtBatchIndex = 1 };
        var engine = new BatchPrintEngine();

        var (summary, _) = await RunAsync(engine, Request(driver, 3), actOnCardIndex: 1, onCardPrinting: async () =>
        {
            await engine.StopAsync();
            await WaitUntil(() => engine.State == BatchExecutionState.PausedByUser);

            // A stray Resume must not decide the fate of a card that is still in the printer
            await engine.ResumeAsync();
            await Task.Delay(60);
            Assert.Equal(BatchExecutionState.PausedByUser, engine.State);

            await engine.AbortCurrentCardAsync();
        });

        Assert.Equal(BatchExecutionState.Cancelled, summary.FinalState);
        Assert.Equal(1, driver.AbortedCardCount);
    }

    [Fact]
    public async Task FinishCurrentCard_WhenNothingIsPrinting_IsIgnored()
    {
        var engine = new BatchPrintEngine();

        await engine.FinishCurrentCardAsync();
        await engine.AbortCurrentCardAsync();
        await engine.StopAsync();

        Assert.Equal(BatchExecutionState.Idle, engine.State);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The engine never reached the expected state.");
            }

            await Task.Delay(5);
        }
    }
}
