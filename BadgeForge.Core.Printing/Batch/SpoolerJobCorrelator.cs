using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using BadgeForge.Core.Printing.Drivers;

namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Handles unique job name generation (GUID + record ID) and spooler job correlation
/// with retry loops, timeout handling, and diagnostic logging.
/// </summary>
public class SpoolerJobCorrelator : ISpoolerJobCorrelator
{
    private static readonly Regex InvalidCharsRegex = new(@"[^a-zA-Z0-9_\-]", RegexOptions.Compiled);
    private readonly Action<string>? _logAction;

    public SpoolerJobCorrelator(Action<string>? logAction = null)
    {
        _logAction = logAction;
    }

    /// <inheritdoc/>
    public string GenerateJobName(string recordId, string? batchRunId = null)
    {
        string cleanRecordId = InvalidCharsRegex.Replace(recordId ?? "REC", "_");
        if (cleanRecordId.Length > 24)
        {
            cleanRecordId = cleanRecordId[..24];
        }

        string guid = Guid.NewGuid().ToString("N")[..12];
        return $"BF_{cleanRecordId}_{guid}";
    }

    /// <inheritdoc/>
    public async Task<SpoolerCorrelationResult> CorrelateJobAsync(
        string targetPrinterName,
        string expectedJobName,
        Func<Task<string?>>? customLookup = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(3);
        var stopwatch = Stopwatch.StartNew();
        int retries = 0;
        var retryInterval = TimeSpan.FromMilliseconds(100);

        Log($"Starting spooler correlation for job '{expectedJobName}' on printer '{targetPrinterName}' (timeout: {effectiveTimeout.TotalSeconds}s)");

        while (stopwatch.Elapsed < effectiveTimeout)
        {
            ct.ThrowIfCancellationRequested();
            retries++;

            try
            {
                // 1. Prefer the real queue: its numeric job id is what an operator sees in Windows and what a
                //    support call about a jammed card is traced by. A driver lookup can only echo the job name back.
                if (OperatingSystem.IsWindows())
                {
                    string? matchedId = QueryWindowsSpooler(targetPrinterName, expectedJobName);
                    if (!string.IsNullOrEmpty(matchedId))
                    {
                        Log($"Job '{expectedJobName}' correlated via Windows Print Spooler (JobId: '{matchedId}', took {stopwatch.ElapsedMilliseconds}ms)");
                        return SpoolerCorrelationResult.Success(matchedId, expectedJobName, retries, stopwatch.Elapsed);
                    }
                }

                // 2. Fall back to the driver's / mock's own lookup
                if (customLookup != null)
                {
                    string? matchedId = await customLookup();
                    if (!string.IsNullOrEmpty(matchedId))
                    {
                        Log($"Job '{expectedJobName}' successfully correlated via driver lookup (SpoolerJobId: '{matchedId}', took {stopwatch.ElapsedMilliseconds}ms, {retries} retries)");
                        return SpoolerCorrelationResult.Success(matchedId, expectedJobName, retries, stopwatch.Elapsed);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Transient error during spooler correlation retry #{retries}: {ex.Message}");
            }

            await Task.Delay(retryInterval, ct);
        }

        // Timeout expired without matching
        string failureMsg = $"Failed to correlate job '{expectedJobName}' on printer '{targetPrinterName}' within {effectiveTimeout.TotalSeconds}s ({retries} attempts).";
        Log($"CRITICAL: {failureMsg}");

        return SpoolerCorrelationResult.Failure(expectedJobName, retries, stopwatch.Elapsed, failureMsg);
    }

    /// <summary>
    /// The Windows spooler job id of the queued job with this document name, or null when the queue can't be read
    /// or the job isn't in it (yet, or any more).
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? QueryWindowsSpooler(string printerName, string expectedJobName)
    {
        try
        {
            var snapshot = WindowsSpooler.TryQuery(printerName);
            var match = snapshot?.Jobs.FirstOrDefault(job =>
                string.Equals(job.DocumentName, expectedJobName, StringComparison.Ordinal));

            return match?.JobId.ToString(CultureInfo.InvariantCulture);
        }
        catch
        {
            // The queue may be unreadable (access denied, printer removed mid-run); the caller retries and then
            // falls back to the driver's own lookup
            return null;
        }
    }

    private void Log(string message)
    {
        _logAction?.Invoke($"[SpoolerJobCorrelator] {DateTime.UtcNow:HH:mm:ss.fff} {message}");
    }
}
