namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Result of attempting to correlate a submitted print job in the spooler / driver queue.
/// </summary>
public record SpoolerCorrelationResult
{
    public bool IsCorrelated { get; init; }
    public string? SpoolerJobId { get; init; }
    public string ExpectedJobName { get; init; } = string.Empty;
    public string? MatchedJobName { get; init; }
    public int RetriesAttempted { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string? ErrorMessage { get; init; }

    public static SpoolerCorrelationResult Success(string spoolerJobId, string expectedName, int retries, TimeSpan elapsed) =>
        new()
        {
            IsCorrelated = true,
            SpoolerJobId = spoolerJobId,
            ExpectedJobName = expectedName,
            MatchedJobName = expectedName,
            RetriesAttempted = retries,
            Elapsed = elapsed
        };

    public static SpoolerCorrelationResult Failure(string expectedName, int retries, TimeSpan elapsed, string message) =>
        new()
        {
            IsCorrelated = false,
            ExpectedJobName = expectedName,
            RetriesAttempted = retries,
            Elapsed = elapsed,
            ErrorMessage = message
        };
}

/// <summary>
/// Interface for generating identifiable print job names and correlating them
/// immediately in the spooler or driver queue after submission.
/// </summary>
public interface ISpoolerJobCorrelator
{
    /// <summary>
    /// Generates a uniquely identifiable print job name containing the record ID and a GUID.
    /// </summary>
    string GenerateJobName(string recordId, string? batchRunId = null);

    /// <summary>
    /// Correlates the submitted job in the print queue / driver record, with timeout and retries.
    /// </summary>
    Task<SpoolerCorrelationResult> CorrelateJobAsync(
        string targetPrinterName,
        string expectedJobName,
        Func<Task<string?>>? customLookup = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default);
}
