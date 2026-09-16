using System.Runtime.Versioning;
using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;
using SkiaSharp;

namespace BadgeForge.Core.Printing.Drivers;

/// <summary>
/// Concrete implementation of <see cref="ICardPrinterDriver"/> for the IDP SMART-31 Direct-To-Card (DTC) printer.
///
/// Physical Constraints:
/// - Input hopper: 80 cards.
/// - Output hopper: 25 cards.
/// - Mandatory operator pause: Every 20 cards.
/// - Canvas resolution: Dynamically queried at runtime from driver (never hardcoded).
/// - K-resin routing: Supports both RGB(0,0,0) pixel extraction and driver DEVMODE resin flags.
///
/// Each card is spooled as its own job through the Windows print spooler. When the printer isn't installed (or the
/// host isn't Windows) the probe returns a simulated CR80 profile so previews still work, and printing fails loudly.
/// </summary>
public class IdpSmart31CardPrinterDriver : ICardPrinterDriver
{
    public const string DefaultPrinterName = "SMART-31";

    public string DriverId => "IDP.SMART31";
    public string DisplayName => "IDP SMART-31 Direct-to-Card Printer";
    public string? TargetPrinterName { get; private set; }

    public IRibbonModeController RibbonController { get; }
    public ICardPrinterStatusMonitor StatusMonitor => _statusMonitor;

    private readonly IdpSmart31StatusMonitor _statusMonitor;
    private PrinterCapabilities? _cachedCapabilities;

    public IdpSmart31CardPrinterDriver(
        string? targetPrinterName = DefaultPrinterName,
        INativeIdpStatusProvider? nativeStatusProvider = null,
        IRibbonModeController? ribbonController = null)
    {
        TargetPrinterName = targetPrinterName;
        RibbonController = ribbonController ?? new DefaultRibbonModeController();
        _statusMonitor = new IdpSmart31StatusMonitor(targetPrinterName ?? DefaultPrinterName, nativeStatusProvider);
    }

    /// <summary>
    /// Names of the printers installed in Windows, or none on other operating systems.
    /// </summary>
    public static IReadOnlyList<string> GetInstalledPrinterNames()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return Array.Empty<string>();
        }

        try
        {
            return System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IdpSmart31CardPrinterDriver] Couldn't list installed printers: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Probes the installed IDP SMART-31 printer driver at runtime for actual DPI,
    /// printable area, and pixel dimensions.
    /// Never hardcodes 1013x638!
    /// </summary>
    public Task<PrinterCapabilities> ProbeCapabilitiesAsync(string printerName, CancellationToken ct = default)
    {
        string name = string.IsNullOrWhiteSpace(printerName) ? TargetPrinterName ?? DefaultPrinterName : printerName;
        TargetPrinterName = name;

        string unavailableReason;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var probed = ProbeWindowsSpooler(name);
                if (probed != null)
                {
                    _cachedCapabilities = probed;
                    return Task.FromResult(probed);
                }

                unavailableReason = $"no printer named '{name}' is installed";
            }
            catch (Exception ex)
            {
                unavailableReason = $"the Windows print spooler couldn't be queried: {ex.Message}";
            }
        }
        else
        {
            unavailableReason = "card printing needs Windows";
        }

        var simulated = CreateSimulatedProfile(name, unavailableReason);
        _cachedCapabilities = simulated;
        return Task.FromResult(simulated);
    }

    /// <summary>
    /// Submits a card to the IDP SMART-31 printer.
    /// Validates canvas dimensions against probed capabilities, failing loudly on mismatch.
    /// Enforces the mandatory 20-card operator pause for output hopper clearing.
    /// </summary>
    public async Task<CardPrintResult> SubmitCardAsync(CardPrintJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        // Checked before the printer is even looked at: this is a misconfiguration of the app, not a printer state.
        // Pixel-black extraction is carried by the rendering pipeline (aliased pure black) and the 1:1 blit in
        // PrintDocumentHelper. The DEVMODE route needs the IDP driver's private field, which isn't implemented —
        // printing anyway would quietly produce dye-panel text that looks right on screen and smudges on the card.
        if (RibbonController.ActiveMode == KResinRoutingMode.DriverDevModeFlag)
        {
            throw new PrinterException(
                $"Cannot print card #{job.BatchIndex}: K-resin routing is set to {nameof(KResinRoutingMode.DriverDevModeFlag)}, which needs the " +
                $"IDP driver's private DEVMODE field and is not implemented. Switch back to {nameof(KResinRoutingMode.PixelBlackExtraction)} " +
                "and enable black extraction in the printer's own driver properties instead. No card was sent.");
        }

        // 1. Probe the driver; re-probe while the printer was unavailable so one connected mid-session is picked up
        var capabilities = _cachedCapabilities is { IsSimulated: false } cached
            ? cached
            : await ProbeCapabilitiesAsync(TargetPrinterName ?? DefaultPrinterName, ct);

        // 2. Validate format against probed capabilities - FAIL LOUDLY on mismatch
        capabilities.EnsureCompatible(job.Format);

        if (capabilities.IsSimulated)
        {
            capabilities.RawDriverProperties.TryGetValue("UnavailableReason", out string? reason);
            throw new PrinterException(
                $"Cannot print card #{job.BatchIndex}: printer '{TargetPrinterName}' is not available ({reason ?? "no printer answered"}). No card was sent.");
        }

        // 3. Check status monitor for mandatory operator pause (every 20 cards) or errors
        var status = await _statusMonitor.PollStatusAsync(ct);
        if (status.State == PrinterState.OperatorPauseRequired)
        {
            throw new OperatorPauseRequiredException(
                $"Cannot submit card #{job.BatchIndex}: Mandatory operator pause reached ({IdpSmart31StatusMonitor.MandatoryPauseThreshold} cards printed). " +
                $"Clear the output hopper (capacity {IdpSmart31StatusMonitor.MaxOutputHopperCapacity} cards) and reset the pause counter to continue.",
                status.CardsPrintedSincePause,
                IdpSmart31StatusMonitor.MandatoryPauseThreshold
            );
        }

        if (status.RequiresOperatorAttention)
        {
            throw new PrinterHardwareFaultException(
                $"Cannot submit card #{job.BatchIndex}: Printer is in state '{status.State}' ({status.StatusMessage}).",
                status.State.ToString()
            );
        }

        if (status.State != PrinterState.Ready)
        {
            throw new PrinterException(
                $"Cannot submit card #{job.BatchIndex}: printer is '{status.State}' ({status.StatusMessage}). No card was sent.");
        }

        if (job.IsDuplex && !capabilities.SupportsDuplex)
        {
            throw new PrinterException(
                $"Card #{job.BatchIndex} has a back side, but printer '{TargetPrinterName}' can't print double-sided. No card was sent.");
        }

        // 4. Decode and size-check the card images before anything reaches the spooler
        using var front = DecodeCardImage(job.FrontPixelBuffer, job, capabilities, "front");
        using var back = job.IsDuplex ? DecodeCardImage(job.BackPixelBuffer, job, capabilities, "back") : null;

        // 5. Spool exactly one card as its own print job, named with the correlated job name
        string printerName = TargetPrinterName ?? DefaultPrinterName;
        string documentName = job.JobId;
        await Task.Run(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                PrintDocumentHelper.PrintCard(printerName, job.Format, documentName, front, back);
            }
        }, ct);

        // Track the job so completion is gated on it leaving the queue, and count it toward the hopper pause
        _statusMonitor.TrackSubmittedJob(documentName);
        _statusMonitor.RecordCardEjected();

        bool pauseTriggered = _statusMonitor.CardsPrintedSincePause >= IdpSmart31StatusMonitor.MandatoryPauseThreshold;
        return CardPrintResult.Succeeded(job.JobId, documentName, pauseTriggered);
    }

    /// <summary>
    /// Stops the card the printer is working on: the job is deleted from the Windows queue so no more of the image
    /// is sent, and the printer ejects the card with whatever has been printed on it so far.
    /// </summary>
    public Task<bool> AbortCurrentCardAsync(CancellationToken ct = default)
    {
        var jobs = _statusMonitor.GetTrackedJobNames();
        if (jobs.Count == 0)
        {
            // Nothing of ours is still in the queue: the card is already out of the printer's hands
            return Task.FromResult(false);
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        try
        {
            int cancelled = WindowsSpooler.TryCancelJobs(TargetPrinterName ?? DefaultPrinterName, jobs);
            if (cancelled > 0)
            {
                _statusMonitor.ForgetTrackedJobs();
            }

            return Task.FromResult(cancelled > 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IdpSmart31CardPrinterDriver] Couldn't cancel the card in the printer: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public void Dispose()
    {
        _statusMonitor.Dispose();
    }

    private static SKBitmap DecodeCardImage(byte[]? buffer, CardPrintJob job, PrinterCapabilities capabilities, string side)
    {
        if (buffer is not { Length: > 0 })
        {
            throw new PrinterException($"Card #{job.BatchIndex} has no {side} image. No card was sent.");
        }

        var bitmap = SKBitmap.Decode(buffer)
            ?? throw new PrinterException($"The {side} image of card #{job.BatchIndex} isn't a readable image. No card was sent.");

        var (expectedWidth, expectedHeight) = capabilities.GetCanvasSizeFor(job.Format);
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Same tolerance the capability check uses: a dot or two of rounding is centred on the card at print time,
        // anything larger would mean the image was rendered for a different canvas than the one being printed on.
        int tolerance = capabilities.DimensionTolerancePixels;
        if (Math.Abs(width - expectedWidth) > tolerance || Math.Abs(height - expectedHeight) > tolerance)
        {
            bitmap.Dispose();
            throw new PrinterException(
                $"The {side} image of card #{job.BatchIndex} is {width}x{height}px but the printer canvas is {expectedWidth}x{expectedHeight}px. " +
                "Silent stretching prevented; no card was sent.");
        }

        return bitmap;
    }

    private static PrinterCapabilities CreateSimulatedProfile(string printerName, string unavailableReason)
    {
        // CR80 baseline (85.60mm x 53.98mm @ 300 DPI = 1011x638px) so card previews work without a printer
        int defaultDpi = 300;
        int defaultWidthPx = (int)Math.Round((85.60 / 25.4) * defaultDpi);  // 1011
        int defaultHeightPx = (int)Math.Round((53.98 / 25.4) * defaultDpi); // 638

        return new PrinterCapabilities
        {
            PrinterName = printerName,
            DriverName = "IDP SMART-31 (Simulated/Offline)",
            ResolutionDpiX = defaultDpi,
            ResolutionDpiY = defaultDpi,
            PrintableWidthPixels = defaultWidthPx,
            PrintableHeightPixels = defaultHeightPx,
            SupportsColor = true,
            SupportsDuplex = false,
            IsSimulated = true,
            RawDriverProperties = new Dictionary<string, string>
            {
                ["DriverSource"] = "FallbackProfile",
                ["HardwareVerificationRequired"] = "True",
                ["UnavailableReason"] = unavailableReason,
                ["Note"] = "No printer answered the probe; a 300 DPI CR80 profile is used for previews only."
            }
        };
    }

    [SupportedOSPlatform("windows")]
    private static PrinterCapabilities? ProbeWindowsSpooler(string printerName)
    {
        var settings = new System.Drawing.Printing.PrinterSettings
        {
            PrinterName = printerName
        };

        if (!settings.IsValid)
            return null;

        var pageSettings = settings.DefaultPageSettings;
        bool driverReportsDpi = pageSettings.PrinterResolution is { X: > 0, Y: > 0 };
        int dpiX = driverReportsDpi ? pageSettings.PrinterResolution.X : 300;
        int dpiY = driverReportsDpi ? pageSettings.PrinterResolution.Y : 300;

        // Direct-to-card printers print edge to edge, so the canvas is the whole card (paper size), not the printable
        // area that some drivers shrink by hardware margins. Both are in hundredths of an inch.
        var paper = pageSettings.PaperSize;
        var printableArea = pageSettings.PrintableArea;
        double widthHundredths = paper.Width > 0 ? paper.Width : printableArea.Width;
        double heightHundredths = paper.Height > 0 ? paper.Height : printableArea.Height;
        int widthPixels = (int)Math.Round((widthHundredths / 100.0) * dpiX);
        int heightPixels = (int)Math.Round((heightHundredths / 100.0) * dpiY);

        var rawProps = new Dictionary<string, string>
        {
            ["DriverSource"] = "Windows.Spooler.System.Drawing.Printing",
            ["IsValid"] = settings.IsValid.ToString(),
            ["IsDirectToCard"] = "True",
            ["SupportsColor"] = settings.SupportsColor.ToString(),
            ["CanDuplex"] = settings.CanDuplex.ToString(),
            ["MaxCopies"] = settings.MaximumCopies.ToString(),
            ["PaperSize"] = $"{paper.PaperName} ({paper.Width}x{paper.Height} hundredths of an inch)",
            ["PrintableArea"] = $"{printableArea.Width:0}x{printableArea.Height:0} hundredths of an inch",
            ["DpiSource"] = driverReportsDpi ? "Driver" : "Assumed300"
        };

        return new PrinterCapabilities
        {
            PrinterName = printerName,
            DriverName = settings.PrinterName,
            ResolutionDpiX = dpiX,
            ResolutionDpiY = dpiY,
            PrintableWidthPixels = widthPixels,
            PrintableHeightPixels = heightPixels,
            SupportsColor = settings.SupportsColor,
            SupportsDuplex = settings.CanDuplex,
            RawDriverProperties = rawProps
        };
    }
}
