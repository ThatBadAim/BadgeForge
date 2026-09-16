using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using AvaloniaTextAlignment = Avalonia.Media.TextAlignment;
using LayerTextAlignment = BadgeForge.Core.Templates.Enums.TextAlignment;

namespace BadgeForge.App.Controls;

/// <summary>
/// The text box laid exactly over a text layer while it is edited in place on the badge canvas.
/// It copies the layer's typography — font file, weight, size after shrink-to-fit, color, alignment, line height and
/// wrapping — at the canvas's current zoom, with no background, border or padding, so swapping between the rendered
/// text and the editor doesn't shift anything.
/// <para>
/// Lifecycle, driven by <see cref="InteractiveBadgeCanvas"/>: <see cref="Load"/> → the canvas arranges it over the
/// layer, calling <see cref="ApplyGeometry"/> on every arrange (zoom, pan, window resize, typing) → focus and select
/// all → Enter or focus loss raises <see cref="CommitRequested"/>, Esc raises <see cref="CancelRequested"/> → the canvas
/// hides it and calls <see cref="Unload"/>.
/// </para>
/// </summary>
public class InlineTextEditor : TextBox
{
    // Fluent's TextBox template repaints its border element per state from these resources; blanking them on the
    // instance keeps the editor chrome-free when pointed at, focused or disabled
    private static readonly string[] TransparentBrushKeys =
    {
        "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
        "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushDisabled"
    };

    private static readonly string[] ZeroThicknessKeys = { "TextControlBorderThemeThickness", "TextControlBorderThemeThicknessFocused" };

    private const string BundledFontsUri = "avares://BadgeForge.App/Assets/Fonts";
    private static readonly Dictionary<string, FontFamily> FontFamilies = new(StringComparer.OrdinalIgnoreCase);

    private bool _isLoading;

    public InlineTextEditor()
    {
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(0);
        MinHeight = 0;
        MinWidth = 0;
        AcceptsReturn = true;
        AcceptsTab = false;
        VerticalContentAlignment = VerticalAlignment.Center;
        SelectionBrush = new SolidColorBrush(Color.FromArgb(110, 147, 177, 167));
        SelectionForegroundBrush = null;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Hidden);
        ScrollViewer.SetVerticalScrollBarVisibility(this, ScrollBarVisibility.Hidden);

        foreach (string key in TransparentBrushKeys)
        {
            Resources[key] = Brushes.Transparent;
        }

        foreach (string key in ZeroThicknessKeys)
        {
            Resources[key] = new Thickness(0);
        }
    }

    // Keep Fluent's TextBox template and styles for this subclass
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>
    /// Enter (without Shift) was pressed, or the caller should treat focus loss as done.
    /// </summary>
    public event EventHandler? CommitRequested;

    /// <summary>
    /// Esc was pressed: discard the edit.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// The user changed the text (not raised while loading).
    /// </summary>
    public event EventHandler? TextEdited;

    /// <summary>
    /// The layer being edited, or null when the editor isn't in use.
    /// </summary>
    public TextLayer? Layer { get; private set; }

    /// <summary>
    /// The size, in points, the badge prints the current text at — the layer's size, or smaller after shrink-to-fit.
    /// </summary>
    public double EffectiveFontSizePoints { get; private set; }

    /// <summary>
    /// Starts editing <paramref name="layer"/> with <paramref name="text"/> in the box.
    /// </summary>
    public void Load(TextLayer layer, string text)
    {
        _isLoading = true;
        try
        {
            Layer = layer;
            Text = text;
            ApplyStyle(layer);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Follows an updated copy of the layer (restyled mid-edit) without touching the typed text.
    /// </summary>
    public void UpdateLayer(TextLayer layer)
    {
        Layer = layer;
        ApplyStyle(layer);
    }

    public void Unload()
    {
        Layer = null;
        _isLoading = true;
        try
        {
            Text = string.Empty;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Sizes the typography for the canvas's current scale. Shrink-to-fit is measured on the print canvas with the
    /// renderer's own layout engine, so the editor shrinks exactly as the printed text does while the user types.
    /// </summary>
    /// <param name="format">The card format the template renders at.</param>
    /// <param name="screenPixelsPerMm">How many control pixels one card millimeter spans at the current zoom.</param>
    public void ApplyGeometry(CardFormat format, double screenPixelsPerMm)
    {
        if (Layer is not { } layer || format.Dpi <= 0 || screenPixelsPerMm <= 0)
        {
            return;
        }

        double printPixelsPerMm = format.Dpi / 25.4;
        var layout = TextRenderer.LayoutText(
            layer,
            Text ?? string.Empty,
            (float)(layer.Width * printPixelsPerMm),
            (float)(layer.Height * printPixelsPerMm),
            printPixelsPerMm);

        EffectiveFontSizePoints = layout.FontSizePx * 72.0 / format.Dpi;

        // Rounded so re-arranging at the same zoom doesn't keep invalidating layout with float noise
        double fontSize = Math.Round(Math.Max(1.0, EffectiveFontSizePoints * screenPixelsPerMm * 25.4 / 72.0), 2);
        FontSize = fontSize;

        // Explicit line height only matters for several lines; a single line keeps the font's natural box, which is
        // what the renderer centres in the frame
        bool multiLine = layer.Overflow == TextOverflowMode.Wrap || (Text?.Contains('\n') ?? false);
        LineHeight = multiLine ? Math.Round(fontSize * layer.LineHeight, 2) : double.NaN;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Handled before the TextBox sees the key, which would otherwise insert a line break for Enter
        switch (e.Key)
        {
            case Key.Enter when (e.KeyModifiers & KeyModifiers.Shift) == 0:
                e.Handled = true;
                CommitRequested?.Invoke(this, EventArgs.Empty);
                return;

            case Key.Escape:
                e.Handled = true;
                CancelRequested?.Invoke(this, EventArgs.Empty);
                return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && !_isLoading && Layer != null)
        {
            TextEdited?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyStyle(TextLayer layer)
    {
        FontFamily = ResolveFontFamily(layer);
        FontWeight = ParseFontWeight(layer.FontWeight);
        FontStyle = FontStyle.Normal;

        // K-resin text always prints solid black at full strength, whatever color or opacity the layer stores
        if (layer.IsPureBlackKResin)
        {
            Foreground = Brushes.Black;
            Opacity = 1.0;
        }
        else
        {
            Foreground = new SolidColorBrush(Color.TryParse(layer.ColorHex, out var color) ? color : Colors.Black);
            Opacity = Math.Clamp(layer.Opacity, 0.0, 1.0);
        }

        CaretBrush = Foreground;
        TextAlignment = layer.Alignment switch
        {
            LayerTextAlignment.Center => AvaloniaTextAlignment.Center,
            LayerTextAlignment.Right => AvaloniaTextAlignment.Right,
            _ => AvaloniaTextAlignment.Left
        };
        TextWrapping = layer.Overflow == TextOverflowMode.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    /// <summary>
    /// The font the renderer really uses for the layer: the installed family when it is genuinely present, otherwise
    /// the same bundled Liberation file the renderer fell back to, embedded in the app.
    /// </summary>
    internal static FontFamily ResolveFontFamily(TextLayer layer)
    {
        string family = TextRenderer.ResolveTypeface(layer).FamilyName;
        lock (FontFamilies)
        {
            if (!FontFamilies.TryGetValue(family, out var fontFamily))
            {
                fontFamily = family.StartsWith("Liberation", StringComparison.OrdinalIgnoreCase)
                    ? new FontFamily($"{BundledFontsUri}#{family}")
                    : new FontFamily(family);
                FontFamilies[family] = fontFamily;
            }

            return fontFamily;
        }
    }

    private static FontWeight ParseFontWeight(string? weight) => weight?.Trim().ToLowerInvariant() switch
    {
        "thin" or "100" => FontWeight.Thin,
        "extralight" or "ultralight" or "200" => FontWeight.ExtraLight,
        "light" or "300" => FontWeight.Light,
        "medium" or "500" => FontWeight.Medium,
        "semibold" or "demibold" or "600" => FontWeight.SemiBold,
        "bold" or "700" => FontWeight.Bold,
        "extrabold" or "ultrabold" or "800" => FontWeight.ExtraBold,
        "black" or "heavy" or "900" => FontWeight.Black,
        _ => FontWeight.Normal
    };
}
