using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering;
using BadgeForge.Core.Rendering.Testing;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class GoldenImageTests
{
    private readonly CardRenderer _renderer = new();

    [Fact]
    public async Task GoldenImage_FixedTemplateAndData_MatchesReferencePixelForPixel()
    {
        // 1. Prepare fixed, deterministic test photo (80x100 portrait)
        string fixedPhotoBase64 = CreateDeterministicTestPhotoBase64();

        // 2. Define fixed badge template with all layer types
        var template = new TemplateDefinition
        {
            Id = "GOLDEN-TEMPLATE-001",
            Name = "Security Access Pass",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                // Top Header Text (Grey Olive #7A918D)
                new TextLayer
                {
                    Name = "Header",
                    Text = "APEX SECURITY • FACILITY ACCESS",
                    FontFamily = "Arial",
                    FontSize = 10,
                    FontWeight = "Bold",
                    ColorHex = "#7A918D",
                    IsPureBlackKResin = false,
                    Alignment = TextAlignment.Center,
                    // Pinned to the original draw-as-is behaviour so this reference image keeps guarding the
                    // rest of the pipeline; shrink-to-fit is covered by TextLayoutEngineTests
                    Overflow = TextOverflowMode.Overflow,
                    X = 5,
                    Y = 4,
                    Width = 75.6,
                    Height = 6,
                    ZIndex = 1
                },

                // Employee Photo with AspectFill and border radius
                new PhotoLayer
                {
                    Name = "Photo",
                    SourceToken = "{Photo}",
                    BorderRadius = 3.0,
                    CropMode = PhotoCropMode.AspectFill,
                    X = 6,
                    Y = 12,
                    Width = 24,
                    Height = 32,
                    ZIndex = 2
                },

                // Employee Full Name (Strict pure-black K-resin)
                new TextLayer
                {
                    Name = "Full Name",
                    Text = "{FullName}",
                    FontFamily = "Arial",
                    FontSize = 14,
                    FontWeight = "Bold",
                    IsPureBlackKResin = true,
                    Alignment = TextAlignment.Left,
                    // Pinned to the original draw-as-is behaviour so this reference image keeps guarding the
                    // rest of the pipeline; shrink-to-fit is covered by TextLayoutEngineTests
                    Overflow = TextOverflowMode.Overflow,
                    X = 33,
                    Y = 14,
                    Width = 48,
                    Height = 8,
                    ZIndex = 3
                },

                // Department / Role
                new TextLayer
                {
                    Name = "Role",
                    Text = "{Department}",
                    FontFamily = "Arial",
                    FontSize = 9,
                    FontWeight = "Normal",
                    IsPureBlackKResin = true,
                    Alignment = TextAlignment.Left,
                    // Pinned to the original draw-as-is behaviour so this reference image keeps guarding the
                    // rest of the pipeline; shrink-to-fit is covered by TextLayoutEngineTests
                    Overflow = TextOverflowMode.Overflow,
                    X = 33,
                    Y = 23,
                    Width = 48,
                    Height = 6,
                    ZIndex = 4
                },

                // QR Code Verification URL
                new BarcodeLayer
                {
                    Name = "QR Code",
                    ContentToken = "{VerifyUrl}",
                    Symbology = BarcodeSymbology.QrCode,
                    IsPureBlackKResin = true,
                    X = 64,
                    Y = 32,
                    Width = 16,
                    Height = 16,
                    ZIndex = 5
                },

                // Code128 Barcode with human-readable text
                new BarcodeLayer
                {
                    Name = "Badge Barcode",
                    ContentToken = "{BadgeId}",
                    Symbology = BarcodeSymbology.Code128,
                    IncludeText = true,
                    IsPureBlackKResin = true,
                    X = 6,
                    Y = 45,
                    Width = 55,
                    Height = 7,
                    ZIndex = 6
                }
            }
        };

        // 3. Fixed test data
        var fieldData = new Dictionary<string, string>
        {
            ["FullName"] = "ALEXANDER CROSS",
            ["Department"] = "INFRASTRUCTURE & DEV",
            ["BadgeId"] = "SEC-4091-AC",
            ["VerifyUrl"] = "https://badgeforge.io/v/4091",
            ["Photo"] = fixedPhotoBase64
        };

        // 4. Render against MockCardPrinterDriver
        using var mockDriver = new MockCardPrinterDriver();
        using var actualBitmap = await _renderer.RenderCardAsync(template, fieldData, mockDriver);

        Assert.NotNull(actualBitmap);
        Assert.Equal(1011, actualBitmap.Width);
        Assert.Equal(638, actualBitmap.Height);

        // 5. Golden image reference directory
        string goldenPath = Path.Combine(AppContext.BaseDirectory, "GoldenData", "cr80_security_badge_golden.png");
        if (!File.Exists(goldenPath))
        {
            string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GoldenData", "cr80_security_badge_golden.png"));
            if (File.Exists(sourcePath))
            {
                goldenPath = sourcePath;
            }
            else
            {
                string goldenDir = Path.GetDirectoryName(goldenPath)!;
                Directory.CreateDirectory(goldenDir);
                ImageDiffService.SavePng(actualBitmap, goldenPath);
            }
        }

        // 6. Compare actual output against golden reference image
        using var goldenBitmap = ImageDiffService.LoadPng(goldenPath);
        var diffResult = ImageDiffService.Compare(goldenBitmap, actualBitmap, channelTolerance: 0);

        try
        {
            // Assert: Pixel-for-pixel exact match (0 differing pixels)
            Assert.True(diffResult.IsMatch,
                $"Golden image regression detected: {diffResult.MismatchPixels} of {diffResult.TotalPixels} pixels differed ({diffResult.MismatchPercentage:F2}%). Max channel delta: {diffResult.MaxChannelDelta}.");
            Assert.Equal(0, diffResult.MismatchPixels);
        }
        finally
        {
            diffResult.DiffBitmap?.Dispose();
        }
    }

    [Fact]
    public void ImageDiffHarness_DetectsPixelMismatches_AndGeneratesVisualDiff()
    {
        // Arrange: Create two identical 100x100 bitmaps
        using var bmp1 = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp2 = new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul);

        bmp1.Erase(SKColors.White);
        bmp2.Erase(SKColors.White);

        // Modify a 10x10 square in bmp2 to black
        for (int y = 40; y < 50; y++)
        {
            for (int x = 40; x < 50; x++)
            {
                bmp2.SetPixel(x, y, SKColors.Black);
            }
        }

        // Act
        var diff = ImageDiffService.Compare(bmp1, bmp2, channelTolerance: 0, generateDiffBitmap: true);

        try
        {
            // Assert: 100 pixels differed out of 10,000 (1.0%)
            Assert.False(diff.IsMatch);
            Assert.Equal(10000, diff.TotalPixels);
            Assert.Equal(100, diff.MismatchPixels);
            Assert.Equal(1.0, diff.MismatchPercentage, precision: 2);
            Assert.Equal(255, diff.MaxChannelDelta);

            // Verify visual diff bitmap exists and highlights the modified region in magenta (#FF007F)
            Assert.NotNull(diff.DiffBitmap);
            var highlightPixel = diff.DiffBitmap.GetPixel(45, 45);
            Assert.Equal(255, highlightPixel.Red);
            Assert.Equal(0, highlightPixel.Green);
            Assert.Equal(128, highlightPixel.Blue);
        }
        finally
        {
            diff.DiffBitmap?.Dispose();
        }
    }

    [Fact]
    public void ImageDiffHarness_ReturnsZeroDiff_ForIdenticalBitmaps()
    {
        using var bmp1 = new SKBitmap(50, 50, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bmp2 = new SKBitmap(50, 50, SKColorType.Bgra8888, SKAlphaType.Premul);

        bmp1.Erase(SKColors.CornflowerBlue);
        bmp2.Erase(SKColors.CornflowerBlue);

        var diff = ImageDiffService.Compare(bmp1, bmp2);

        try
        {
            Assert.True(diff.IsMatch);
            Assert.Equal(0, diff.MismatchPixels);
            Assert.Equal(0.0, diff.MismatchPercentage);
            Assert.Equal(0, diff.MaxChannelDelta);
        }
        finally
        {
            diff.DiffBitmap?.Dispose();
        }
    }

    private static string CreateDeterministicTestPhotoBase64()
    {
        using var bitmap = new SKBitmap(80, 100, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            // Background fill
            canvas.Clear(new SKColor(220, 230, 242));

            // Silhouette torso
            using var torsoPaint = new SKPaint
            {
                Color = new SKColor(70, 80, 95),
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(new SKRect(10, 60, 70, 100), 10, 10, torsoPaint);

            // Head oval
            using var headPaint = new SKPaint
            {
                Color = new SKColor(235, 185, 155),
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawOval(new SKRect(25, 20, 55, 58), headPaint);

            // Hair
            using var hairPaint = new SKPaint
            {
                Color = new SKColor(40, 30, 25),
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(new SKRect(25, 16, 55, 28), hairPaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }
}
