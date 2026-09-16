using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

public class ImageAdjustmentTests
{
    [Fact]
    public void ImageLayout_AspectFill_ZoomsAndPansWithinOverflow()
    {
        var frame = new SKRect(0, 0, 100, 100);

        var identity = ImageLayout.ComputeContentRect(200, 100, frame, PhotoCropMode.AspectFill, ImageAdjustments.None);
        Assert.Equal(new SKRect(-50, 0, 150, 100), identity);

        var zoomed = ImageLayout.ComputeContentRect(200, 100, frame, PhotoCropMode.AspectFill, new ImageAdjustments { Zoom = 2 });
        Assert.Equal(new SKRect(-150, -50, 250, 150), zoomed);

        // Full pan aligns the image's left edge with the frame's left edge, and is clamped beyond that
        var panned = ImageLayout.ComputeContentRect(200, 100, frame, PhotoCropMode.AspectFill, new ImageAdjustments { OffsetX = 1 });
        var overPanned = ImageLayout.ComputeContentRect(200, 100, frame, PhotoCropMode.AspectFill, new ImageAdjustments { OffsetX = 5 });
        Assert.Equal(0f, panned.Left);
        Assert.Equal(panned, overPanned);
    }

    [Fact]
    public void ImageLayout_QuarterTurn_SwapsOrientedSize()
    {
        var frame = new SKRect(0, 0, 100, 100);
        var content = ImageLayout.ComputeContentRect(200, 100, frame, PhotoCropMode.AspectFit, new ImageAdjustments { Rotation = 90 });

        Assert.Equal(50f, content.Width, 3);
        Assert.Equal(100f, content.Height, 3);
    }

    [Fact]
    public void ImageLayout_Pan_ConvertsFrameDeltaToNormalizedOffset()
    {
        // 200x100 filling a 100x100 frame overflows by 50 on each side
        var panned = ImageLayout.Pan(200, 100, 100, 100, PhotoCropMode.AspectFill, ImageAdjustments.None, deltaX: 25, deltaY: 10);

        Assert.Equal(0.5, panned.OffsetX, 6);
        Assert.Equal(0.0, panned.OffsetY); // no vertical overflow
    }

    [Fact]
    public void ImageAdjustments_Normalized_ClampsAndSnapsRotation()
    {
        var normalized = new ImageAdjustments { Zoom = 9, OffsetX = -3, Rotation = -90, Saturation = -4 }.Normalized();

        Assert.Equal(ImageAdjustments.MaxZoom, normalized.Zoom);
        Assert.Equal(-1.0, normalized.OffsetX);
        Assert.Equal(270, normalized.Rotation);
        Assert.Equal(-1.0, normalized.Saturation);
    }

    [Fact]
    public void ImageDrawing_RotateAndFlip_MovesPixelsToExpectedEdges()
    {
        using var source = CreateSplitBitmap(40, 20, SKColors.Red, SKColors.Blue); // red left, blue right

        using var rotated = DrawOnto(source, new ImageAdjustments { Rotation = 90 });
        Assert.True(IsMostly(rotated.GetPixel(20, 5), SKColors.Red), "Rotating clockwise moves the left half to the top");
        Assert.True(IsMostly(rotated.GetPixel(20, 35), SKColors.Blue));

        using var flipped = DrawOnto(source, new ImageAdjustments { FlipHorizontal = true });
        Assert.True(IsMostly(flipped.GetPixel(5, 20), SKColors.Blue), "Horizontal flip moves the right half to the left");
        Assert.True(IsMostly(flipped.GetPixel(35, 20), SKColors.Red));
    }

    [Fact]
    public void ImageDrawing_ColorAdjustments_ApplyGreyscaleAndBrightness()
    {
        using var red = CreateSplitBitmap(10, 10, SKColors.Red, SKColors.Red);
        using var greyscale = DrawOnto(red, new ImageAdjustments { Saturation = -1 });
        var grey = greyscale.GetPixel(20, 20);
        Assert.InRange(Math.Abs(grey.Red - grey.Green), 0, 2);
        Assert.InRange(Math.Abs(grey.Green - grey.Blue), 0, 2);

        using var black = CreateSplitBitmap(10, 10, SKColors.Black, SKColors.Black);
        using var brightened = DrawOnto(black, new ImageAdjustments { Brightness = 0.2 });
        Assert.InRange((int)brightened.GetPixel(20, 20).Red, 49, 53); // +20% of 255
    }

    [Fact]
    public void StaticImageLayer_LegacyMaintainAspectRatioFalse_LoadsAsStretch_AndAdjustmentsRoundTrip()
    {
        var legacy = JsonSerializer.Deserialize<TemplateLayer>("""{"LayerType":"StaticImage","MaintainAspectRatio":false}""");
        Assert.Equal(PhotoCropMode.Stretch, Assert.IsType<StaticImageLayer>(legacy).CropMode);

        var original = new StaticImageLayer
        {
            CropMode = PhotoCropMode.AspectFill,
            BorderRadius = 2.5,
            Adjustments = new ImageAdjustments { Zoom = 2, OffsetX = -0.25, Rotation = 90, FlipVertical = true, Saturation = -1 }
        };

        var json = JsonSerializer.Serialize<TemplateLayer>(original);
        var loaded = Assert.IsType<StaticImageLayer>(JsonSerializer.Deserialize<TemplateLayer>(json));

        Assert.Equal(PhotoCropMode.AspectFill, loaded.CropMode);
        Assert.Equal(2.5, loaded.BorderRadius);
        Assert.Equal(original.Adjustments, loaded.Adjustments);
    }

    [Fact]
    public void ImportFile_DownscalesOversizedImages()
    {
        string path = WritePng(3000, 100);
        try
        {
            var imported = ImageSourceLoader.ImportFile(path);

            Assert.StartsWith("data:image/", imported.DataUri);
            Assert.Equal(ImageSourceLoader.MaxEmbeddedDimension, imported.Width);
            Assert.True(ImageSourceLoader.TryGetSize(imported.DataUri, out int w, out _));
            Assert.Equal(imported.Width, w);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ViewModel_AddImageFromFile_EmbedsImageAndSizesFrameToAspect()
    {
        string path = WritePng(300, 150);
        try
        {
            var vm = new MainWindowViewModel();
            var layer = await vm.AddImageLayerFromFileAsync(path);

            Assert.NotNull(layer);
            Assert.Same(layer, vm.SelectedLayer);
            Assert.True(layer!.HasImage);
            Assert.Equal(Path.GetFileName(path), layer.SourceFileName);
            Assert.Equal(2.0, layer.Width / layer.Height, 1);
            Assert.True(vm.IsImageLayerSelected);
            Assert.True(vm.SelectedLayerHasImage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ViewModel_ImageEdits_UpdateTemplateLayer()
    {
        string path = WritePng(300, 150);
        try
        {
            var vm = new MainWindowViewModel();
            var added = await vm.AddImageLayerFromFileAsync(path);
            double width = added!.Width;
            double height = added.Height;

            vm.IsFillMode = true;
            vm.ImageZoom = 2.0;
            vm.ImageSaturation = -1.0;

            var edited = Assert.IsType<StaticImageLayer>(vm.SelectedLayer);
            Assert.Contains(edited, vm.Template.Layers);
            Assert.Equal(PhotoCropMode.AspectFill, edited.CropMode);
            Assert.Equal(2.0, edited.Adjustments.Zoom);
            Assert.True(vm.HasImageAdjustments);

            // The frame was shaped to the image, so a quarter turn turns the frame too
            vm.RotateSelectedImage(90);
            var rotated = Assert.IsType<StaticImageLayer>(vm.SelectedLayer);
            Assert.Equal(90, rotated.Adjustments.Rotation);
            Assert.Equal(height, rotated.Width);
            Assert.Equal(width, rotated.Height);

            vm.ResetSelectedImageAdjustments();
            Assert.Equal(ImageAdjustments.None, Assert.IsType<StaticImageLayer>(vm.SelectedLayer).Adjustments);
            Assert.False(vm.HasImageAdjustments);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ViewModel_RepositionMode_SurvivesPanning_AndEndsOnSelectionChange()
    {
        string path = WritePng(300, 150);
        try
        {
            var vm = new MainWindowViewModel();
            await vm.AddImageLayerFromFileAsync(path);
            vm.IsFillMode = true;
            vm.ImageZoom = 2.0;

            vm.IsRepositioningImage = true;
            Assert.True(vm.IsRepositioningImage);

            vm.PanSelectedImage(3.0, 0.0);
            Assert.True(vm.IsRepositioningImage);
            Assert.True(Assert.IsType<StaticImageLayer>(vm.SelectedLayer).Adjustments.OffsetX > 0);

            vm.SelectedLayer = vm.Template.Layers.OfType<TextLayer>().First();
            Assert.False(vm.IsRepositioningImage);

            // A frame without an image can't be repositioned
            vm.AddStaticImageLayer();
            vm.IsRepositioningImage = true;
            Assert.False(vm.IsRepositioningImage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ViewModel_PhotoLayer_UploadSetsFallbackImage()
    {
        string path = WritePng(120, 160);
        try
        {
            var vm = new MainWindowViewModel();
            vm.SelectedLayer = vm.Template.Layers.OfType<PhotoLayer>().First();
            Assert.False(vm.SelectedLayerHasImage);

            Assert.True(await vm.SetSelectedLayerImageFromFileAsync(path));

            var photo = Assert.IsType<PhotoLayer>(vm.SelectedLayer);
            Assert.True(ImageSourceLoader.IsDataUri(photo.FallbackImagePath));
            Assert.True(vm.SelectedLayerHasImage);

            vm.ClearSelectedLayerImage();
            Assert.Null(Assert.IsType<PhotoLayer>(vm.SelectedLayer).FallbackImagePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SKBitmap DrawOnto(SKBitmap source, ImageAdjustments adjustments)
    {
        var target = new SKBitmap(40, 40, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);
        ImageDrawing.Draw(canvas, source, new SKRect(0, 0, 40, 40), PhotoCropMode.Stretch, adjustments, 0f, 1.0);
        return target;
    }

    private static SKBitmap CreateSplitBitmap(int width, int height, SKColor left, SKColor right)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        using var leftPaint = new SKPaint { Color = left };
        using var rightPaint = new SKPaint { Color = right };
        canvas.DrawRect(0, 0, width / 2f, height, leftPaint);
        canvas.DrawRect(width / 2f, 0, width / 2f, height, rightPaint);
        return bitmap;
    }

    private static bool IsMostly(SKColor actual, SKColor expected) =>
        Math.Abs(actual.Red - expected.Red) < 40 &&
        Math.Abs(actual.Green - expected.Green) < 40 &&
        Math.Abs(actual.Blue - expected.Blue) < 40;

    private static string WritePng(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"badgeforge-test-{Guid.NewGuid():N}.png");
        using var bitmap = CreateSplitBitmap(width, height, SKColors.Orange, SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
