using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Exceptions;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class CardRendererTests
{
    private readonly CardRenderer _renderer = new();

    [Fact]
    public async Task RenderCardAsync_QueriesCanvasSize_FromDriverProbedCapabilities()
    {
        // Arrange: mock driver with specific probed capabilities (CR80 standard: 1011x638 at 300 DPI)
        using var mockDriver = new MockCardPrinterDriver();
        var template = new TemplateDefinition
        {
            Name = "Basic Test Badge",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new TextLayer
                {
                    Name = "Title",
                    Text = "Staff Badge",
                    FontSize = 14,
                    X = 10,
                    Y = 10,
                    Width = 60,
                    Height = 10
                }
            }
        };

        var fieldData = new Dictionary<string, string>();

        // Act
        using var bitmap = await _renderer.RenderCardAsync(template, fieldData, mockDriver);

        // Assert: real, driver-reported canvas size — never a hardcoded constant
        Assert.NotNull(bitmap);
        Assert.Equal(mockDriver.ProbedCapabilities.PrintableWidthPixels, bitmap.Width);
        Assert.Equal(mockDriver.ProbedCapabilities.PrintableHeightPixels, bitmap.Height);
        Assert.Equal(1011, bitmap.Width);
        Assert.Equal(638, bitmap.Height);
    }

    [Fact]
    public async Task RenderCardAsync_ThrowsLoudly_WhenDriverCanvasMismatchesTemplateFormat()
    {
        // Arrange: mock driver reports a 600 DPI canvas while template expects 300 DPI CR80
        var mismatchedCapabilities = new PrinterCapabilities
        {
            PrinterName = "HighRes-Printer",
            DriverName = "HighRes Driver",
            ResolutionDpiX = 600,
            ResolutionDpiY = 600,
            PrintableWidthPixels = 2022,
            PrintableHeightPixels = 1276
        };

        using var mockDriver = new MockCardPrinterDriver(capabilities: mismatchedCapabilities);
        var template = new TemplateDefinition
        {
            Name = "Standard Badge",
            TargetFormat = CardFormat.CR80 // 300 DPI
        };

        // Act & Assert: Must fail loudly with PrinterCapabilityMismatchException rather than silently stretching
        var ex = await Assert.ThrowsAsync<PrinterCapabilityMismatchException>(() =>
            _renderer.RenderCardAsync(template, new Dictionary<string, string>(), mockDriver));

        Assert.Contains("capability validation failed", ex.Message);
        Assert.Equal(600, ex.DriverDpiX);
        Assert.Equal(300, ex.ExpectedDpi);
    }

    [Fact]
    public void ExifOrientation_RotatesPortraitsCorrectly_ForRightTop_90DegreesCW()
    {
        // Arrange: Create a 200x100 horizontal source bitmap with a distinctive red dot at top-left (10, 10)
        using var source = new SKBitmap(200, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(0, 0, 20, 20), paint);
        }

        // Act: Apply EXIF RightTop (orientation 6 = rotate 90 CW)
        using var oriented = PhotoRenderer.ApplyExifOrientation(source, SKEncodedOrigin.RightTop);

        // Assert: Swapped dimensions (100x200) and top-left dot is now near top-right
        Assert.Equal(100, oriented.Width);
        Assert.Equal(200, oriented.Height);

        // The red corner (0,0..20,20) rotated 90 CW moves to top-right: x in [80..100], y in [0..20]
        var cornerPixel = oriented.GetPixel(90, 10);
        Assert.Equal(SKColors.Red.Red, cornerPixel.Red);
        Assert.Equal(SKColors.Red.Green, cornerPixel.Green);
        Assert.Equal(SKColors.Red.Blue, cornerPixel.Blue);

        // Former top-left is now white
        var oppositePixel = oriented.GetPixel(10, 10);
        Assert.Equal(SKColors.White.Red, oppositePixel.Red);
        Assert.Equal(SKColors.White.Green, oppositePixel.Green);
        Assert.Equal(SKColors.White.Blue, oppositePixel.Blue);
    }

    [Fact]
    public void ExifOrientation_RotatesPortraitsCorrectly_ForLeftBottom_270DegreesCW()
    {
        // Arrange: Create a 200x100 horizontal source bitmap with red at top-left
        using var source = new SKBitmap(200, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(0, 0, 20, 20), paint);
        }

        // Act: Apply EXIF LeftBottom (orientation 8 = rotate 270 CW)
        using var oriented = PhotoRenderer.ApplyExifOrientation(source, SKEncodedOrigin.LeftBottom);

        // Assert: Swapped dimensions (100x200) and top-left dot moves to bottom-left: x in [0..20], y in [180..200]
        Assert.Equal(100, oriented.Width);
        Assert.Equal(200, oriented.Height);

        var cornerPixel = oriented.GetPixel(10, 190);
        Assert.Equal(SKColors.Red.Red, cornerPixel.Red);
        Assert.Equal(SKColors.Red.Green, cornerPixel.Green);
        Assert.Equal(SKColors.Red.Blue, cornerPixel.Blue);
    }

    [Fact]
    public void AspectFill_ComputesCenteredCrop_WithoutDistortion()
    {
        // Scenario 1: Source is wider (400x200, aspect 2.0) than target (100x100, aspect 1.0)
        // Expected: crops horizontal sides centered: cropWidth = 200, cropHeight = 200, cropX = 100
        var cropWider = PhotoRenderer.ComputeAspectFillCrop(400, 200, 100, 100);
        Assert.Equal(100f, cropWider.Left);
        Assert.Equal(0f, cropWider.Top);
        Assert.Equal(300f, cropWider.Right);
        Assert.Equal(200f, cropWider.Bottom);
        Assert.Equal(200f, cropWider.Width);
        Assert.Equal(200f, cropWider.Height);

        // Scenario 2: Source is taller (200x400, aspect 0.5) than target (100x100, aspect 1.0)
        // Expected: crops top and bottom centered: cropWidth = 200, cropHeight = 200, cropY = 100
        var cropTaller = PhotoRenderer.ComputeAspectFillCrop(200, 400, 100, 100);
        Assert.Equal(0f, cropTaller.Left);
        Assert.Equal(100f, cropTaller.Top);
        Assert.Equal(200f, cropTaller.Right);
        Assert.Equal(300f, cropTaller.Bottom);
        Assert.Equal(200f, cropTaller.Width);
        Assert.Equal(200f, cropTaller.Height);
    }

    [Fact]
    public void PureBlackText_RendersStrictlyNonAntiAliased_ForKResinPanel()
    {
        // Arrange: A text layer configured for K-resin pure black rendering
        using var bitmap = new SKBitmap(300, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            var layer = new TextLayer
            {
                Name = "Employee Name",
                Text = "JOHN SMITH",
                FontFamily = "Arial",
                FontSize = 18,
                FontWeight = "Bold",
                Alignment = TextAlignment.Left,
                IsPureBlackKResin = true,
                X = 10,
                Y = 10,
                Width = 250,
                Height = 80
            };

            var fieldData = new Dictionary<string, string>();
            var destRect = new SKRect(10, 10, 260, 90);

            // Act
            TextRenderer.Render(canvas, layer, destRect, fieldData, scaleFactorX: 1.0, scaleFactorY: 1.0);
        }

        // Assert: In strict 1-bit K-resin rendering, every pixel on the canvas must be either
        // pure black (0,0,0) or background white (255,255,255).
        // No anti-aliased gray pixels (e.g. R=128, G=128, B=128) are allowed!
        int pureBlackCount = 0;
        int whiteCount = 0;
        int intermediateGrayCount = 0;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                bool isPureBlack = pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0;
                bool isWhite = pixel.Red == 255 && pixel.Green == 255 && pixel.Blue == 255;

                if (isPureBlack)
                {
                    pureBlackCount++;
                }
                else if (isWhite)
                {
                    whiteCount++;
                }
                else
                {
                    intermediateGrayCount++;
                }
            }
        }

        Assert.True(pureBlackCount > 0, "Expected pure black pixels for the text stroke.");
        Assert.Equal(0, intermediateGrayCount); // Zero anti-aliased intermediate gray pixels!
    }

    [Fact]
    public void BarcodeRenderer_RendersCode128_AsPureBlackIntegerModules()
    {
        // Arrange
        using var bitmap = new SKBitmap(400, 150, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            var layer = new BarcodeLayer
            {
                Name = "Test Barcode",
                ContentToken = "{BadgeId}",
                Symbology = BarcodeSymbology.Code128,
                IncludeText = false,
                IsPureBlackKResin = true,
                X = 20,
                Y = 20,
                Width = 360,
                Height = 100
            };

            var fieldData = new Dictionary<string, string> { { "BadgeId", "EMP-2026-X" } };
            var destRect = new SKRect(20, 20, 380, 120);

            // Act
            BarcodeRenderer.Render(canvas, layer, destRect, fieldData, scaleFactorX: 1.0, scaleFactorY: 1.0);
        }

        // Assert: Barcode modules must be integer rectangles of pure black, with 0 intermediate gray pixels
        int blackPixels = 0;
        int intermediatePixels = 0;

        for (int y = 20; y < 120; y++)
        {
            for (int x = 20; x < 380; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                bool isBlack = pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0;
                bool isWhite = pixel.Red == 255 && pixel.Green == 255 && pixel.Blue == 255;

                if (isBlack)
                {
                    blackPixels++;
                }
                else if (!isWhite)
                {
                    intermediatePixels++;
                }
            }
        }

        Assert.True(blackPixels > 0, "Barcode bars must be rendered.");
        Assert.Equal(0, intermediatePixels); // No anti-aliased edges on barcode modules
    }

    [Fact]
    public void BarcodeRenderer_RendersQrCode_AsPureBlackIntegerModules()
    {
        // Arrange
        using var bitmap = new SKBitmap(200, 200, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            var layer = new BarcodeLayer
            {
                Name = "Test QR",
                ContentToken = "https://badgeforge.io/verify?id={Id}",
                Symbology = BarcodeSymbology.QrCode,
                IsPureBlackKResin = true
            };

            var fieldData = new Dictionary<string, string> { { "Id", "BF-998811" } };
            var destRect = new SKRect(20, 20, 180, 180);

            // Act
            BarcodeRenderer.Render(canvas, layer, destRect, fieldData, scaleFactorX: 1.0, scaleFactorY: 1.0);
        }

        // Assert: QR code modules must be pure black with zero anti-aliasing
        int blackPixels = 0;
        int intermediatePixels = 0;

        for (int y = 20; y < 180; y++)
        {
            for (int x = 20; x < 180; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                bool isBlack = pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0;
                bool isWhite = pixel.Red == 255 && pixel.Green == 255 && pixel.Blue == 255;

                if (isBlack)
                {
                    blackPixels++;
                }
                else if (!isWhite)
                {
                    intermediatePixels++;
                }
            }
        }

        Assert.True(blackPixels > 0, "QR code modules must be rendered.");
        Assert.Equal(0, intermediatePixels);
    }
}
