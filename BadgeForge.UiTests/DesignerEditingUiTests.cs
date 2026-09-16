using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App;
using BadgeForge.App.Controls;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Templates.Layers;
using SkiaSharp;
using Xunit;

namespace BadgeForge.UiTests;

/// <summary>
/// Drives the real designer window's keyboard and crop box: Delete and Backspace remove the selected layer,
/// Ctrl+Z and Ctrl+Y step the design back and forward, text boxes keep their own editing keys, and dragging a crop
/// handle on the card crops the picture instead of rescaling it.
/// </summary>
public class DesignerEditingUiTests
{
    private sealed record Harness(MainWindow Window, MainWindowViewModel Vm, InteractiveBadgeCanvas Canvas);

    private static Harness Open()
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        var vm = (MainWindowViewModel)window.DataContext!;
        vm.IsDesignPageActive = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var canvas = window.FindControl<InteractiveBadgeCanvas>("BadgeCanvas")!;
        canvas.Focus();
        Dispatcher.UIThread.RunJobs();
        return new Harness(window, vm, canvas);
    }

    private static TemplateLayer LayerNamed(MainWindowViewModel vm, string name) =>
        vm.Template.Layers.Single(l => l.Name == name);

    private static void Press(Harness h, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        h.Window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task DeleteKey_RemovesTheSelectedLayer() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            h.Vm.SelectedLayer = LayerNamed(h.Vm, "QR Code");
            int before = h.Vm.Template.Layers.Count;

            Press(h, PhysicalKey.Delete);

            Assert.Equal(before - 1, h.Vm.Template.Layers.Count);
            Assert.DoesNotContain(h.Vm.Template.Layers, l => l.Name == "QR Code");
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task BackspaceKey_AlsoRemovesTheSelectedLayer() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            h.Vm.SelectedLayer = LayerNamed(h.Vm, "Department");
            int before = h.Vm.Template.Layers.Count;

            Press(h, PhysicalKey.Backspace);

            Assert.Equal(before - 1, h.Vm.Template.Layers.Count);
            Assert.DoesNotContain(h.Vm.Template.Layers, l => l.Name == "Department");
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task DeleteThenCtrlZ_BringsTheLayerBack_AndCtrlYRemovesItAgain() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            var target = LayerNamed(h.Vm, "QR Code");
            h.Vm.SelectedLayer = target;
            int before = h.Vm.Template.Layers.Count;

            Press(h, PhysicalKey.Delete);
            Assert.Equal(before - 1, h.Vm.Template.Layers.Count);

            Press(h, PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Equal(before, h.Vm.Template.Layers.Count);
            Assert.Equal(target, LayerNamed(h.Vm, "QR Code"));

            Press(h, PhysicalKey.Y, RawInputModifiers.Control);
            Assert.Equal(before - 1, h.Vm.Template.Layers.Count);

            // Ctrl+Shift+Z is the other redo convention users arrive with
            Press(h, PhysicalKey.Z, RawInputModifiers.Control);
            Press(h, PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
            Assert.Equal(before - 1, h.Vm.Template.Layers.Count);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task DeleteWhileTypingOnTheCard_EditsTheText_AndLeavesTheLayerAlone() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            var layer = (TextLayer)LayerNamed(h.Vm, "Company Header");
            h.Vm.SelectedLayer = layer;
            int before = h.Vm.Template.Layers.Count;

            // Double-clicking text opens the in-place editor, which is a text box and owns Delete and Backspace
            var point = h.Canvas.TranslatePoint(h.Canvas.GetLayerBounds(layer).Center, h.Window)!.Value;
            h.Window.MouseDown(point, MouseButton.Left);
            h.Window.MouseUp(point, MouseButton.Left);
            h.Window.MouseDown(point, MouseButton.Left);
            h.Window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(h.Vm.IsInlineTextEditing);

            Press(h, PhysicalKey.Delete);
            Press(h, PhysicalKey.Backspace);

            Assert.Equal(before, h.Vm.Template.Layers.Count);
            Assert.True(h.Vm.IsInlineTextEditing);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task DeleteKey_IsIgnoredWhenNoLayerIsSelected() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            h.Vm.SelectedLayer = null;
            int before = h.Vm.Template.Layers.Count;

            Press(h, PhysicalKey.Delete);

            Assert.Equal(before, h.Vm.Template.Layers.Count);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task ArrowKeys_NudgeTheSelectedLayer_FineByDefaultAndAMillimetreWithShift() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            h.Vm.SelectedLayer = LayerNamed(h.Vm, "Full Name");
            h.Canvas.Focus();
            Dispatcher.UIThread.RunJobs();

            double x = h.Vm.SelectedLayer!.X;
            double y = h.Vm.SelectedLayer.Y;

            Press(h, PhysicalKey.ArrowRight);
            Assert.Equal(x + 0.1, h.Vm.SelectedLayer!.X, 2);

            Press(h, PhysicalKey.ArrowDown);
            Assert.Equal(y + 0.1, h.Vm.SelectedLayer!.Y, 2);

            Press(h, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            Assert.Equal(x - 0.9, h.Vm.SelectedLayer!.X, 2);

            Press(h, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            Assert.Equal(y - 0.9, h.Vm.SelectedLayer!.Y, 2);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task NudgingRepeatedly_IsOneUndoStep() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            h.Vm.SelectedLayer = LayerNamed(h.Vm, "Full Name");
            h.Canvas.Focus();
            Dispatcher.UIThread.RunJobs();
            double x = h.Vm.SelectedLayer!.X;

            for (int i = 0; i < 10; i++)
            {
                Press(h, PhysicalKey.ArrowRight);
            }

            Assert.Equal(x + 1.0, h.Vm.SelectedLayer!.X, 2);

            // Holding an arrow down and then pressing Ctrl+Z puts the layer back where it started, not one step back
            Press(h, PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Equal(x, h.Vm.SelectedLayer!.X, 2);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task ArrowKeys_LeaveTheLayerListAlone_AndNeverMoveALockedLayer() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            h.Vm.SelectedLayer = LayerNamed(h.Vm, "Full Name");
            double x = h.Vm.SelectedLayer!.X;

            // The layer list steers its own selection with the arrows. The list itself isn't focusable — clicking a
            // row focuses that row — so focus the row the way the user would reach it.
            var list = h.Window.GetVisualDescendants().OfType<ListBox>().First(l => l.Classes.Contains("layers"));
            Assert.True(list.ContainerFromItem(h.Vm.SelectedLayer!)!.Focus());
            Dispatcher.UIThread.RunJobs();

            Press(h, PhysicalKey.ArrowRight);
            Assert.Equal(x, h.Vm.SelectedLayer!.X, 2);

            // And a locked layer stays put wherever the keyboard is
            h.Canvas.Focus();
            Dispatcher.UIThread.RunJobs();
            h.Vm.ToggleLayerLock(h.Vm.SelectedLayer!);

            Press(h, PhysicalKey.ArrowRight);
            Assert.Equal(x, h.Vm.SelectedLayer!.X, 2);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task ArrowKeys_DoNotMoveALayerWhileTypingOnTheCard() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            var layer = (TextLayer)LayerNamed(h.Vm, "Company Header");
            h.Vm.SelectedLayer = layer;
            double x = layer.X;

            var point = h.Canvas.TranslatePoint(h.Canvas.GetLayerBounds(layer).Center, h.Window)!.Value;
            h.Window.MouseDown(point, MouseButton.Left);
            h.Window.MouseUp(point, MouseButton.Left);
            h.Window.MouseDown(point, MouseButton.Left);
            h.Window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(h.Vm.IsInlineTextEditing);

            // The arrows move the caret through the text being typed
            Press(h, PhysicalKey.ArrowRight);
            Press(h, PhysicalKey.ArrowLeft);

            Assert.Equal(x, h.Vm.SelectedLayer!.X, 2);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task DraggingACropHandle_CropsTheFrame_WithoutMovingThePicture() => HeadlessTestApp.RunOnUiThread(() =>
    {
        string path = WritePng(400, 200);
        var h = Open();
        try
        {
            var layer = AddImage(h, path);
            h.Vm.IsFillMode = true;
            h.Vm.IsRepositioningImage = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(h.Vm.IsRepositioningImage);

            var contentBefore = h.Vm.GetSelectedImageContentRectMm()!.Value;
            double widthBefore = h.Vm.SelectedLayer!.Width;

            // Grab the middle of the box's right edge and pull it in
            var box = h.Canvas.GetLayerBounds(h.Vm.SelectedLayer!);
            var grab = h.Canvas.TranslatePoint(new Point(box.Right, box.Center.Y), h.Window)!.Value;
            var drop = h.Canvas.TranslatePoint(new Point(box.Right - 40, box.Center.Y), h.Window)!.Value;

            h.Window.MouseDown(grab, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            h.Window.MouseMove(drop);
            Dispatcher.UIThread.RunJobs();
            h.Window.MouseUp(drop, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(h.Vm.SelectedLayer!.Width < widthBefore - 1,
                $"The crop box should have narrowed from {widthBefore:F2} mm, but is {h.Vm.SelectedLayer.Width:F2} mm.");

            // The picture stayed put on the card: the frame cropped it rather than rescaling it
            var contentAfter = h.Vm.GetSelectedImageContentRectMm()!.Value;
            Assert.Equal(contentBefore.X, contentAfter.X, 1);
            Assert.Equal(contentBefore.Width, contentAfter.Width, 1);

            // And the whole drag is a single undo step
            Assert.True(h.Vm.Undo());
            Assert.Equal(widthBefore, h.Vm.SelectedLayer!.Width, 2);
        }
        finally
        {
            h.Window.Close();
            File.Delete(path);
        }
    });

    [Fact]
    public Task DraggingInsideTheCropBox_MovesThePictureNotTheFrame() => HeadlessTestApp.RunOnUiThread(() =>
    {
        string path = WritePng(400, 200);
        var h = Open();
        try
        {
            AddImage(h, path);
            h.Vm.IsFillMode = true;

            // The frame was shaped to the picture, so zoom in first: with nothing overflowing there is nothing to pan
            h.Vm.ImageZoom = 2.0;
            h.Vm.IsRepositioningImage = true;
            Dispatcher.UIThread.RunJobs();

            double frameX = h.Vm.SelectedLayer!.X;
            double offsetBefore = ((IImageLayer)h.Vm.SelectedLayer!).Adjustments.OffsetX;

            var box = h.Canvas.GetLayerBounds(h.Vm.SelectedLayer!);
            var from = h.Canvas.TranslatePoint(box.Center, h.Window)!.Value;
            var to = h.Canvas.TranslatePoint(box.Center + new Vector(25, 0), h.Window)!.Value;

            h.Window.MouseDown(from, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            h.Window.MouseMove(to);
            Dispatcher.UIThread.RunJobs();
            h.Window.MouseUp(to, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(frameX, h.Vm.SelectedLayer!.X, 2);
            Assert.NotEqual(offsetBefore, ((IImageLayer)h.Vm.SelectedLayer!).Adjustments.OffsetX, 3);
        }
        finally
        {
            h.Window.Close();
            File.Delete(path);
        }
    });

    /// <summary>
    /// Adds an image layer from a file while the dispatcher keeps pumping, since the import runs off the UI thread
    /// and finishes back on it.
    /// </summary>
    private static TemplateLayer AddImage(Harness h, string path)
    {
        var task = h.Vm.AddImageLayerFromFileAsync(path);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The image never finished importing.");
            }

            Thread.Sleep(1);
        }

        var layer = task.GetAwaiter().GetResult();
        Assert.NotNull(layer);
        Dispatcher.UIThread.RunJobs();
        return layer!;
    }

    private static string WritePng(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"badgeforge-ui-crop-{Guid.NewGuid():N}.png");
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
