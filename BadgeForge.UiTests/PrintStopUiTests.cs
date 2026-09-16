using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BadgeForge.App;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using Xunit;

namespace BadgeForge.UiTests;

/// <summary>
/// Stopping a print run from the real window's view model. The engine's own tests cover the state machine; these
/// check the operator's side of it — that the prompt appears while the card is still in the printer, that both
/// answers reach the engine, and that the list shows what happened to that card.
/// </summary>
public class PrintStopUiTests
{
    private sealed record Harness(MainWindow Window, MainWindowViewModel Vm, MockCardPrinterDriver Driver);

    private static Harness Open(int people, int holdCardAtIndex)
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        for (int i = 1; i <= people; i++)
        {
            var person = vm.AddPerson();
            person.Cells.Single(c => c.Token == "FirstName").Value = $"Person{i}";
            person.Cells.Single(c => c.Token == "LastName").Value = "Test";
            person.Cells.Single(c => c.Token == "EmployeeId").Value = $"E-{i}";
        }

        // The window answers pre-flight warnings with a modal dialog; nobody is here to click it
        vm.ConfirmPrintWithWarningsAsync = _ => Task.FromResult(true);

        var driver = Assert.IsType<MockCardPrinterDriver>(vm.ActiveDriver);
        driver.HoldCardInPrinterAtBatchIndex = holdCardAtIndex;
        return new Harness(window, vm, driver);
    }

    /// <summary>Runs the dispatcher until the condition holds, so the print run can make progress.</summary>
    private static void PumpUntil(Func<bool> condition, string what, int timeoutMs = 20000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            Thread.Sleep(1);
        }

        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Stop_AsksAboutTheCardInThePrinter_AndEjectingItMarksThatPersonForReprinting() =>
        HeadlessTestApp.RunOnUiThread(() =>
        {
            var h = Open(people: 3, holdCardAtIndex: 2);
            try
            {
                var run = h.Vm.StartBatchRunAsync();

                // Card 2 is in the printer now
                PumpUntil(() => h.Driver.SubmittedJobs.Count == 2, "the second card to be sent");

                var stop = h.Vm.StopBatchRunAsync();
                PumpUntil(() => h.Vm.StopPromptVisible, "the stop prompt");

                Assert.Contains("card 2 of 3", h.Vm.StopPromptMessage);
                Assert.False(h.Vm.IsBatchPausedByUser);   // the answer here is Finish or Eject, never Resume
                Assert.False(h.Vm.CanStopBatchRun);

                var eject = h.Vm.EjectCurrentCardAsync();
                PumpUntil(() => run.IsCompleted, "the run to stop");
                Assert.True(stop.IsCompleted && eject.IsCompleted);

                Assert.Equal(1, h.Driver.AbortedCardCount);
                Assert.Equal(2, h.Driver.SubmittedJobs.Count);      // the third card was never sent
                Assert.Equal(BatchExecutionState.Cancelled, h.Vm.BatchState);
                Assert.False(h.Vm.StopPromptVisible);
                Assert.True(h.Vm.CanStartBatch);

                // The list says which badge came out unfinished
                Assert.Equal(nameof(BatchRecordStatus.Success), h.Vm.People[0].PrintStatus);
                Assert.Equal(nameof(BatchRecordStatus.Failed), h.Vm.People[1].PrintStatus);
                Assert.Equal(nameof(BatchRecordStatus.Pending), h.Vm.People[2].PrintStatus);
                Assert.Contains("ejected part-printed", h.Vm.People[1].ErrorMessage);
            }
            finally
            {
                h.Window.Close();
            }
        });

    [Fact]
    public Task Stop_ThenLettingTheCardFinish_CountsItAsPrinted() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open(people: 3, holdCardAtIndex: 2);
        try
        {
            var run = h.Vm.StartBatchRunAsync();
            PumpUntil(() => h.Driver.SubmittedJobs.Count == 2, "the second card to be sent");

            var stop = h.Vm.StopBatchRunAsync();
            PumpUntil(() => h.Vm.StopPromptVisible, "the stop prompt");

            // The operator lets it finish: it comes out of the printer, then the run stops
            h.Driver.FinishHeldCard();
            var finish = h.Vm.FinishCurrentCardAsync();
            PumpUntil(() => run.IsCompleted, "the run to stop");
            Assert.True(stop.IsCompleted && finish.IsCompleted);

            Assert.Equal(0, h.Driver.AbortedCardCount);
            Assert.Equal(2, h.Driver.SubmittedJobs.Count);
            Assert.Equal(BatchExecutionState.Cancelled, h.Vm.BatchState);
            Assert.Equal(nameof(BatchRecordStatus.Success), h.Vm.People[1].PrintStatus);
            Assert.Equal(nameof(BatchRecordStatus.Pending), h.Vm.People[2].PrintStatus);
        }
        finally
        {
            h.Window.Close();
        }
    });
}
