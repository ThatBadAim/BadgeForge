using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// An image file prepared for embedding directly inside a template.
/// </summary>
public record ImportedImage(string DataUri, int Width, int Height, string FileName, long EmbeddedBytes);

/// <summary>
/// Resolves image sources (file paths, data URIs or raw Base64) into upright bitmaps,
/// with a size-bounded cache so live preview re-renders don't decode the same image repeatedly.
/// </summary>
public static class ImageSourceLoader
{
    /// <summary>
    /// Decoded images are downscaled to this longest side (ample for a 300 DPI card at several times zoom).
    /// </summary>
    public const int MaxDecodedDimension = 3000;

    /// <summary>
    /// Imported images larger than this longest side are downscaled before embedding to keep templates small.
    /// </summary>
    public const int MaxEmbeddedDimension = 2400;

    public const string SupportedFormatsDescription = "PNG, JPEG, WebP, BMP or GIF";

    public static IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

    private const long CacheBudgetBytes = 256L * 1024 * 1024;
    private const string FileKeyPrefix = "file:";

    private static readonly object CacheGate = new();
    private static readonly LinkedList<KeyValuePair<string, SKBitmap>> LruList = new();
    private static readonly Dictionary<string, LinkedListNode<KeyValuePair<string, SKBitmap>>> LruIndex = new();
    private static readonly List<SKBitmap> PendingDisposal = new();
    private static long _cachedBytes;
    private static int _activeLeases;

    public static bool IsDataUri(string? source) =>
        source != null && source.StartsWith("data:image", StringComparison.OrdinalIgnoreCase);

    public static bool HasSupportedExtension(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks that a file is a decodable image by reading only its header, cheap enough for hundreds of files.
    /// </summary>
    public static bool IsReadableImageFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var codec = SKCodec.Create(filePath);
            return codec != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Keeps bitmaps returned by <see cref="TryLoad"/> valid until the lease is disposed. Hold one while drawing a
    /// loaded bitmap or reading its pixels or size. Bitmaps evicted from the cache meanwhile have their native memory
    /// released once no lease remains, instead of waiting for the garbage collector.
    /// </summary>
    public static IDisposable AcquireLease()
    {
        lock (CacheGate)
        {
            _activeLeases++;
        }

        return new Lease();
    }

    /// <summary>
    /// Loads an EXIF-oriented bitmap for a file path, data URI or raw Base64 payload.
    /// The returned bitmap is shared and immutable: callers must NOT dispose it, and should hold an
    /// <see cref="AcquireLease"/> for as long as they use it.
    /// Returns null when the source is empty, missing or not a decodable image.
    /// </summary>
    public static SKBitmap? TryLoad(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            if (!IsDataUri(source) && File.Exists(source))
            {
                var file = new FileInfo(source);
                string key = $"{FileKeyPrefix}{file.FullName}|{file.LastWriteTimeUtc.Ticks}|{file.Length}";
                return GetOrDecode(key, () => File.ReadAllBytes(file.FullName));
            }

            if (IsDataUri(source) || source.Length > 100)
            {
                return GetOrDecode(source, () => DecodeBase64(source));
            }
        }
        catch
        {
            // Malformed payloads and unreadable files render as "no image"
        }

        return null;
    }

    /// <summary>
    /// Reads the upright pixel size of an image source, if it can be loaded.
    /// </summary>
    public static bool TryGetSize(string? source, out int width, out int height)
    {
        using var lease = AcquireLease();
        var bitmap = TryLoad(source);
        width = bitmap?.Width ?? 0;
        height = bitmap?.Height ?? 0;
        return bitmap != null;
    }

    /// <summary>
    /// Reads an image file and converts it to a data URI for embedding in a template.
    /// Small, upright PNG/JPEG/WebP files are embedded byte-for-byte; anything else is
    /// rotated upright, downscaled if oversized, and re-encoded.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not a supported image.</exception>
    public static ImportedImage ImportFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        byte[] bytes = File.ReadAllBytes(filePath);

        using var skData = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(skData)
            ?? throw new InvalidDataException($"'{fileName}' is not a supported image. Use {SupportedFormatsDescription}.");

        var format = codec.EncodedFormat;
        var info = codec.Info;

        bool keepOriginal = codec.EncodedOrigin == SKEncodedOrigin.TopLeft
                            && Math.Max(info.Width, info.Height) <= MaxEmbeddedDimension
                            && format is SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp;

        if (keepOriginal)
        {
            string mime = format switch
            {
                SKEncodedImageFormat.Jpeg => "image/jpeg",
                SKEncodedImageFormat.Webp => "image/webp",
                _ => "image/png"
            };
            return new ImportedImage($"data:{mime};base64,{Convert.ToBase64String(bytes)}", info.Width, info.Height, fileName, bytes.Length);
        }

        using var stream = new MemoryStream(bytes);
        using var oriented = PhotoRenderer.LoadExifOrientedBitmap(stream);
        using var scaled = DownscaleCopy(oriented, MaxEmbeddedDimension);
        var output = scaled ?? oriented;

        bool opaque = output.AlphaType == SKAlphaType.Opaque || format == SKEncodedImageFormat.Jpeg;
        using var image = SKImage.FromBitmap(output);
        using var encoded = image.Encode(opaque ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, opaque ? 92 : 100);
        byte[] encodedBytes = encoded.ToArray();

        string encodedMime = opaque ? "image/jpeg" : "image/png";
        return new ImportedImage($"data:{encodedMime};base64,{Convert.ToBase64String(encodedBytes)}",
            output.Width, output.Height, fileName, encodedBytes.Length);
    }

    /// <summary>
    /// Drops all cached bitmaps (mainly for tests).
    /// </summary>
    public static void ClearCache()
    {
        lock (CacheGate)
        {
            foreach (var entry in LruList)
            {
                RetireLocked(entry.Value);
            }

            LruList.Clear();
            LruIndex.Clear();
            _cachedBytes = 0;
        }
    }

    private static SKBitmap? GetOrDecode(string key, Func<byte[]> readBytes)
    {
        lock (CacheGate)
        {
            if (LruIndex.TryGetValue(key, out var node))
            {
                LruList.Remove(node);
                LruList.AddFirst(node);
                return node.Value.Value;
            }
        }

        // Decode outside the lock; a concurrent duplicate decode is harmless
        byte[] bytes = readBytes();
        using var stream = new MemoryStream(bytes);
        SKBitmap bitmap = PhotoRenderer.LoadExifOrientedBitmap(stream);

        var scaled = DownscaleCopy(bitmap, MaxDecodedDimension);
        if (scaled != null)
        {
            bitmap.Dispose();
            bitmap = scaled;
        }

        bitmap.SetImmutable();

        lock (CacheGate)
        {
            if (LruIndex.TryGetValue(key, out var existing))
            {
                // Another thread won the race; our copy was never handed out, so free it now
                bitmap.Dispose();
                return existing.Value.Value;
            }

            var added = LruList.AddFirst(new KeyValuePair<string, SKBitmap>(key, bitmap));
            LruIndex[key] = added;
            _cachedBytes += bitmap.ByteCount;

            while (_cachedBytes > CacheBudgetBytes && LruList.Count > 1)
            {
                var last = LruList.Last!;
                LruList.RemoveLast();
                LruIndex.Remove(last.Value.Key);
                _cachedBytes -= last.Value.Value.ByteCount;
                RetireLocked(last.Value.Value);
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Frees an evicted bitmap now, or once the last lease ends if a render may still be drawing it.
    /// </summary>
    private static void RetireLocked(SKBitmap bitmap)
    {
        if (_activeLeases == 0)
        {
            bitmap.Dispose();
        }
        else
        {
            PendingDisposal.Add(bitmap);
        }
    }

    private sealed class Lease : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (CacheGate)
            {
                _activeLeases--;
                if (_activeLeases == 0)
                {
                    foreach (var bitmap in PendingDisposal)
                    {
                        bitmap.Dispose();
                    }

                    PendingDisposal.Clear();
                }
            }
        }
    }

    private static byte[] DecodeBase64(string source)
    {
        int commaIndex = source.IndexOf(',');
        string clean = commaIndex >= 0 && source.Contains(";base64", StringComparison.OrdinalIgnoreCase)
            ? source[(commaIndex + 1)..]
            : source;
        return Convert.FromBase64String(clean);
    }

    /// <summary>
    /// Returns a downscaled copy when the longest side exceeds <paramref name="maxDimension"/>, otherwise null.
    /// </summary>
    private static SKBitmap? DownscaleCopy(SKBitmap source, int maxDimension)
    {
        int longest = Math.Max(source.Width, source.Height);
        if (longest <= maxDimension)
        {
            return null;
        }

        double scale = (double)maxDimension / longest;
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            source.ColorType,
            source.AlphaType);

        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }
}
