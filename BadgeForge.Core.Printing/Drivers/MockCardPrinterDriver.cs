using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Drivers;

/// <summary>
/// Mock card printer driver implementation that does not require physical hardware.
/// Enables comprehensive testing of batch orchestration, hopper pause gates, error recovery,
/// and ribbon configuration.
/// </summary>
public class MockCardPrinterDriver : ICardPrinterDriver
{
    public string DriverId => "MOCK.PRINTER";
    public string DisplayName => "BadgeForge Mock Card Printer";
    public string? TargetPrinterName { get; private set; } = "Mock-SMART-31";

    public IRibbonModeController RibbonController { get; }
    public ICardPrinterStatusMonitor StatusMonitor => _mockStatusMonitor;
    public MockStatusMonitor MockStatus => _mockStatusMonitor;

    private readonly MockStatusMonitor _mockStatusMonitor;
    private readonly List<CardPrintJob> _submittedJobs = new();

    /// <summary>
    /// Simulated print delay in milliseconds to emulate mechanical card transport.
    /// Default is 0 for fast automated unit tests.
    /// </summary>
    public int SimulatedPrintDelayMs { get; set; } = 0;

    /// <summary>
    /// Probed capabilities returned during simulation. Can be customized to test mismatches.
    /// </summary>
    public PrinterCapabilities ProbedCapabilities { get; set; }

    /// <summary>
    /// Read-only history of submitted jobs for test assertions.
    /// </summary>
    public IReadOnlyList<CardPrintJob> SubmittedJobs => _submittedJobs.AsReadOnly();

    /// <summary>
    /// Card index at which a simulated paper jam fault will be thrown (0 = disabled).
    /// </summary>
    public int SimulatePaperJamAtBatchIndex { get; set; } = 0;

    /// <summary>
    /// Card index that stays "in the printer" (status Printing) once submitted, until <see cref="FinishHeldCard"/>
    /// or <see cref="AbortCurrentCardAsync"/> releases it, so a caller can act while a card is physically printing.
    /// 0 = every card ejects immediately, as before.
    /// </summary>
    public int HoldCardInPrinterAtBatchIndex { get; set; }

    /// <summary>
    /// How many times the printer was told to give up on the card it was printing.
    /// </summary>
    public int AbortedCardCount => _abortedCardCount;

    private int _abortedCardCount;

    public MockCardPrinterDriver(
        PrinterCapabilities? capabilities = null,
        int initialInputHopper = 80,
        int initialRibbonPercent = 100)
    {
        RibbonController = new DefaultRibbonModeController();
        _mockStatusMonitor = new MockStatusMonitor(initialInputHopper, initialRibbonPercent);

        // Baseline CR80 capabilities at 300 DPI
        int dpi = 300;
        int widthPx = (int)Math.Round((85.60 / 25.4) * dpi);  // 1011
        int heightPx = (int)Math.Round((53.98 / 25.4) * dpi); // 638

        ProbedCapabilities = capabilities ?? new PrinterCapabilities
        {
            PrinterName = "Mock-SMART-31",
            DriverName = "Mock IDP SMART-31 DTC",
            ResolutionDpiX = dpi,
            ResolutionDpiY = dpi,
            PrintableWidthPixels = widthPx,
            PrintableHeightPixels = heightPx,
            SupportsColor = true,
            SupportsDuplex = false,
            RawDriverProperties = new Dictionary<string, string>
            {
                ["IsMock"] = "True",
                ["Vendor"] = "Mock-IDP"
            }
        };
    }

    public Task<PrinterCapabilities> ProbeCapabilitiesAsync(string printerName, CancellationToken ct = default)
    {
        TargetPrinterName = printerName;
        return Task.FromResult(ProbedCapabilities with { PrinterName = printerName });
    }

    public async Task<CardPrintResult> SubmitCardAsync(CardPrintJob job, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 1. Validate format against probed capabilities - FAIL LOUDLY on mismatch
        ProbedCapabilities.EnsureCompatible(job.Format);

        // 2. Check for simulated fault triggers
        if (SimulatePaperJamAtBatchIndex > 0 && job.BatchIndex == SimulatePaperJamAtBatchIndex)
        {
            _mockStatusMonitor.ForceState(PrinterState.PaperJam, "Simulated card transport jam between capstan and print head");
            throw new PrinterHardwareFaultException(
                $"Hardware fault on card #{job.BatchIndex}: Paper transport jam.",
                "ERR_MOCK_JAM"
            );
        }

        // 3. Check status monitor for mandatory pause or errors
        var status = await _mockStatusMonitor.PollStatusAsync(ct);
        if (status.State == PrinterState.OperatorPauseRequired)
        {
            throw new OperatorPauseRequiredException(
                $"Cannot submit card #{job.BatchIndex}: Mandatory operator pause reached ({MockStatusMonitor.MandatoryPauseThreshold} cards printed). " +
                $"Clear output hopper (capacity: {MockStatusMonitor.MaxOutputHopperCapacity}) to resume.",
                status.CardsPrintedSincePause,
                MockStatusMonitor.MandatoryPauseThreshold
            );
        }

        // A card can't be fed from an empty hopper or printed without ribbon
        if ((status.InputHopperCount ?? MockStatusMonitor.MaxInputHopperCapacity) <= 0)
        {
            _mockStatusMonitor.ForceState(PrinterState.OutOfCards, "Input hopper empty. Load cards and press Resume.");
            throw new PrinterHardwareFaultException(
                $"Cannot submit card #{job.BatchIndex}: the input hopper is empty.",
                "ERR_MOCK_NO_CARDS"
            );
        }

        if ((status.RibbonRemainingPercent ?? 100) <= 0)
        {
            _mockStatusMonitor.ForceState(PrinterState.OutOfRibbon, "Ribbon exhausted. Replace the ribbon and press Resume.");
            throw new PrinterHardwareFaultException(
                $"Cannot submit card #{job.BatchIndex}: the ribbon is exhausted.",
                "ERR_MOCK_NO_RIBBON"
            );
        }

        if (status.RequiresOperatorAttention && status.State != PrinterState.Ready)
        {
            throw new PrinterHardwareFaultException(
                $"Cannot submit card #{job.BatchIndex}: Printer is in state '{status.State}' ({status.StatusMessage}).",
                status.State.ToString()
            );
        }

        // 4. Simulate print time
        if (SimulatedPrintDelayMs > 0)
        {
            await Task.Delay(SimulatedPrintDelayMs, ct);
        }

        // 5. Record job and card ejection
        _submittedJobs.Add(job);
        _mockStatusMonitor.RecordCardEjected();

        bool pauseTriggered = _mockStatusMonitor.CurrentStatus.CardsPrintedSincePause >= MockStatusMonitor.MandatoryPauseThreshold;
        string spoolerJobId = $"MOCK-JOB-{_submittedJobs.Count:D4}";

        // Simulated mechanical transport: the card stays in the printer until something releases it. Only taken when
        // nothing more important (a due hopper pause, an empty hopper) is already showing.
        if (HoldCardInPrinterAtBatchIndex == job.BatchIndex && _mockStatusMonitor.CurrentStatus.State == PrinterState.Ready)
        {
            _mockStatusMonitor.ForceState(PrinterState.Printing, $"Printing card #{job.BatchIndex}");
        }

        return CardPrintResult.Succeeded(job.JobId, spoolerJobId, pauseTriggered);
    }

    /// <summary>
    /// Releases a card held by <see cref="HoldCardsInPrinter"/>, as if it had finished printing and ejected.
    /// </summary>
    public void FinishHeldCard()
    {
        if (_mockStatusMonitor.CurrentStatus.State == PrinterState.Printing)
        {
            _mockStatusMonitor.ForceState(PrinterState.Ready, "Mock printer ready");
        }
    }

    /// <summary>
    /// Simulates giving up on the card in the printer: it stops printing and is ejected as it is.
    /// </summary>
    public Task<bool> AbortCurrentCardAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _abortedCardCount);
        if (_mockStatusMonitor.CurrentStatus.State == PrinterState.Printing)
        {
            _mockStatusMonitor.ForceState(PrinterState.Ready, "Card ejected part-printed after the operator stopped the run");
        }

        return Task.FromResult(true);
    }

    public void ClearSubmittedJobs() => _submittedJobs.Clear();

    public void Dispose()
    {
        _mockStatusMonitor.Dispose();
    }
}
