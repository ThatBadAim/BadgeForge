namespace BadgeForge.Core.Printing.Models;

/// <summary>
/// Encapsulates a single card print payload submitted to a card printer driver.
/// </summary>
public record CardPrintJob
{
    /// <summary>
    /// Unique identifier for this card job.
    /// </summary>
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Intended card physical format profile.
    /// </summary>
    public CardFormat Format { get; init; } = CardFormat.CR80;

    /// <summary>
    /// Label or recipient name for operator identification.
    /// </summary>
    public string Label { get; init; } = "Badge";

    /// <summary>
    /// 1-based sequence index within the batch run.
    /// </summary>
    public int BatchIndex { get; init; } = 1;

    /// <summary>
    /// Raw uncompressed 32-bit RGBA pixel buffer (or encoded PNG) of the front face.
    /// Sized exactly according to Format.WidthPixels x Format.HeightPixels.
    /// </summary>
    public byte[] FrontPixelBuffer { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Optional rear face pixel buffer if printing dual-sided.
    /// </summary>
    public byte[]? BackPixelBuffer { get; init; }

    /// <summary>
    /// True if two-sided duplex print is requested.
    /// </summary>
    public bool IsDuplex => BackPixelBuffer is { Length: > 0 };

    /// <summary>
    /// Contextual metadata (e.g. record ID, batch run ID).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Result returned after submitting a card to the printer driver.
/// </summary>
public record CardPrintResult
{
    public string JobId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? SpoolerJobId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset SubmittedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public bool OperatorPauseTriggered { get; init; }

    public static CardPrintResult Succeeded(string jobId, string? spoolerJobId, bool pauseTriggered = false) =>
        new()
        {
            JobId = jobId,
            Success = true,
            SpoolerJobId = spoolerJobId,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            OperatorPauseTriggered = pauseTriggered
        };

    public static CardPrintResult Failed(string jobId, string errorMessage) =>
        new()
        {
            JobId = jobId,
            Success = false,
            ErrorMessage = errorMessage,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
}
