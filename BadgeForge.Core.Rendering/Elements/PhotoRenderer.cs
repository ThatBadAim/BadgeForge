using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Tokens;
using SkiaSharp;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// Renders employee and attendee photos with EXIF orientation awareness,
/// centered aspect-fill (cover mode) cropping, and optional rounded corner clipping.
/// </summary>
public static class PhotoRenderer
{
    /// <summary>
    /// Decodes an image stream while applying EXIF orientation rotation from SKCodec.EncodedOrigin.
    /// Ensures camera/phone portraits are never rendered sideways.
    /// </summary>
    /// <param name="stream">Image data stream.</param>
    /// <returns>Upright oriented SKBitmap.</returns>
    public static SKBitmap LoadExifOrientedBitmap(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        // Decode from an owned SKData buffer: rewinding a managed stream underneath an SKCodec corrupts decoding
        using var data = SKData.CreateCopy(memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length));
        using var codec = SKCodec.Create(data);
        if (codec == null)
        {
            throw new InvalidOperationException("Failed to create SKCodec from image stream.");
        }

        var origin = codec.EncodedOrigin;
        using var rawBitmap = SKBitmap.Decode(codec);
        if (rawBitmap == null)
        {
            throw new InvalidOperationException("Failed to decode image data into SKBitmap.");
        }

        return ApplyExifOrientation(rawBitmap, origin);
    }

    /// <summary>
    /// Loads an image file from disk, applying EXIF orientation rotation.
    /// </summary>
    public static SKBitmap LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Photo file not found: {filePath}", filePath);
        }

        using var fileStream = File.OpenRead(filePath);
        return LoadExifOrientedBitmap(fileStream);
    }

    /// <summary>
    /// Loads an image from Base64 encoded payload, applying EXIF orientation rotation.
    /// </summary>
    public static SKBitmap LoadFromBase64(string base64Data)
    {
        if (string.IsNullOrWhiteSpace(base64Data))
        {
            throw new ArgumentException("Base64 data cannot be null or empty.", nameof(base64Data));
        }

        // Clean any data URI prefix (e.g. data:image/png;base64,...)
        int commaIndex = base64Data.IndexOf(',');
        string cleanBase64 = (commaIndex >= 0 && base64Data.Contains(";base64"))
            ? base64Data[(commaIndex + 1)..]
            : base64Data;

        byte[] bytes = Convert.FromBase64String(cleanBase64);
        using var stream = new MemoryStream(bytes);
        return LoadExifOrientedBitmap(stream);
    }

    /// <summary>
    /// Applies EXIF orientation rotation/mirroring to an SKBitmap based on SKEncodedOrigin.
    /// </summary>
    public static SKBitmap ApplyExifOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source.Copy();
        }

        bool swapDimensions = origin is SKEncodedOrigin.LeftTop
                                  or SKEncodedOrigin.RightTop
                                  or SKEncodedOrigin.RightBottom
                                  or SKEncodedOrigin.LeftBottom;

        int targetW = swapDimensions ? source.Height : source.Width;
        int targetH = swapDimensions ? source.Width : source.Height;

        var orientedBitmap = new SKBitmap(targetW, targetH, source.ColorType, source.AlphaType);
        using (var canvas = new SKCanvas(orientedBitmap))
        {
            canvas.Clear(SKColors.Transparent);

            switch (origin)
            {
                case SKEncodedOrigin.TopRight: // Flip Horizontal
                    canvas.Translate(targetW, 0);
                    canvas.Scale(-1, 1);
                    break;

                case SKEncodedOrigin.BottomRight: // Rotate 180
                    canvas.Translate(targetW, targetH);
                    canvas.RotateDegrees(180);
                    break;

                case SKEncodedOrigin.BottomLeft: // Flip Vertical
                    canvas.Translate(0, targetH);
                    canvas.Scale(1, -1);
                    break;

                case SKEncodedOrigin.LeftTop: // Transpose (flip horizontal then rotate 270)
                    canvas.Scale(-1, 1);
                    canvas.RotateDegrees(90);
                    break;

                case SKEncodedOrigin.RightTop: // Rotate 90 CW
                    canvas.Translate(targetW, 0);
                    canvas.RotateDegrees(90);
                    break;

                case SKEncodedOrigin.RightBottom: // Transverse
                    canvas.Translate(targetW, targetH);
                    canvas.Scale(-1, 1);
                    canvas.RotateDegrees(270);
                    break;

                case SKEncodedOrigin.LeftBottom: // Rotate 270 CW
                    canvas.Translate(0, targetH);
                    canvas.RotateDegrees(270);
                    break;

                default:
                    break;
            }

            using var paint = new SKPaint
            {
                IsAntialias = true
            };
            var sampling = new SKSamplingOptions(SKFilterMode.Linear);
            canvas.DrawBitmap(source, 0, 0, sampling, paint);
        }

        return orientedBitmap;
    }

    /// <summary>
    /// Renders a PhotoLayer into the specified destination rectangle on the SKCanvas.
    /// Handles token resolution, fallback image, EXIF rotation, crop mode, image adjustments and corner clipping.
    /// When neither the record photo nor the layer's fallback image can be loaded, the frame is left empty, or holds
    /// the <see cref="PhotoPlaceholder"/> silhouette when <paramref name="drawPlaceholderWhenMissing"/> is set.
    /// </summary>
    public static void Render(
        SKCanvas canvas,
        PhotoLayer layer,
        SKRect destRect,
        IReadOnlyDictionary<string, string> fieldData,
        double scaleFactorX,
        double scaleFactorY,
        bool drawPlaceholderWhenMissing = false)
    {
        // 1. Resolve photo path or base64 from fieldData
        string tokenName = TokenSyntax.NormalizeName(layer.SourceToken);
        string? photoSource = null;

        if (fieldData.TryGetValue(tokenName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            photoSource = value;
        }
        else if (fieldData.TryGetValue(layer.SourceToken, out var directValue) && !string.IsNullOrWhiteSpace(directValue))
        {
            photoSource = directValue;
        }

        // 2. Load EXIF-oriented bitmap, falling back to the layer placeholder (cached, shared — do not dispose)
        var photo = ImageSourceLoader.TryLoad(photoSource) ?? ImageSourceLoader.TryLoad(layer.FallbackImagePath);
        float radiusPx = (float)(layer.BorderRadius * ((scaleFactorX + scaleFactorY) / 2.0));
        if (photo == null)
        {
            if (drawPlaceholderWhenMissing)
            {
                PhotoPlaceholder.Draw(canvas, destRect, radiusPx, layer.Opacity);
            }

            return;
        }

        // 3. Crop, adjust and clip
        ImageDrawing.Draw(canvas, photo, destRect, layer.CropMode, layer.Adjustments, radiusPx, layer.Opacity);
    }

    /// <summary>
    /// Computes the centered source cropping rectangle for AspectFill (cover mode).
    /// Prevents aspect ratio distortion by cropping excess symmetrically.
    /// </summary>
    public static SKRect ComputeAspectFillCrop(int srcW, int srcH, float destW, float destH) =>
        ImageLayout.ComputeAspectFillCrop(srcW, srcH, destW, destH);
}
