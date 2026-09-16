using System.Text;

namespace BadgeForge.Core.Templates.Storage;

/// <summary>
/// Saves files by writing a temporary file beside the target and then swapping it into place, so a crash, full disk
/// or power cut mid-save leaves the previous file intact instead of a truncated one.
/// </summary>
public static class AtomicFileWriter
{
    public static void WriteAllText(string filePath, string contents, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(encoding);

        string fullPath = PrepareTarget(filePath);
        string tempPath = CreateTempPath(fullPath);

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, encoding))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public static async Task WriteAllTextAsync(string filePath, string contents, Encoding encoding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(encoding);

        string fullPath = PrepareTarget(filePath);
        string tempPath = CreateTempPath(fullPath);

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream, encoding))
            {
                await writer.WriteAsync(contents.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static string PrepareTarget(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }

    private static string CreateTempPath(string fullPath) =>
        Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Leave the temp file rather than hide the original error
        }
    }
}
