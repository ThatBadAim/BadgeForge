using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App;
using BadgeForge.App.Controls;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using Xunit;
using AvaloniaTextAlignment = Avalonia.Media.TextAlignment;

namespace BadgeForge.UiTests;

/// <summary>
/// Drives the real badge canvas in a headless Avalonia session (with Skia text) to check the in-place editor's
/// gesture, placement, typography and keyboard lifecycle.
/// </summary>
public class InlineTextEditorUiTests
{
    private static Task RunOnUiThread(Action test) => HeadlessTestApp.RunOnUiThread(test);

    private sealed record Harness(Window Window, InteractiveBadgeCanvas Canvas, MainWindowViewModel Vm, TextLayer Layer);

    private static Harness Open(Func<TextLayer, TextLayer>? configure = null)
    {
        var vm = new MainWindowViewModel();
        vm.NewBlankTemplate();
        vm.AddTextLayer(x: 20, y: 20, text: "Visitor Pass");
        var layer = Assert.IsType<TextLayer>(vm.SelectedLayer);
        if (configure != null)
        {
            var configured = configure(layer);
            vm.TextFontSize = configured.FontSize;
            vm.TextFontWeight = configured.FontWeight;
            vm.TextOverflowIndex = (int)configured.Overflow;
            vm.IsTextAlignCenter = configured.Alignment == Core.Templates.Enums.TextAlignment.Center;
            layer = Assert.IsType<TextLayer>(vm.SelectedLayer);
        }

        var canvas = new InteractiveBadgeCanvas { DataContext = vm, Format = vm.ActiveFormat };
        var window = new Window { Width = 1000, Height = 700, Content = canvas };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Harness(window, canvas, vm, layer);
    }

    private static void DoubleClick(Harness h, TextLayer layer)
    {
        var point = h.Canvas.TranslatePoint(h.Canvas.GetLayerBounds(layer).Center, h.Window)!.Value;
        h.Window.MouseDown(point, MouseButton.Left);
        h.Window.MouseUp(point, MouseButton.Left);
        h.Window.MouseDown(point, MouseButton.Left);
        h.Window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertWithinOnePixel(double expected, double actual) =>
        Assert.True(Math.Abs(expected - actual) <= 1.0, $"Expected {expected:F2}, got {actual:F2}");

    [Fact]
    public Task DoubleClickingText_OpensAFocusedEditorOverTheLayer_WithMatchingTypography() => RunOnUiThread(() =>
    {
        var h = Open(l => l with { FontSize = 14, FontWeight = "Bold", Alignment = Core.Templates.Enums.TextAlignment.Center });
        try
        {
            DoubleClick(h, h.Layer);

            var editor = h.Canvas.InlineEditor;
            Assert.True(h.Vm.IsInlineTextEditing);
            Assert.True(editor.IsVisible);
            Assert.True(editor.IsFocused);
            Assert.Equal("Visitor Pass", editor.Text);
            Assert.Equal(0, editor.SelectionStart);
            Assert.Equal(editor.Text!.Length, editor.SelectionEnd);

            // Same frame, same typography, no chrome
            var frame = h.Canvas.GetLayerBounds(h.Layer);
            // Centred on the frame (layout rounding snaps to whole pixels); single-line editors get a few pixels of
            // caret slack, split either side for centred text
            AssertWithinOnePixel(frame.Center.X, editor.Bounds.Center.X);
            Assert.InRange(editor.Bounds.Width - frame.Width, -1.0, 4.0);
            AssertWithinOnePixel(frame.Center.Y, editor.Bounds.Center.Y);
            Assert.Equal(14.0, editor.EffectiveFontSizePoints, 1);
            double expectedDips = 14.0 * (frame.Height / h.Layer.Height) * 25.4 / 72.0;
            Assert.Equal(expectedDips, editor.FontSize, 1);
            Assert.Equal(FontWeight.Bold, editor.FontWeight);
            Assert.Equal(AvaloniaTextAlignment.Center, editor.TextAlignment);
            Assert.Equal(new Thickness(0), editor.Padding);

            var border = editor.GetVisualDescendants().OfType<Border>().First();
            Assert.True(border.Background is null or ISolidColorBrush { Color.A: 0 }, $"Editor background is {border.Background}");
            Assert.Equal(default, border.BorderThickness);

            // The font the editor draws with is the same face the renderer prints with
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(editor.FontFamily, FontStyle.Normal, FontWeight.Bold), out var glyphs));
            Assert.Equal(
                Core.Rendering.Elements.TextRenderer.ResolveTypeface(h.Layer).FamilyName,
                glyphs!.FamilyName);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task Escape_DiscardsTheEdit() => RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            DoubleClick(h, h.Layer);
            h.Window.KeyTextInput("Staff");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Staff", h.Vm.InlineEditingText);

            h.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(h.Vm.IsInlineTextEditing);
            Assert.False(h.Canvas.InlineEditor.IsVisible);
            Assert.Equal("Visitor Pass", Assert.IsType<TextLayer>(Assert.Single(h.Vm.Template.Layers)).Text);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task Enter_Commits_AndShiftEnterAddsALine() => RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            DoubleClick(h, h.Layer);
            h.Window.KeyTextInput("Staff");
            h.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
            h.Window.KeyTextInput("{{Department}}");
            Dispatcher.UIThread.RunJobs();
            Assert.True(h.Vm.IsInlineTextEditing);

            h.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(h.Vm.IsInlineTextEditing);
            var text = Assert.IsType<TextLayer>(Assert.Single(h.Vm.Template.Layers)).Text.ReplaceLineEndings("\n");
            Assert.Equal("Staff\n{{Department}}", text);
            Assert.True(h.Canvas.IsFocused);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task ClickingElsewhereOnTheCanvas_CommitsTheEdit() => RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            DoubleClick(h, h.Layer);
            h.Window.KeyTextInput("Contractor");
            Dispatcher.UIThread.RunJobs();

            var emptySpot = h.Canvas.TranslatePoint(new Point(5, 5), h.Window)!.Value;
            h.Window.MouseDown(emptySpot, MouseButton.Left);
            h.Window.MouseUp(emptySpot, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var focused = TopLevel.GetTopLevel(h.Canvas)?.FocusManager?.GetFocusedElement();
            Assert.False(h.Vm.IsInlineTextEditing,
                $"Still editing. Focused: {focused?.GetType().Name ?? "nothing"}; editor focused: {h.Canvas.InlineEditor.IsFocused}; canvas focused: {h.Canvas.IsFocused}");
            Assert.Equal("Contractor", Assert.IsType<TextLayer>(Assert.Single(h.Vm.Template.Layers)).Text);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task WrapMode_EditorGrowsPastAShortFrame() => RunOnUiThread(() =>
    {
        var h = Open(l => l with { Overflow = TextOverflowMode.Wrap });
        try
        {
            DoubleClick(h, h.Layer);
            h.Window.KeyTextInput("Head of Infrastructure and Developer Platforms, Northern Region");
            Dispatcher.UIThread.RunJobs();

            var frame = h.Canvas.GetLayerBounds(h.Layer);
            var editor = h.Canvas.InlineEditor;
            Assert.Equal(TextWrapping.Wrap, editor.TextWrapping);
            Assert.True(editor.Bounds.Height > frame.Height, $"{editor.Bounds.Height} <= {frame.Height}");
            AssertWithinOnePixel(frame.Width, editor.Bounds.Width);
        }
        finally
        {
            h.Window.Close();
        }
    });
}

/// <summary>
/// Entry point for the headless test session: the real app (theme, styles, resources) on the headless platform,
/// drawing and measuring text with Skia so font metrics are real.
/// </summary>
public static class HeadlessTestApp
{
    // One session for the whole test process: Avalonia's UI dispatcher can only be claimed once
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp)));

    public static Task RunOnUiThread(Action test) => Session.Value.Dispatch(test, CancellationToken.None);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App.App>()
        .UseSkia()
        .UseHarfBuzz()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
