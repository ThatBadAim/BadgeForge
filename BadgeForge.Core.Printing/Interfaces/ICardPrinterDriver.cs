using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Interfaces;

/// <summary>
/// Hardware abstraction interface for card printers.
/// Isolates high-level badge batch orchestration from manufacturer-specific drivers.
/// IDP SMART-31 is one implementation; future printer models are additional implementations, not rewrites.
/// </summary>
public interface ICardPrinterDriver : IDisposable
{
    /// <summary>
    /// Unique programmatic identifier for this driver implementation.
    /// </summary>
    string DriverId { get; }

    /// <summary>
    /// Human-friendly display name (e.g. "IDP SMART-31 Card Printer", "Mock Card Printer").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Active target printer queue or device name.
    /// </summary>
    string? TargetPrinterName { get; }

    /// <summary>
    /// Probes the printer driver at runtime to query actual DPI, printable area, and canvas pixel size.
    /// Never hardcodes canvas dimensions.
    /// </summary>
    /// <param name="printerName">Windows printer queue name or hardware identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Probed capabilities from the active driver.</returns>
    Task<PrinterCapabilities> ProbeCapabilitiesAsync(string printerName, CancellationToken ct = default);

    /// <summary>
    /// Controller for configuring K-resin monochrome ribbon routing and density.
    /// </summary>
    IRibbonModeController RibbonController { get; }

    /// <summary>
    /// Status monitor for querying physical printer state and spooler queue progress.
    /// </summary>
    ICardPrinterStatusMonitor StatusMonitor { get; }

    /// <summary>
    /// Submits a single card print job to the printer hardware.
    /// Validates canvas pixel dimensions against probed capabilities before spooling,
    /// failing loudly on mismatch to prevent silent image stretching.
    /// </summary>
    /// <param name="job">Card print payload including pixel buffer and metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Execution result with job identifier or error details.</returns>
    Task<CardPrintResult> SubmitCardAsync(CardPrintJob job, CancellationToken ct = default);

    /// <summary>
    /// Gives up on the card the printer is working on right now: stops sending it any more data and lets the
    /// mechanism push out whatever is already on the card. Used when the operator stops a run mid-card and
    /// chooses to abort rather than let that card finish.
    /// </summary>
    /// <returns>
    /// True when the printer was told to stop. False when this driver can't interrupt a card, in which case the
    /// card finishes on its own and the caller should say so.
    /// </returns>
    Task<bool> AbortCurrentCardAsync(CancellationToken ct = default) => Task.FromResult(false);
}
