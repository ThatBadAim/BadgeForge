using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using SkiaSharp;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// The crop box: dragging its edges changes which part of the picture shows, leaving the picture itself where it
/// sits on the card. The geometry is the inverse of the one the renderer uses, so the two have to agree exactly.
/// </summary>
public class CropBoxTests
{
    private const float Tolerance = 0.01f;

    [Theory]
    [InlineData(PhotoCropMode.AspectFill)]
    [InlineData(PhotoCropMode.AspectFit)]
    [InlineData(PhotoCropMode.Stretch)]
    public void FitContentRect_ReproducesWhateverComputeContentRectProduced(PhotoCropMode mode)
    {
        var frame = new SKRect(10, 20, 90, 80);
        var adjustments = new ImageAdjustments { Zoom = 2.4, OffsetX = -0.6, OffsetY = 0.35 };

        var content = ImageLayout.ComputeContentRect(300, 150, frame, mode, adjustments);
        var solved = ImageLayout.FitContentRect(300, 150, frame, mode, ImageAdjustments.None, content);
        var again = ImageLayout.ComputeContentRect(300, 150, frame, mode, solved);

        Assert.Equal(content.Left, again.Left, Tolerance);
        Assert.Equal(content.Top, again.Top, Tolerance);
        Assert.Equal(content.Right, again.Right, Tolerance);
        Assert.Equal(content.Bottom, again.Bottom, Tolerance);
    }

    [Fact]
    public void FitContentRect_WithASmallerFrame_LeavesThePictureExactlyWhereItWas()
    {
        var frame = new SKRect(0, 0, 100, 100);
        var content = ImageLayout.ComputeContentRect(200, 100, frame, PhotoCropMode.AspectFill, ImageAdjustments.None);

        // Pull the right edge of the crop box in by 40: the picture must not move or resize
        var cropped = new SKRect(0, 0, 60, 100);
        var solved = ImageLayout.FitContentRect(200, 100, cropped, PhotoCropMode.AspectFill, ImageAdjustments.None, content);
        var after = ImageLayout.ComputeContentRect(200, 100, cropped, PhotoCropMode.AspectFill, solved);

        Assert.Equal(content.Left, after.Left, Tolerance);
        Assert.Equal(content.Top, after.Top, Tolerance);
        Assert.Equal(content.Width, after.Width, Tolerance);
        Assert.Equal(content.Height, after.Height, Tolerance);
    }

    [Fact]
    public void FitContentRect_KeepsZoomWithinRange_WhenTheBoxGrowsPastWhatTheImageCanCover()
    {
        var frame = new SKRect(0, 0, 400, 400);
        var tiny = new SKRect(0, 0, 10, 10);

        var solved = ImageLayout.FitContentRect(200, 100, frame, PhotoCropMode.AspectFill, ImageAdjustments.None, tiny);

        Assert.Equal(ImageAdjustments.MinZoom, solved.Zoom);
        Assert.InRange(solved.OffsetX, -1.0, 1.0);
        Assert.InRange(solved.OffsetY, -1.0, 1.0);
    }

    [Fact]
    public void FitContentRect_IgnoresNonsenseInput()
    {
        var adjustments = new ImageAdjustments { Zoom = 1.8 };
        var frame = new SKRect(0, 0, 100, 100);

        Assert.Same(adjustments, ImageLayout.FitContentRect(0, 0, frame, PhotoCropMode.AspectFill, adjustments, frame));
        Assert.Same(adjustments, ImageLayout.FitContentRect(100, 100, frame, PhotoCropMode.AspectFill, adjustments, SKRect.Empty));
    }

    [Fact]
    public async Task CropBoxDrag_ChangesTheFrame_ButNotWhereThePictureSitsOnTheCard()
    {
        string path = WritePng(400, 200);
        try
        {
            var vm = new MainWindowViewModel();
            var layer = await vm.AddImageLayerFromFileAsync(path);
            Assert.NotNull(layer);

            vm.IsFillMode = true;
            var before = vm.GetSelectedImageContentRectMm();
            Assert.NotNull(before);

            var frame = new Rect(layer!.X, layer.Y, layer.Width, layer.Height);
            var narrower = new Rect(frame.X + 3, frame.Y, frame.Width - 3, frame.Height);

            Assert.True(vm.SetSelectedImageCropFrame(narrower, before!.Value));

            var cropped = vm.SelectedLayer!;
            Assert.Equal(narrower.X, cropped.X, 2);
            Assert.Equal(narrower.Width, cropped.Width, 2);

            var after = vm.GetSelectedImageContentRectMm();
            Assert.NotNull(after);
            Assert.Equal(before.Value.X, after!.Value.X, 1);
            Assert.Equal(before.Value.Y, after.Value.Y, 1);
            Assert.Equal(before.Value.Width, after.Value.Width, 1);
            Assert.Equal(before.Value.Height, after.Value.Height, 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CropBoxDrag_IsOneUndoStep_AndUndoRestoresTheFrameAndThePicture()
    {
        string path = WritePng(400, 200);
        try
        {
            var vm = new MainWindowViewModel();
            var layer = await vm.AddImageLayerFromFileAsync(path);
            vm.IsFillMode = true;
            vm.EndHistoryGesture();

            var start = new Rect(layer!.X, layer.Y, layer.Width, layer.Height);
            var keep = vm.GetSelectedImageContentRectMm()!.Value;
            var startAdjustments = ((IImageLayer)vm.SelectedLayer!).Adjustments;

            // A drag arrives as a stream of frames, all part of one gesture
            for (int i = 1; i <= 12; i++)
            {
                vm.SetSelectedImageCropFrame(new Rect(start.X, start.Y, start.Width - i * 0.4, start.Height), keep);
            }

            vm.EndHistoryGesture();
            Assert.True(vm.SelectedLayer!.Width < start.Width);

            Assert.True(vm.Undo());
            Assert.Equal(start.Width, vm.SelectedLayer!.Width, 2);
            Assert.Equal(startAdjustments, ((IImageLayer)vm.SelectedLayer).Adjustments);

            // The whole drag was one step, so there is nothing more from it to undo
            Assert.False(vm.Undo() && vm.SelectedLayer!.Width != start.Width);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CropBoxDrag_IsRefusedOnALockedLayer()
    {
        string path = WritePng(400, 200);
        try
        {
            var vm = new MainWindowViewModel();
            var layer = await vm.AddImageLayerFromFileAsync(path);
            vm.ToggleLayerLock(vm.SelectedLayer!);

            var frame = new Rect(layer!.X, layer.Y, layer.Width * 0.5, layer.Height);
            Assert.False(vm.SetSelectedImageCropFrame(frame, frame));
            Assert.Equal(layer.Width, vm.SelectedLayer!.Width, 2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CropHandleHitTest_FindsCornersBeforeEdges_AndMissesTheMiddle()
    {
        var box = new Rect(100, 100, 200, 120);

        Assert.Equal("NW", BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(101, 101)));
        Assert.Equal("SE", BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(299, 219)));
        Assert.Equal("N", BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(200, 102)));
        Assert.Equal("W", BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(102, 160)));

        // Just outside the edge still grabs: the handle straddles it
        Assert.Equal("E", BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(304, 160)));

        Assert.Null(BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(200, 160)));
        Assert.Null(BadgeForge.App.Controls.InteractiveBadgeCanvas.HitTestCropHandles(box, new Point(340, 160)));
    }

    private static string WritePng(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"badgeforge-crop-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Orange);
            canvas.DrawRect(new SKRect(0, 0, width / 2f, height), new SKPaint { Color = SKColors.Teal });
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
