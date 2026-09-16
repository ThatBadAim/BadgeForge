using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Models;
using Xunit;

namespace BadgeForge.Tests;

public class MockCardPrinterDriverTests
{
    [Fact]
    public async Task SubmitCard_SucceedsAndRecordsSubmission()
    {
        using var driver = new MockCardPrinterDriver();
        var format = CardFormat.CR80;

        var job = new CardPrintJob
        {
            JobId = "TEST-001",
            Format = format,
            Label = "Test Staff Badge",
            BatchIndex = 1
        };

        var result = await driver.SubmitCardAsync(job);

        Assert.True(result.Success);
        Assert.Equal("TEST-001", result.JobId);
        Assert.Single(driver.SubmittedJobs);
        Assert.Equal(1, driver.StatusMonitor.CurrentStatus.CardsPrintedSincePause);
        Assert.Equal(1, driver.StatusMonitor.CurrentStatus.OutputHopperCount);
        Assert.Equal(79, driver.StatusMonitor.CurrentStatus.InputHopperCount);
    }

    [Fact]
    public async Task BatchSubmission_EnforcesMandatoryPause_At20Cards()
    {
        using var driver = new MockCardPrinterDriver();
        var format = CardFormat.CR80;

        // Print 20 cards
        for (int i = 1; i <= 20; i++)
        {
            var job = new CardPrintJob
            {
                JobId = $"JOB-{i:D3}",
                Format = format,
                Label = $"Card #{i}",
                BatchIndex = i
            };

            var result = await driver.SubmitCardAsync(job);
            Assert.True(result.Success);

            if (i == 20)
            {
                Assert.True(result.OperatorPauseTriggered);
            }
            else
            {
                Assert.False(result.OperatorPauseTriggered);
            }
        }

        // Verify status monitor now requires operator attention
        Assert.Equal(PrinterState.OperatorPauseRequired, driver.StatusMonitor.CurrentStatus.State);
        Assert.True(driver.StatusMonitor.CurrentStatus.RequiresOperatorAttention);
        Assert.Equal(20, driver.StatusMonitor.CurrentStatus.CardsPrintedSincePause);
        Assert.Equal(20, driver.StatusMonitor.CurrentStatus.OutputHopperCount);

        // Attempting to print the 21st card without clearing the pause must fail loudly
        var card21 = new CardPrintJob
        {
            JobId = "JOB-021",
            Format = format,
            Label = "Card #21",
            BatchIndex = 21
        };

        var pauseEx = await Assert.ThrowsAsync<OperatorPauseRequiredException>(() =>
            driver.SubmitCardAsync(card21));

        Assert.Equal(20, pauseEx.CardsPrintedInBatch);
        Assert.Equal(20, pauseEx.MaxCardsBeforePause);
        Assert.Contains("Mandatory operator pause reached", pauseEx.Message);

        // Operator clears output hopper and resets pause counter
        driver.StatusMonitor.ResetBatchPauseCounter();

        Assert.Equal(PrinterState.Ready, driver.StatusMonitor.CurrentStatus.State);
        Assert.False(driver.StatusMonitor.CurrentStatus.RequiresOperatorAttention);
        Assert.Equal(0, driver.StatusMonitor.CurrentStatus.CardsPrintedSincePause);
        Assert.Equal(0, driver.StatusMonitor.CurrentStatus.OutputHopperCount);

        // Card 21 should now print successfully
        var result21 = await driver.SubmitCardAsync(card21);
        Assert.True(result21.Success);
        Assert.Equal(1, driver.StatusMonitor.CurrentStatus.CardsPrintedSincePause);
    }

    [Fact]
    public async Task SimulatedHardwareFault_PaperJam_ThrowsAndHalts()
    {
        using var driver = new MockCardPrinterDriver
        {
            SimulatePaperJamAtBatchIndex = 3
        };

        var format = CardFormat.CR80;

        await driver.SubmitCardAsync(new CardPrintJob { JobId = "J1", Format = format, BatchIndex = 1 });
        await driver.SubmitCardAsync(new CardPrintJob { JobId = "J2", Format = format, BatchIndex = 2 });

        var jamEx = await Assert.ThrowsAsync<PrinterHardwareFaultException>(() =>
            driver.SubmitCardAsync(new CardPrintJob { JobId = "J3", Format = format, BatchIndex = 3 }));

        Assert.Equal("ERR_MOCK_JAM", jamEx.FaultCode);
        Assert.Equal(PrinterState.PaperJam, driver.StatusMonitor.CurrentStatus.State);
        Assert.True(driver.StatusMonitor.CurrentStatus.RequiresOperatorAttention);
    }
}
