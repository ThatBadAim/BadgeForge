using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using BadgeForge.App;
using BadgeForge.App.Controls;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Layers;
using SkiaSharp;
using Xunit;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace BadgeForge.UiTests;

/// <summary>
/// The colour dropper reads the colour out of the drawn card, so text can be matched to a logo or a photo without
/// anyone knowing its hex code.
/// </summary>
public class ColorDropperUiTests
{
    private static readonly Color Left = Color.FromRgb(0x11, 0x88, 0x77);
    private static readonly Color Right = Color.FromRgb(0xEE, 0x66, 0x22);

    /// <summary>A canvas showing a card that is one colour on the left half and another on the right.</summary>
    private static (Window Window, InteractiveBadgeCanvas Canvas) OpenTwoColourCard()
    {
        var vm = new MainWindowViewModel();
        var canvas = new InteractiveBadgeCanvas
        {
            DataContext = vm,
            Format = vm.ActiveFormat,
            Bitmap = TwoColourBitmap(300, 190)
        };

        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        window.Show();

        // The dropper needs the view model's own preview to have been drawn at least once, which happens on a
        // worker thread and comes back through the dispatcher
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (vm.PreviewBitmap == null)
        {
            Dispatcher.UIThread.RunJobs();
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The card preview was never drawn.");
            }

            Thread.Sleep(1);
        }

        Dispatcher.UIThread.RunJobs();
        return (window, canvas);
    }

    [Fact]
    public Task SampleCardColor_ReadsTheColourUnderThePointer() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        try
        {
            var card = canvas.GetCardBounds();

            var left = canvas.SampleCardColor(new Point(card.X + card.Width * 0.25, card.Center.Y));
            var right = canvas.SampleCardColor(new Point(card.X + card.Width * 0.75, card.Center.Y));

            Assert.Equal(Left, left);
            Assert.Equal(Right, right);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SampleCardColor_ReturnsNothingOffTheCard() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        try
        {
            var card = canvas.GetCardBounds();

            Assert.Null(canvas.SampleCardColor(new Point(card.X - 12, card.Center.Y)));
            Assert.Null(canvas.SampleCardColor(new Point(card.Center.X, card.Y - 12)));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ClickingWhilePicking_GivesTheTextLayerThatColour_AsOneUndoStep() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        var vm = (MainWindowViewModel)canvas.DataContext!;
        try
        {
            var text = vm.Template.Layers.OfType<TextLayer>().First();
            vm.SelectedLayer = text;
            string before = vm.TextColorHex;

            vm.IsPickingColor = true;
            Assert.True(vm.IsPickingColor);

            var card = canvas.GetCardBounds();
            var target = canvas.TranslatePoint(new Point(card.X + card.Width * 0.75, card.Center.Y), window)!.Value;
            window.MouseDown(target, MouseButton.Left);
            window.MouseUp(target, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("#EE6622", vm.TextColorHex);
            Assert.False(vm.IsPickingColor);      // one pick per press; the tool puts itself away
            Assert.Contains("matching the colour", vm.UndoTooltip);

            Assert.True(vm.Undo());
            Assert.Equal(before, vm.TextColorHex);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ClickingWhilePicking_DoesNotSelectOrMoveALayer() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        var vm = (MainWindowViewModel)canvas.DataContext!;
        try
        {
            var text = vm.Template.Layers.OfType<TextLayer>().First();
            vm.SelectedLayer = text;
            double x = text.X, y = text.Y;

            vm.IsPickingColor = true;

            // Press on top of another layer and drag: normally that would select and move it
            var other = vm.Template.Layers.First(l => l.Id != text.Id);
            var from = canvas.TranslatePoint(canvas.GetLayerBounds(other).Center, window)!.Value;
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Vector(30, 20));
            window.MouseUp(from + new Vector(30, 20), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(text.Id, vm.SelectedLayer?.Id);
            Assert.Equal(other.X, vm.Template.Layers.Single(l => l.Id == other.Id).X);
            Assert.Equal(x, vm.Template.Layers.OfType<TextLayer>().First(l => l.Id == text.Id).X);
            Assert.Equal(y, vm.Template.Layers.OfType<TextLayer>().First(l => l.Id == text.Id).Y);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Escape_LeavesPickingWithoutChangingTheColour() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        var vm = (MainWindowViewModel)canvas.DataContext!;
        try
        {
            vm.SelectedLayer = vm.Template.Layers.OfType<TextLayer>().First();
            string before = vm.TextColorHex;

            vm.IsPickingColor = true;
            canvas.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(vm.IsPickingColor);
            Assert.Equal(before, vm.TextColorHex);
            Assert.False(vm.CanUndo);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Picking_IsOfferedOnlyForAnUnlockedTextLayer() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        var vm = (MainWindowViewModel)canvas.DataContext!;
        try
        {
            // Nothing selected: there is nowhere to put a colour
            vm.SelectedLayer = null;
            Assert.False(vm.CanPickColor);
            vm.IsPickingColor = true;
            Assert.False(vm.IsPickingColor);

            // A photo or barcode has no colour of its own to set
            vm.SelectedLayer = vm.Template.Layers.First(l => l is not TextLayer);
            Assert.False(vm.CanPickColor);

            var text = vm.Template.Layers.OfType<TextLayer>().First();
            vm.SelectedLayer = text;
            Assert.True(vm.CanPickColor);

            // A locked layer is inspected, never edited
            vm.ToggleLayerLock(vm.SelectedLayer!);
            Assert.False(vm.CanPickColor);
            vm.IsPickingColor = true;
            Assert.False(vm.IsPickingColor);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ChangingTheSelection_PutsTheDropperAway() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var (window, canvas) = OpenTwoColourCard();
        var vm = (MainWindowViewModel)canvas.DataContext!;
        try
        {
            vm.SelectedLayer = vm.Template.Layers.OfType<TextLayer>().First();
            vm.IsPickingColor = true;
            Assert.True(vm.IsPickingColor);

            vm.SelectedLayer = vm.Template.Layers.First(l => l is not TextLayer);

            Assert.False(vm.IsPickingColor);
        }
        finally
        {
            window.Close();
        }
    });

    private static AvaloniaBitmap TwoColourBitmap(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(Left.R, Left.G, Left.B));
            canvas.DrawRect(new SKRect(width / 2f, 0, width, height), new SKPaint { Color = new SKColor(Right.R, Right.G, Right.B) });
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new AvaloniaBitmap(stream);
    }
}
