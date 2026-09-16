using System.Text.Json;

namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// JSON-based implementation of IBatchCheckpointStore.
/// Utilizes atomic file writes (write to temp file then rename/replace)
/// to prevent file corruption during mid-run process crashes or power interruptions.
/// </summary>
public class JsonBatchCheckpointStore : IBatchCheckpointStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveCheckpointAsync(BatchCheckpoint checkpoint, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            checkpoint.LastUpdatedAtUtc = DateTimeOffset.UtcNow;

            await using (var fileStream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(fileStream, checkpoint, JsonOptions, ct);
                await fileStream.FlushAsync(ct);

                // Force the bytes to disk before the rename, so a power cut can't leave an empty checkpoint behind
                fileStream.Flush(flushToDisk: true);
            }

            // Atomic replace / move
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            throw;
        }
    }

    public async Task<BatchCheckpoint?> LoadCheckpointAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<BatchCheckpoint>(fileStream, JsonOptions, ct);
    }
}
