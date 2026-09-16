namespace BadgeForge.Core.Printing.Models;

/// <summary>
/// Operational state of the card printer.
/// </summary>
public enum PrinterState
{
    Ready,
    Printing,
    Paused,
    PaperJam,
    OutOfRibbon,
    OutOfCards,
    CoverOpen,
    Offline,
    OperatorPauseRequired,
    Error
}

/// <summary>
/// Snapshot of physical printer and spooler status.
/// </summary>
public record PrinterStatus
{
    /// <summary>
    /// Current high-level operational state.
    /// </summary>
    public PrinterState State { get; init; } = PrinterState.Ready;

    /// <summary>
    /// Human-readable status description (e.g., "Ready to print", "Ribbon cartridge exhausted", "Mandatory hopper pause").
    /// </summary>
    public string StatusMessage { get; init; } = "Ready";

    /// <summary>
    /// Estimated or reported cards remaining in the input hopper (SMART-31 capacity: 80 cards).
    /// </summary>
    public int? InputHopperCount { get; init; } = 80;

    /// <summary>
    /// Estimated cards present in the output hopper (SMART-31 capacity: 25 cards).
    /// </summary>
    public int OutputHopperCount { get; init; } = 0;

    /// <summary>
    /// Number of cards printed in current continuous batch sequence.
    /// A mandatory pause is enforced every 20 cards.
    /// </summary>
    public int CardsPrintedSincePause { get; init; } = 0;

    /// <summary>
    /// Ribbon capacity percentage remaining (0-100), if readable via RFID tag or estimated.
    /// </summary>
    public int? RibbonRemainingPercent { get; init; } = 100;

    /// <summary>
    /// Indicates whether the status was obtained directly from the physical printer's native API/SDK
    /// (e.g. SmartComm/bi-directional query) or fallback spooler queue polling.
    /// </summary>
    public bool IsNativeStatus { get; init; } = false;

    /// <summary>
    /// Spooler or native job identifier currently active, or null if idle.
    /// </summary>
    public string? ActiveJobId { get; init; }

    /// <summary>
    /// UTC timestamp of this status reading.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns true if the printer is in an idle, healthy state and ready to accept a card.
    /// </summary>
    public bool IsReadyToPrint => State == PrinterState.Ready;

    /// <summary>
    /// Returns true if the printer cannot accept jobs without operator action (jam, out of cards/ribbon, hopper pause,
    /// disconnected).
    /// </summary>
    public bool RequiresOperatorAttention => State switch
    {
        PrinterState.PaperJam or
        PrinterState.OutOfRibbon or
        PrinterState.OutOfCards or
        PrinterState.CoverOpen or
        PrinterState.Offline or
        PrinterState.OperatorPauseRequired or
        PrinterState.Error => true,
        _ => false
    };
}
