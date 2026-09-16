using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Tokens;
using LayerTextAlignment = BadgeForge.Core.Templates.Enums.TextAlignment;

namespace BadgeForge.App.Controls;

/// <summary>
/// Interactive badge canvas supporting zoom, pan, safe margin guides,
/// layer selection, drag-to-move (hold Ctrl to snap to guides), and resize handles in millimeter units.
/// Image layers also support drag-and-drop of image files, and a crop mode (double-click, or the Crop box button)
/// that puts a crop box over the frame: dragging its edges crops the picture, dragging inside moves the picture
/// under it, and the wheel zooms.
/// Double-clicking a text layer edits its text in place with an <see cref="InlineTextEditor"/> hosted as a child of
/// this control. Layers bound to record data through tokens get a dashed outline, and a chip naming their tokens when
/// hovered or selected.
/// </summary>
public class InteractiveBadgeCanvas : Control
{
    public static readonly StyledProperty<Avalonia.Media.Imaging.Bitmap?> BitmapProperty =
        AvaloniaProperty.Register<InteractiveBadgeCanvas, Avalonia.Media.Imaging.Bitmap?>(nameof(Bitmap));

    public static readonly StyledProperty<CardFormat> FormatProperty =
        AvaloniaProperty.Register<InteractiveBadgeCanvas, CardFormat>(nameof(Format), CardFormat.CR80);

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<InteractiveBadgeCanvas, double>(nameof(Zoom), 1.0);

    public Avalonia.Media.Imaging.Bitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public CardFormat Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Raised when the user double-clicks an image or photo frame that has no image yet.
    /// The selected layer is already set to that frame.
    /// </summary>
    public event EventHandler? ChooseImageRequested;

    private bool _isDragging;
    private bool _isResizing;
    private bool _isPanningImage;
    private string? _activeResizeHandle;
    private Point _lastPointerPos;

    private Point _dragStartPointerPos;
    private Point _dragStartLayerMm;
    private bool _isSnapping;
    private double? _snapGuideXmm;
    private double? _snapGuideYmm;

    private Rect _resizeStartRectMm;

    // Crop box drag: which handle is held, the frame it started from, and where the picture sat on the card when it
    // started. Measuring the frame from the start (rather than accumulating per-move steps) keeps the dragged edge
    // under the pointer, and re-anchoring to the *starting* content rectangle stops the picture creeping as the
    // frame is rounded to 0.01 mm on every move.
    private string? _activeCropHandle;
    private Rect _cropStartFrameMm;
    private Rect _cropStartContentMm;

    private bool _isPanningView;
    private Vector _viewOffset;

    private bool _isDropTarget;
    private TemplateLayer? _dropTargetLayer;
    private MainWindowViewModel? _subscribedViewModel;

    // In-place text editing: one editor child, shown over the layer named by the view model's InlineEditingLayer
    private readonly InlineTextEditor _textEditor = new() { IsVisible = false };
    private bool _isHidingTextEditor;
    private TopLevel? _outsidePressRoot;

    // Layer under the pointer, by ID: layers are swapped for updated copies as they are edited
    private string? _hoverLayerId;

    // Colour dropper: what is under the pointer right now, so the swatch shows the colour before it is committed
    private Color? _pickedColor;
    private Point _pickedPoint;

    private bool IsInPointerGesture => _isDragging || _isResizing || _isPanningImage || _isPanningView || _activeCropHandle != null;

    static InteractiveBadgeCanvas()
    {
        AffectsRender<InteractiveBadgeCanvas>(BitmapProperty, FormatProperty, ZoomProperty);
        AffectsArrange<InteractiveBadgeCanvas>(FormatProperty, ZoomProperty);
    }

    public InteractiveBadgeCanvas()
    {
        ClipToBounds = true;
        Focusable = true;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Hosted as a real child (not a popup) so it pans, zooms and clips together with the card
        VisualChildren.Add(_textEditor);
        LogicalChildren.Add(_textEditor);
        _textEditor.CommitRequested += OnTextEditorCommitRequested;
        _textEditor.CancelRequested += OnTextEditorCancelRequested;
        _textEditor.TextEdited += OnTextEditorTextEdited;
        _textEditor.LostFocus += (_, _) => OnTextEditorLostFocus();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private const double FitPadding = 40.0;
    private const double HandleSize = 8.0;
    private const double HandleHitRadius = 7.0;

    // Crop box: bracket arms are drawn inside the box, the grab area straddles its edge
    private const double CropHandleLength = 16.0;
    private const double CropHandleThickness = 3.0;
    private const double CropHandleHitRadius = 8.0;
    private const double SnapDistancePx = 8.0;

    // Extra width for single-line inline editors: see ArrangeOverride
    private const double CaretSlack = 3.0;

    // ISO/IEC 7810 card corner radius
    private const double CardCornerRadiusMm = 3.18;

    private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.Parse("#7A918D"));
    private static readonly IPen SelectionPen = new Pen(SelectionBrush, 1.5);
    private static readonly IPen HandlePen = new Pen(SelectionBrush, 1.5);
    private static readonly IPen CardEdgePen = new Pen(new SolidColorBrush(Color.Parse("#D5DBD9")), 1.0);
    private static readonly IPen SafeAreaPen = new Pen(
        new SolidColorBrush(Color.FromArgb(120, 147, 177, 167)), 1.0, new DashStyle(new double[] { 4, 4 }, 0));

    private static readonly IBrush PlaceholderFill = new SolidColorBrush(Color.FromArgb(150, 236, 238, 236));
    private static readonly IPen PlaceholderPen = new Pen(
        new SolidColorBrush(Color.Parse("#93B1A7")), 1.0, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly IBrush PlaceholderTitleBrush = new SolidColorBrush(Color.Parse("#56625F"));
    private static readonly IBrush PlaceholderHintBrush = new SolidColorBrush(Color.Parse("#86908D"));

    private static readonly IPen RepositionFramePen = new Pen(new SolidColorBrush(Color.Parse("#7A918D")), 2.0);
    private static readonly IBrush CropShadeBrush = new SolidColorBrush(Color.FromArgb(70, 16, 24, 22));
    private static readonly IPen PickerRingPen = new Pen(Brushes.White, 2.0);
    private static readonly IPen PickerEdgePen = new Pen(new SolidColorBrush(Color.Parse("#3F524E")), 1.0);
    private static readonly IPen PickerCrossPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.0);
    private static readonly IBrush CropHandleBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IPen CropHandlePen = new Pen(new SolidColorBrush(Color.Parse("#3F524E")), 1.0);
    private static readonly IPen CropThirdsPen = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), 1.0);
    private static readonly IPen ImageExtentPen = new Pen(
        new SolidColorBrush(Color.Parse("#7A918D")), 1.0, new DashStyle(new double[] { 5, 3 }, 0));
    private static readonly IPen DropPen = new Pen(
        new SolidColorBrush(Color.Parse("#7A918D")), 2.0, new DashStyle(new double[] { 6, 4 }, 0));
    private static readonly IBrush DropFill = new SolidColorBrush(Color.FromArgb(60, 197, 237, 172));
    private static readonly IBrush HintBackground = new SolidColorBrush(Color.FromArgb(230, 29, 38, 36));
    private static readonly IPen SnapGuidePen = new Pen(
        new SolidColorBrush(Color.Parse("#586C68")), 1.0, new DashStyle(new double[] { 4, 3 }, 0));

    private static readonly IPen TokenOutlinePen = new Pen(
        new SolidColorBrush(Color.FromArgb(170, 147, 177, 167)), 1.0, new DashStyle(new double[] { 2, 3 }, 0));
    private static readonly IBrush TokenChipFill = new SolidColorBrush(Color.Parse("#E6EEEB"));
    private static readonly IPen TokenChipPen = new Pen(new SolidColorBrush(Color.Parse("#93B1A7")), 1.0);
    private static readonly IBrush TokenChipTextBrush = new SolidColorBrush(Color.Parse("#3F524E"));

    /// <summary>
    /// Card rectangle in control coordinates. Zoom 1.0 fits the card to the available space.
    /// </summary>
    public Rect GetCardBounds() => GetCardBounds(Bounds.Size);

    /// <summary>
    /// Card rectangle for a control of the given size (during arrange, <see cref="Visual.Bounds"/> still holds the old size).
    /// </summary>
    private Rect GetCardBounds(Size viewSize)
    {
        double aspect = Format.WidthMm / Format.HeightMm;
        double availW = Math.Max(1, viewSize.Width - FitPadding * 2);
        double availH = Math.Max(1, viewSize.Height - FitPadding * 2);

        double fitW = Math.Min(availW, availH * aspect);
        double w = Math.Round(fitW * Zoom);
        double h = Math.Round(w / aspect);

        // Centered, then moved by the view offset. A card larger than the view used to be pinned to the top-left,
        // leaving its right and bottom edges out of reach.
        var offset = ClampViewOffset(_viewOffset, w, h, viewSize);
        double x = Math.Round((viewSize.Width - w) / 2.0 + offset.X);
        double y = Math.Round((viewSize.Height - h) / 2.0 + offset.Y);

        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// Keeps the view offset within the part of the card that doesn't fit, so the card can't be scrolled away.
    /// </summary>
    private static Vector ClampViewOffset(Vector offset, double cardW, double cardH, Size viewSize)
    {
        double limitX = Math.Max(0, (cardW - viewSize.Width) / 2.0 + FitPadding / 2);
        double limitY = Math.Max(0, (cardH - viewSize.Height) / 2.0 + FitPadding / 2);
        return new Vector(Math.Clamp(offset.X, -limitX, limitX), Math.Clamp(offset.Y, -limitY, limitY));
    }

    private void PanView(Vector delta)
    {
        var card = GetCardBounds();
        _viewOffset = ClampViewOffset(_viewOffset + delta, card.Width, card.Height, Bounds.Size);
        InvalidateVisual();
        InvalidateArrange();
    }

    /// <summary>
    /// Fits the whole card in view again.
    /// </summary>
    public void ResetView()
    {
        _viewOffset = default;
        Zoom = 1.0;
        InvalidateVisual();
        InvalidateArrange();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = ViewModel;

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        SyncTextEditor();
        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SelectedLayer)
            or nameof(MainWindowViewModel.IsRepositioningImage)
            or nameof(MainWindowViewModel.IsPickingColor))
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsPickingColor))
            {
                _pickedColor = null;
            }

            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.InlineEditingLayer))
        {
            SyncTextEditor();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var cardBounds = GetCardBounds();
        double radius = cardBounds.Width * CardCornerRadiusMm / Format.WidthMm;

        // 1. Soft layered drop shadow
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(10, 16, 24, 22)), null,
            new RoundedRect(cardBounds.Inflate(6).Translate(new Vector(0, 8)), radius + 6));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(18, 16, 24, 22)), null,
            new RoundedRect(cardBounds.Inflate(1).Translate(new Vector(0, 2)), radius + 1));

        // 2. Card surface
        context.DrawRectangle(Brushes.White, null, new RoundedRect(cardBounds, radius));

        // 3. High-resolution rendered badge bitmap. Without an explicit interpolation mode, Avalonia's
        // default falls back to low-quality (near-nearest-neighbor) scaling on some backends, which makes
        // the 300 DPI render look blurry/aliased once it's scaled down to fit the preview panel.
        if (Bitmap != null)
        {
            using (context.PushClip(new RoundedRect(cardBounds, radius)))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(Bitmap, new Rect(0, 0, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height), cardBounds);
            }
        }

        context.DrawRectangle(null, CardEdgePen, new RoundedRect(cardBounds.Deflate(0.5), radius));

        // 4. Safe-area guide
        double marginPxX = cardBounds.Width * Format.SafeMarginMm / Format.WidthMm;
        double marginPxY = cardBounds.Height * Format.SafeMarginMm / Format.HeightMm;
        var safeBounds = cardBounds.Deflate(new Thickness(marginPxX, marginPxY));
        context.DrawRectangle(null, SafeAreaPen, new RoundedRect(safeBounds, Math.Max(0, radius - marginPxX)));

        var vm = ViewModel;

        // 5. Placeholders for image frames with nothing to draw (designer only, never printed)
        if (vm != null)
        {
            using (context.PushClip(new RoundedRect(cardBounds, radius)))
            {
                foreach (var layer in vm.Template.Layers.Where(l => l is IImageLayer && l.IsVisible).OrderBy(l => l.ZIndex))
                {
                    if (!vm.LayerHasImage(layer))
                    {
                        DrawImagePlaceholder(context, GetLayerScreenRect(layer, cardBounds), layer is PhotoLayer);
                    }
                }
            }
        }

        // 6. Data-bound indicators (designer only, never printed): a dashed outline on every layer fed by record
        // tokens, and a chip naming the tokens of the hovered — or else the selected — one
        if (vm != null)
        {
            TemplateLayer? chipLayer = null;
            foreach (var layer in vm.Template.Layers.Where(l => l.IsVisible && l.HasDynamicTokens))
            {
                context.DrawRectangle(null, TokenOutlinePen, GetLayerScreenRect(layer, cardBounds).Deflate(0.5));
                if (layer.Id == _hoverLayerId || (chipLayer == null && layer.Id == vm.SelectedLayer?.Id))
                {
                    chipLayer = layer;
                }
            }

            if (chipLayer != null && !IsInPointerGesture && chipLayer.Id != vm.InlineEditingLayer?.Id)
            {
                DrawTokenChip(context, GetLayerScreenRect(chipLayer, cardBounds), chipLayer);
            }
        }

        // 7. Selected layer outline and corner resize handles, or the reposition overlay
        if (vm?.SelectedLayer != null)
        {
            var layerRect = GetLayerScreenRect(vm.SelectedLayer, cardBounds);

            if (vm.IsRepositioningImage)
            {
                DrawCropOverlay(context, cardBounds, layerRect,
                    vm.GetSelectedImageContentRectMm() is { } contentMm ? MmToScreen(contentMm, cardBounds) : null,
                    vm.SelectedLayer.IsLocked);
            }
            else
            {
                context.DrawRectangle(null, SelectionPen, layerRect);

                // A locked layer can't be resized, so don't offer handles that do nothing
                if (!vm.SelectedLayer.IsLocked)
                {
                    DrawHandle(context, layerRect.TopLeft);
                    DrawHandle(context, layerRect.TopRight);
                    DrawHandle(context, layerRect.BottomRight);
                    DrawHandle(context, layerRect.BottomLeft);
                }

                if (_isDragging)
                {
                    DrawSnapGuides(context, cardBounds);

                    if (!_isSnapping)
                    {
                        DrawHint(context, "Ctrl to snap");
                    }
                }
            }
        }

        // 8. Colour dropper readout
        if (vm is { IsPickingColor: true })
        {
            DrawColorPicker(context);
        }

        // 9. Drop feedback
        if (_isDropTarget && vm != null)
        {
            if (_dropTargetLayer != null)
            {
                var targetRect = GetLayerScreenRect(_dropTargetLayer, cardBounds);
                context.DrawRectangle(DropFill, DropPen, targetRect);
                DrawHint(context, _dropTargetLayer is PhotoLayer ? "Drop to set the fallback photo" : "Drop to replace this image");
            }
            else
            {
                context.DrawRectangle(DropFill, DropPen, new RoundedRect(cardBounds.Inflate(4), radius + 4));
                DrawHint(context, "Drop to add the image to the card");
            }
        }
    }

    /// <summary>
    /// The colour of the rendered card under a point on this control, or null when the point isn't on the card or
    /// the card hasn't been drawn yet. A small block is averaged rather than one pixel, so a lone compression
    /// artefact or a stray highlight in a photo can't decide the match.
    /// </summary>
    public Color? SampleCardColor(Point pos)
    {
        var bitmap = Bitmap;
        var cardBounds = GetCardBounds();
        if (bitmap == null || cardBounds.Width <= 0 || cardBounds.Height <= 0 || !cardBounds.Contains(pos))
        {
            return null;
        }

        // Only the 32-bit layouts are read directly; anything else is left to a future format rather than guessed at
        var format = bitmap.Format;
        bool bgra = format == PixelFormats.Bgra8888;
        if (!bgra && format != PixelFormats.Rgba8888)
        {
            return null;
        }

        var size = bitmap.PixelSize;
        int centreX = Math.Clamp((int)((pos.X - cardBounds.X) / cardBounds.Width * size.Width), 0, size.Width - 1);
        int centreY = Math.Clamp((int)((pos.Y - cardBounds.Y) / cardBounds.Height * size.Height), 0, size.Height - 1);

        int left = Math.Max(0, centreX - 1);
        int top = Math.Max(0, centreY - 1);
        int width = Math.Min(size.Width - 1, centreX + 1) - left + 1;
        int height = Math.Min(size.Height - 1, centreY + 1) - top + 1;

        int stride = width * 4;
        var pixels = new byte[stride * height];
        IntPtr buffer = Marshal.AllocHGlobal(pixels.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(left, top, width, height), buffer, pixels.Length, stride);
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ColourDropper] Couldn't read the card's pixels: {ex.Message}");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        long r = 0, g = 0, b = 0;
        int counted = 0;
        for (int i = 0; i + 3 < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3];
            if (alpha == 0)
            {
                continue; // Nothing drawn here; averaging transparent black would darken the match
            }

            byte c0 = pixels[i];
            byte c1 = pixels[i + 1];
            byte c2 = pixels[i + 2];
            (byte red, byte green, byte blue) = bgra ? (c2, c1, c0) : (c0, c1, c2);

            // Skia hands back premultiplied pixels; undo it so a semi-transparent area matches the colour it looks
            if (alpha < 255)
            {
                red = (byte)Math.Min(255, red * 255 / alpha);
                green = (byte)Math.Min(255, green * 255 / alpha);
                blue = (byte)Math.Min(255, blue * 255 / alpha);
            }

            r += red;
            g += green;
            b += blue;
            counted++;
        }

        if (counted == 0)
        {
            return null;
        }

        return Color.FromRgb((byte)(r / counted), (byte)(g / counted), (byte)(b / counted));
    }

    /// <summary>
    /// The dropper's readout: a ring of the colour under the pointer with its hex, so the match is confirmed before
    /// the click commits it.
    /// </summary>
    private void DrawColorPicker(DrawingContext context)
    {
        DrawHint(context, _pickedColor == null
            ? "Move over the card to match a colour  ·  Esc to cancel"
            : "Click to match this colour  ·  Esc to cancel");

        if (_pickedColor is not { } colour)
        {
            return;
        }

        // Cross-hair on the exact pixel the swatch is reporting
        context.DrawLine(PickerCrossPen, new Point(_pickedPoint.X - 8, _pickedPoint.Y), new Point(_pickedPoint.X + 8, _pickedPoint.Y));
        context.DrawLine(PickerCrossPen, new Point(_pickedPoint.X, _pickedPoint.Y - 8), new Point(_pickedPoint.X, _pickedPoint.Y + 8));

        string hex = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
        var label = CreateText(hex, 11, FontWeight.SemiBold, Brushes.White);

        const double swatch = 30.0;
        double chipWidth = swatch + 12 + label.Width + 10;
        double chipHeight = swatch + 10;

        // Beside the pointer, flipped to stay on screen near the edges
        double x = _pickedPoint.X + 14;
        double y = _pickedPoint.Y - chipHeight - 10;
        if (x + chipWidth > Bounds.Width) x = _pickedPoint.X - 14 - chipWidth;
        if (y < 0) y = _pickedPoint.Y + 14;

        var chip = new Rect(x, y, chipWidth, chipHeight);
        context.DrawRectangle(HintBackground, null, new RoundedRect(chip, 6));

        var well = new Rect(chip.X + 5, chip.Y + 5, swatch, swatch);
        context.DrawRectangle(new SolidColorBrush(colour), PickerEdgePen, new RoundedRect(well, 4));
        context.DrawRectangle(null, PickerRingPen, new RoundedRect(well.Deflate(1), 3));

        context.DrawText(label, new Point(well.Right + 10, chip.Center.Y - label.Height / 2));
    }

    /// <summary>
    /// The crop box: the layer's frame drawn as a crop rectangle with corner brackets and edge bars, thirds guides,
    /// the picture's full extent dashed behind it, and the rest of the card shaded so the box stands out.
    /// Dragging a bracket moves that edge of the crop; dragging inside moves the picture under it.
    /// </summary>
    private void DrawCropOverlay(DrawingContext context, Rect cardBounds, Rect box, Rect? contentRect, bool locked)
    {
        // Shade the card outside the box, as four bands around it
        var clipped = box.Intersect(cardBounds);
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            DrawShade(context, new Rect(cardBounds.X, cardBounds.Y, cardBounds.Width, clipped.Y - cardBounds.Y));
            DrawShade(context, new Rect(cardBounds.X, clipped.Bottom, cardBounds.Width, cardBounds.Bottom - clipped.Bottom));
            DrawShade(context, new Rect(cardBounds.X, clipped.Y, clipped.X - cardBounds.X, clipped.Height));
            DrawShade(context, new Rect(clipped.Right, clipped.Y, cardBounds.Right - clipped.Right, clipped.Height));
        }
        else
        {
            // The crop box is off the card entirely (a layer dragged past the edge): shade all of it
            DrawShade(context, cardBounds);
        }

        // How far the picture reaches, so it is clear how much room there is to pan and crop into
        if (contentRect is { } content)
        {
            context.DrawRectangle(null, ImageExtentPen, content);
        }

        // Thirds guides, the usual aid for placing a face in a badge photo
        for (int i = 1; i <= 2; i++)
        {
            double x = Math.Round(box.X + box.Width * i / 3.0) + 0.5;
            double y = Math.Round(box.Y + box.Height * i / 3.0) + 0.5;
            context.DrawLine(CropThirdsPen, new Point(x, box.Y), new Point(x, box.Bottom));
            context.DrawLine(CropThirdsPen, new Point(box.X, y), new Point(box.Right, y));
        }

        context.DrawRectangle(null, RepositionFramePen, box);

        if (!locked)
        {
            DrawCropHandles(context, box);
        }

        DrawHint(context, locked
            ? "This layer is locked  ·  Esc to finish"
            : "Drag an edge to crop  ·  Drag inside to move the picture  ·  Scroll to zoom  ·  Esc to finish");
    }

    private static void DrawShade(DrawingContext context, Rect rect)
    {
        if (rect.Width > 0 && rect.Height > 0)
        {
            context.DrawRectangle(CropShadeBrush, null, rect);
        }
    }

    /// <summary>
    /// Corner brackets and edge bars, drawn just inside the box so they never hide the pixels being cropped to.
    /// </summary>
    private static void DrawCropHandles(DrawingContext context, Rect box)
    {
        double arm = Math.Min(CropHandleLength, Math.Min(box.Width, box.Height) / 2.5);
        double t = CropHandleThickness;
        if (arm < t * 2)
        {
            return; // Too small a box to draw handles into without covering it
        }

        // Corners: an L in each, pointing along the two edges it controls
        foreach (var (originX, originY, signX, signY) in new[]
                 {
                     (box.X, box.Y, 1.0, 1.0),
                     (box.Right, box.Y, -1.0, 1.0),
                     (box.X, box.Bottom, 1.0, -1.0),
                     (box.Right, box.Bottom, -1.0, -1.0)
                 })
        {
            double left = signX > 0 ? originX : originX - arm;
            double top = signY > 0 ? originY : originY - t;
            context.DrawRectangle(CropHandleBrush, CropHandlePen, new Rect(left, top, arm, t));

            left = signX > 0 ? originX : originX - t;
            top = signY > 0 ? originY : originY - arm;
            context.DrawRectangle(CropHandleBrush, CropHandlePen, new Rect(left, top, t, arm));
        }

        // Edges: a short bar in the middle of each side
        double barX = Math.Min(CropHandleLength, box.Width / 3.0);
        double barY = Math.Min(CropHandleLength, box.Height / 3.0);
        context.DrawRectangle(CropHandleBrush, CropHandlePen, new Rect(box.Center.X - barX / 2, box.Y, barX, t));
        context.DrawRectangle(CropHandleBrush, CropHandlePen, new Rect(box.Center.X - barX / 2, box.Bottom - t, barX, t));
        context.DrawRectangle(CropHandleBrush, CropHandlePen, new Rect(box.X, box.Center.Y - barY / 2, t, barY));
        context.DrawRectangle(CropHandleBrush, CropHandlePen, new Rect(box.Right - t, box.Center.Y - barY / 2, t, barY));
    }

    /// <summary>
    /// Which part of the crop box the pointer is on: a corner ("NW", "NE", "SW", "SE") or an edge ("N", "S", "E",
    /// "W"). The grab area straddles the edge, so a handle can be caught from just outside the box too.
    /// </summary>
    public static string? HitTestCropHandles(Rect box, Point p)
    {
        const double r = CropHandleHitRadius;

        bool west = Math.Abs(p.X - box.X) <= r;
        bool east = Math.Abs(p.X - box.Right) <= r;
        bool north = Math.Abs(p.Y - box.Y) <= r;
        bool south = Math.Abs(p.Y - box.Bottom) <= r;

        bool withinX = p.X >= box.X - r && p.X <= box.Right + r;
        bool withinY = p.Y >= box.Y - r && p.Y <= box.Bottom + r;

        if (!withinX || !withinY)
        {
            return null;
        }

        if (north && west) return "NW";
        if (north && east) return "NE";
        if (south && west) return "SW";
        if (south && east) return "SE";
        if (north) return "N";
        if (south) return "S";
        if (west) return "W";
        if (east) return "E";

        return null;
    }

    private static void DrawHandle(DrawingContext context, Point pt)
    {
        var handleRect = new Rect(pt.X - HandleSize / 2, pt.Y - HandleSize / 2, HandleSize, HandleSize);
        context.DrawRectangle(Brushes.White, HandlePen, new RoundedRect(handleRect, 2));
    }

    private static void DrawImagePlaceholder(DrawingContext context, Rect rect, bool isPhoto)
    {
        using (context.PushClip(rect))
        {
            context.DrawRectangle(PlaceholderFill, PlaceholderPen, rect.Deflate(0.5));

            var title = CreateText(isPhoto ? "Photo" : "Image", 12, FontWeight.SemiBold, PlaceholderTitleBrush);
            var hint = CreateText("Double-click to choose", 10, FontWeight.Normal, PlaceholderHintBrush);

            bool showHint = rect.Width >= hint.Width + 8 && rect.Height >= title.Height + hint.Height + 8;
            if (rect.Width < title.Width + 4 || rect.Height < title.Height + 2)
            {
                return;
            }

            double totalHeight = title.Height + (showHint ? hint.Height : 0);
            double top = rect.Center.Y - totalHeight / 2;
            context.DrawText(title, new Point(rect.Center.X - title.Width / 2, top));
            if (showHint)
            {
                context.DrawText(hint, new Point(rect.Center.X - hint.Width / 2, top + title.Height));
            }
        }
    }

    private static void DrawTokenChip(DrawingContext context, Rect layerRect, TemplateLayer layer)
    {
        string label = string.Join("  ", layer.GetReferencedTokens().Distinct(StringComparer.OrdinalIgnoreCase).Select(TokenSyntax.Format));
        var text = CreateText(label, 10.5, FontWeight.Medium, TokenChipTextBrush);
        double chipHeight = Math.Round(text.Height + 4);
        var chip = new Rect(
            Math.Round(layerRect.Left),
            // Just above the frame, or just inside its top edge when the frame touches the top of the view
            Math.Round(layerRect.Top - chipHeight - 3 >= 2 ? layerRect.Top - chipHeight - 3 : layerRect.Top + 3),
            Math.Round(text.Width + 12),
            chipHeight);

        context.DrawRectangle(TokenChipFill, TokenChipPen, new RoundedRect(chip.Deflate(0.5), chipHeight / 2));
        context.DrawText(text, new Point(chip.X + 6, chip.Y + 2));
    }

    private void DrawHint(DrawingContext context, string message)
    {
        // Along the bottom edge: the printer/preview notice sits over the top of the canvas and would hide it
        var text = CreateText(message, 12, FontWeight.Medium, Brushes.White);
        double pillHeight = Math.Round(text.Height + 10);
        var pill = new Rect(
            Math.Round((Bounds.Width - text.Width) / 2 - 12), Math.Round(Math.Max(8, Bounds.Height - pillHeight - 8)),
            Math.Round(text.Width + 24), pillHeight);
        context.DrawRectangle(HintBackground, null, new RoundedRect(pill, pill.Height / 2));
        context.DrawText(text, new Point(pill.X + 12, pill.Y + 5));
    }

    private static FormattedText CreateText(string text, double size, FontWeight weight, IBrush brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);

    private Rect GetLayerScreenRect(TemplateLayer layer, Rect cardBounds) =>
        MmToScreen(new Rect(layer.X, layer.Y, layer.Width, layer.Height), cardBounds);

    private Rect MmToScreen(Rect mm, Rect cardBounds)
    {
        double scaleX = cardBounds.Width / Format.WidthMm;
        double scaleY = cardBounds.Height / Format.HeightMm;

        return new Rect(cardBounds.Left + mm.X * scaleX, cardBounds.Top + mm.Y * scaleY, mm.Width * scaleX, mm.Height * scaleY);
    }

    /// <summary>
    /// Visible layers, topmost first: the reverse of the order the renderer draws them in (ZIndex, then list order).
    /// </summary>
    private static IEnumerable<TemplateLayer> VisibleLayersTopFirst(MainWindowViewModel vm) =>
        vm.Template.Layers
            .Select((layer, index) => (layer, index))
            .Where(t => t.layer.IsVisible)
            .OrderByDescending(t => t.layer.ZIndex)
            .ThenByDescending(t => t.index)
            .Select(t => t.layer);

    // Hidden layers aren't drawn, so they must not catch clicks meant for the layers under them
    private TemplateLayer? HitTestLayer(MainWindowViewModel vm, Point pos, Rect cardBounds) =>
        VisibleLayersTopFirst(vm).FirstOrDefault(l => GetLayerScreenRect(l, cardBounds).Contains(pos));

    // A locked image can't be replaced by dropping a file on it; the drop adds a new image instead
    private TemplateLayer? HitTestImageLayer(MainWindowViewModel vm, Point pos, Rect cardBounds) =>
        VisibleLayersTopFirst(vm)
            .FirstOrDefault(l => l is IImageLayer && !l.IsLocked && GetLayerScreenRect(l, cardBounds).Contains(pos));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var vm = ViewModel;
        var point = e.GetCurrentPoint(this);
        if (vm == null) return;

        // Presses inside the in-place text editor belong to it (caret placement, selecting words)
        if (IsFromTextEditor(e.Source))
        {
            return;
        }

        // Middle-drag moves the view, so every part of a zoomed-in card can be reached
        if (point.Properties.IsMiddleButtonPressed && !IsInPointerGesture)
        {
            Focus();
            _isPanningView = true;
            _lastPointerPos = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // Colour dropper: the click belongs to the pick, not to selecting or dragging a layer
        if (vm.IsPickingColor)
        {
            var pickPoint = e.GetPosition(this);
            if (point.Properties.IsLeftButtonPressed && SampleCardColor(pickPoint) is { } picked)
            {
                vm.ApplyPickedColor($"#{picked.R:X2}{picked.G:X2}{picked.B:X2}");
            }
            else
            {
                // A click off the card, or with any other button, means "never mind"
                vm.IsPickingColor = false;
            }

            _pickedColor = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed || IsInPointerGesture) return;

        Focus();

        var pos = e.GetPosition(this);
        var cardBounds = GetCardBounds();

        // Crop mode: the crop box's handles resize the crop, dragging inside moves the picture under it, and
        // clicking well away from it finishes
        if (vm.IsRepositioningImage && vm.SelectedLayer != null)
        {
            var cropBox = GetLayerScreenRect(vm.SelectedLayer, cardBounds);

            if (e.ClickCount >= 2 && cropBox.Contains(pos))
            {
                vm.IsRepositioningImage = false;
                return;
            }

            // Tested before the box itself: handles straddle its edge, so the outer half must still grab
            string? cropHandle = vm.SelectedLayer.IsLocked ? null : HitTestCropHandles(cropBox, pos);
            if (cropHandle != null)
            {
                _activeCropHandle = cropHandle;
                _dragStartPointerPos = pos;
                _cropStartFrameMm = new Rect(vm.SelectedLayer.X, vm.SelectedLayer.Y, vm.SelectedLayer.Width, vm.SelectedLayer.Height);
                _cropStartContentMm = vm.GetSelectedImageContentRectMm() ?? _cropStartFrameMm;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            if (cropBox.Contains(pos))
            {
                _isPanningImage = true;
                _lastPointerPos = pos;
                e.Pointer.Capture(this);
                return;
            }

            vm.IsRepositioningImage = false;
        }

        // Check if clicked inside selected layer resize handles (locked layers can be selected but not resized)
        if (vm.SelectedLayer != null && !vm.SelectedLayer.IsLocked)
        {
            var layerRect = GetLayerScreenRect(vm.SelectedLayer, cardBounds);
            string? handle = HitTestHandles(layerRect, pos);
            if (handle != null)
            {
                _isResizing = true;
                _activeResizeHandle = handle;
                _dragStartPointerPos = pos;
                _resizeStartRectMm = new Rect(vm.SelectedLayer.X, vm.SelectedLayer.Y, vm.SelectedLayer.Width, vm.SelectedLayer.Height);
                e.Pointer.Capture(this);
                return;
            }
        }

        // Hit-test layers in reverse Z-order (topmost first)
        var hitLayer = HitTestLayer(vm, pos, cardBounds);

        if (hitLayer != null)
        {
            vm.SelectedLayer = hitLayer;

            // A locked layer can be selected and inspected, but not dragged or repositioned
            if (hitLayer.IsLocked)
            {
                InvalidateVisual();
                return;
            }

            // Double-click on text: edit it right here on the card
            if (e.ClickCount >= 2 && hitLayer is TextLayer textLayer)
            {
                if (vm.BeginInlineTextEdit(textLayer))
                {
                    e.Handled = true;
                }

                InvalidateVisual();
                return;
            }

            if (e.ClickCount >= 2 && hitLayer is IImageLayer)
            {
                if (vm.LayerHasImage(hitLayer))
                {
                    vm.IsRepositioningImage = true;
                }
                else
                {
                    ChooseImageRequested?.Invoke(this, EventArgs.Empty);
                }

                InvalidateVisual();
                return;
            }

            _isDragging = true;
            _lastPointerPos = pos;
            _dragStartPointerPos = pos;
            _dragStartLayerMm = new Point(hitLayer.X, hitLayer.Y);
            _isSnapping = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            e.Pointer.Capture(this);
            InvalidateVisual();
        }
        else if (cardBounds.Contains(pos))
        {
            // Clicked empty card space
            vm.SelectedLayer = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var vm = ViewModel;
        if (vm == null) return;

        var pos = e.GetPosition(this);
        var cardBounds = GetCardBounds();

        if (vm.IsPickingColor)
        {
            // Repainted on every move: the cross-hair and its swatch follow the pointer
            _pickedColor = SampleCardColor(pos);
            _pickedPoint = pos;
            Cursor = new Cursor(StandardCursorType.Cross);
            InvalidateVisual();
            return;
        }

        if (_isPanningView)
        {
            PanView(pos - _lastPointerPos);
            _lastPointerPos = pos;
            return;
        }

        if (!_isDragging && !_isResizing && !_isPanningImage && _activeCropHandle == null)
        {
            if (!IsFromTextEditor(e.Source))
            {
                UpdateHoverCursor(vm, pos, cardBounds);
            }
            return;
        }

        if (vm.SelectedLayer == null) return;

        double pixelsPerMmx = cardBounds.Width / Format.WidthMm;
        double pixelsPerMmy = cardBounds.Height / Format.HeightMm;

        double deltaMmX = (pos.X - _lastPointerPos.X) / pixelsPerMmx;
        double deltaMmY = (pos.Y - _lastPointerPos.Y) / pixelsPerMmy;

        if (_activeCropHandle != null)
        {
            CropFromHandle(vm, _activeCropHandle, pos, cardBounds, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            InvalidateVisual();
        }
        else if (_isPanningImage)
        {
            if (Math.Abs(deltaMmX) >= 0.05 || Math.Abs(deltaMmY) >= 0.05)
            {
                vm.PanSelectedImage(deltaMmX, deltaMmY);
                _lastPointerPos = pos;
                InvalidateVisual();
            }
        }
        else if (_isDragging)
        {
            DragSelectedLayer(vm, pos, cardBounds, e.KeyModifiers.HasFlag(KeyModifiers.Control));
        }
        else if (_isResizing && _activeResizeHandle != null)
        {
            ResizeFromHandle(vm, _activeResizeHandle, pos, cardBounds);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (IsInPointerGesture)
        {
            e.Pointer.Capture(null);
            EndPointerGesture();
        }
    }

    /// <summary>
    /// Losing the pointer mid-gesture (switching windows, a dialog opening) would otherwise leave the canvas
    /// dragging the layer on the next mouse move with no button held.
    /// </summary>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPointerGesture();
    }

    private void EndPointerGesture()
    {
        if (!IsInPointerGesture)
        {
            return;
        }

        bool editedLayer = _isDragging || _isResizing || _isPanningImage || _activeCropHandle != null;
        _isDragging = false;
        _isResizing = false;
        _isPanningImage = false;
        _isPanningView = false;
        _activeResizeHandle = null;
        _activeCropHandle = null;
        _isSnapping = false;
        _snapGuideXmm = null;
        _snapGuideYmm = null;
        if (editedLayer)
        {
            // The whole drag is one undo step; letting go ends it
            ViewModel?.EndHistoryGesture();
            ViewModel?.RequestLivePreviewUpdate();
        }
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ViewModel is { IsRepositioningImage: true } vm && e.Delta.Y != 0)
        {
            vm.ZoomSelectedImage(Math.Pow(1.1, e.Delta.Y));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Delta.Y != 0)
        {
            // Same range and steps as the zoom buttons
            Zoom = Math.Clamp(Math.Round(Zoom + Math.Sign(e.Delta.Y) * 0.15, 2), 0.4, 3.0);
            e.Handled = true;
            return;
        }

        // Scroll a zoomed-in card; Shift scrolls sideways
        const double scrollStep = 40.0;
        var delta = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? new Vector(e.Delta.Y * scrollStep, 0)
            : new Vector(e.Delta.X * scrollStep, e.Delta.Y * scrollStep);
        if (delta != default)
        {
            PanView(delta);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ViewModel is { IsPickingColor: true } picking && e.Key is Key.Escape)
        {
            picking.IsPickingColor = false;
            e.Handled = true;
            return;
        }

        if (ViewModel is { IsRepositioningImage: true } vm && e.Key is Key.Escape or Key.Enter)
        {
            vm.IsRepositioningImage = false;
            e.Handled = true;
        }
    }

    // --- In-place text editing ---

    /// <summary>
    /// The editor shown over a text layer while it is edited in place (exposed for UI automation).
    /// </summary>
    public InlineTextEditor InlineEditor => _textEditor;

    /// <summary>
    /// A layer's frame in this control's coordinates.
    /// </summary>
    public Rect GetLayerBounds(TemplateLayer layer) => GetLayerScreenRect(layer, GetCardBounds());

    private bool IsFromTextEditor(object? source) =>
        _textEditor.IsVisible && source is Visual visual &&
        (ReferenceEquals(visual, _textEditor) || _textEditor.IsVisualAncestorOf(visual));

    /// <summary>
    /// Lifecycle hook for the edit session: shows, restyles or hides the editor to match the view model.
    /// </summary>
    private void SyncTextEditor()
    {
        var vm = ViewModel;
        var layer = vm?.InlineEditingLayer;
        if (vm == null || layer == null)
        {
            HideTextEditor();
            return;
        }

        if (_textEditor.IsVisible && _textEditor.Layer?.Id == layer.Id)
        {
            // Same session, restyled from the properties panel
            _textEditor.UpdateLayer(layer);
            InvalidateArrange();
            return;
        }

        _textEditor.Load(layer, vm.InlineEditingText);
        _textEditor.IsVisible = true;
        AttachOutsidePressHandler();
        InvalidateArrange();
        InvalidateVisual();

        // Focus once the editor has been arranged over the layer; with everything selected, typing replaces the text
        string layerId = layer.Id;
        Dispatcher.UIThread.Post(() =>
        {
            if (_textEditor.IsVisible && _textEditor.Layer?.Id == layerId)
            {
                _textEditor.Focus();
                _textEditor.SelectAll();
            }
        }, DispatcherPriority.Loaded);
    }

    private void HideTextEditor()
    {
        if (!_textEditor.IsVisible)
        {
            return;
        }

        _isHidingTextEditor = true;
        try
        {
            bool hadFocus = _textEditor.IsKeyboardFocusWithin;
            DetachOutsidePressHandler();
            _textEditor.IsVisible = false;
            _textEditor.Unload();

            // Keep the keyboard on the canvas (Esc, shortcuts) rather than dropping focus to the window
            if (hadFocus)
            {
                Focus();
            }
        }
        finally
        {
            _isHidingTextEditor = false;
        }

        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_textEditor.IsVisible)
        {
            AttachOutsidePressHandler();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachOutsidePressHandler();
    }

    /// <summary>
    /// While text is being edited, a press anywhere else in the window commits it. Focus loss alone isn't enough:
    /// pressing somewhere that can't take focus (the backdrop around the card, a panel background) leaves focus in
    /// the editor. Tunnelling, and including handled events, sees the press before any control acts on it.
    /// </summary>
    private void AttachOutsidePressHandler()
    {
        var root = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(root, _outsidePressRoot))
        {
            return;
        }

        DetachOutsidePressHandler();
        _outsidePressRoot = root;
        root?.AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachOutsidePressHandler()
    {
        _outsidePressRoot?.RemoveHandler(PointerPressedEvent, OnWindowPointerPressed);
        _outsidePressRoot = null;
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsFromTextEditor(e.Source) && ViewModel is { IsInlineTextEditing: true } vm)
        {
            vm.CommitInlineTextEdit();
        }
    }

    private void OnTextEditorCommitRequested(object? sender, EventArgs e) => ViewModel?.CommitInlineTextEdit();

    private void OnTextEditorCancelRequested(object? sender, EventArgs e) => ViewModel?.CancelInlineTextEdit();

    // Clicking anywhere outside the editor keeps what was typed, like leaving a spreadsheet cell
    private void OnTextEditorLostFocus()
    {
        if (!_isHidingTextEditor && _textEditor.IsVisible && ViewModel is { IsInlineTextEditing: true } vm)
        {
            vm.CommitInlineTextEdit();
        }
    }

    private void OnTextEditorTextEdited(object? sender, EventArgs e)
    {
        if (ViewModel is { IsInlineTextEditing: true } vm)
        {
            vm.InlineEditingText = _textEditor.Text ?? string.Empty;

            // Shrink-to-fit size and wrapped height depend on the text
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The canvas takes whatever space it's given. The editor is measured against its layer frame in ArrangeOverride.
        return new Size();
    }

    /// <summary>
    /// Layout hook: places the editor exactly over its layer frame at the current zoom and pan.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_textEditor.IsVisible && _textEditor.Layer is { } layer)
        {
            var cardBounds = GetCardBounds(finalSize);
            var frame = GetLayerScreenRect(layer, cardBounds);
            _textEditor.ApplyGeometry(Format, cardBounds.Height / Format.HeightMm);

            // Wrapped text grows past a short frame instead of scrolling; single-line modes keep the frame width and
            // scroll to the caret. Either way the box is centred on the frame, like the printed block of lines.
            bool wraps = layer.Overflow == TextOverflowMode.Wrap;

            // Text that exactly fills the frame when printed can measure a hair wider here, and the caret needs a
            // pixel too; without slack the box scrolls its first character out of view. The slack goes on the side
            // the text grows toward, so the glyphs themselves don't move.
            double slack = wraps ? 0 : CaretSlack;
            double left = frame.X - layer.Alignment switch
            {
                LayerTextAlignment.Right => slack,
                LayerTextAlignment.Center => slack / 2,
                _ => 0
            };
            double width = Math.Max(1, frame.Width) + slack;

            _textEditor.Measure(new Size(width, double.PositiveInfinity));
            double height = wraps
                ? Math.Max(frame.Height, _textEditor.DesiredSize.Height)
                : Math.Max(Math.Max(1, frame.Height), Math.Min(_textEditor.DesiredSize.Height, frame.Height * 2));
            _textEditor.Arrange(new Rect(left, frame.Center.Y - height / 2, width, height));
        }
        else
        {
            _textEditor.Arrange(default);
        }

        return finalSize;
    }

    // --- Drag and drop image files ---

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null || !e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;

        var target = HitTestImageLayer(vm, e.GetPosition(this), GetCardBounds());
        if (!_isDropTarget || !ReferenceEquals(target, _dropTargetLayer))
        {
            _isDropTarget = true;
            _dropTargetLayer = target;
            InvalidateVisual();
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => ClearDropTarget();

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var vm = ViewModel;
        var pos = e.GetPosition(this);
        var cardBounds = GetCardBounds();
        ClearDropTarget();

        if (vm == null)
        {
            return;
        }

        e.Handled = true;

        var files = e.DataTransfer.TryGetFiles();
        string? path = files?
            .Select(f => f.TryGetLocalPath())
            .FirstOrDefault(p => p != null && ImageSourceLoader.HasSupportedExtension(p));

        if (path == null)
        {
            if (files != null && files.Any())
            {
                vm.StatusText = $"Only {ImageSourceLoader.SupportedFormatsDescription} images can be dropped on the card.";
            }
            return;
        }

        // Async event handler: an exception escaping here would end the app
        try
        {
            var target = HitTestImageLayer(vm, pos, cardBounds);
            if (target != null)
            {
                vm.SelectedLayer = target;
                await vm.SetSelectedLayerImageFromFileAsync(path);
            }
            else
            {
                double xMm = (pos.X - cardBounds.Left) * Format.WidthMm / cardBounds.Width;
                double yMm = (pos.Y - cardBounds.Top) * Format.HeightMm / cardBounds.Height;
                await vm.AddImageLayerFromFileAsync(path, xMm, yMm);
            }
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Couldn't use {System.IO.Path.GetFileName(path)}: {ex.Message}";
        }

        InvalidateVisual();
    }

    private void ClearDropTarget()
    {
        if (_isDropTarget)
        {
            _isDropTarget = false;
            _dropTargetLayer = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Moves the dragged layer to follow the pointer from where the drag started. Holding Ctrl locks it onto
    /// the card centre and edges, the safe area, or other layers, and records the guides it locked to.
    /// </summary>
    private void DragSelectedLayer(MainWindowViewModel vm, Point pos, Rect cardBounds, bool snap)
    {
        var layer = vm.SelectedLayer!;
        double pixelsPerMmX = cardBounds.Width / Format.WidthMm;
        double pixelsPerMmY = cardBounds.Height / Format.HeightMm;

        double x = _dragStartLayerMm.X + (pos.X - _dragStartPointerPos.X) / pixelsPerMmX;
        double y = _dragStartLayerMm.Y + (pos.Y - _dragStartPointerPos.Y) / pixelsPerMmY;

        _isSnapping = snap;
        _snapGuideXmm = null;
        _snapGuideYmm = null;

        if (snap)
        {
            var others = vm.Template.Layers.Where(l => l.Id != layer.Id && l.IsVisible);
            var result = LayerSnapper.Snap(x, y, layer.Width, layer.Height,
                Format.WidthMm, Format.HeightMm, Format.SafeMarginMm, others, SnapDistancePx / pixelsPerMmX);

            x = result.X;
            y = result.Y;
            _snapGuideXmm = result.GuideXmm;
            _snapGuideYmm = result.GuideYmm;
        }
        else
        {
            x = Math.Round(x, 1);
            y = Math.Round(y, 1);
        }

        vm.SetSelectedLayerPosition(x, y);
        InvalidateVisual();
    }

    private void DrawSnapGuides(DrawingContext context, Rect cardBounds)
    {
        const double overhang = 12.0;

        if (_snapGuideXmm is { } guideX)
        {
            double sx = Math.Round(cardBounds.X + guideX * cardBounds.Width / Format.WidthMm) + 0.5;
            context.DrawLine(SnapGuidePen, new Point(sx, cardBounds.Top - overhang), new Point(sx, cardBounds.Bottom + overhang));
        }

        if (_snapGuideYmm is { } guideY)
        {
            double sy = Math.Round(cardBounds.Y + guideY * cardBounds.Height / Format.HeightMm) + 0.5;
            context.DrawLine(SnapGuidePen, new Point(cardBounds.Left - overhang, sy), new Point(cardBounds.Right + overhang, sy));
        }
    }

    /// <summary>
    /// Resizes the selected layer so the dragged corner follows the pointer from where the resize started, while the
    /// opposite corner stays exactly where it was. Measuring from the start (rather than adding up small per-move
    /// steps) keeps the corner under the pointer and stops the anchored edges drifting from rounding.
    /// </summary>
    private void ResizeFromHandle(MainWindowViewModel vm, string handle, Point pos, Rect cardBounds)
    {
        double minSize = MainWindowViewModel.MinLayerSizeMm;
        var start = _resizeStartRectMm;
        double dx = (pos.X - _dragStartPointerPos.X) * Format.WidthMm / cardBounds.Width;
        double dy = (pos.Y - _dragStartPointerPos.Y) * Format.HeightMm / cardBounds.Height;

        double left = start.Left, top = start.Top, right = start.Right, bottom = start.Bottom;

        // The moving edge stops at the card's left/top edge and at the minimum size
        if (handle.EndsWith('W'))
            left = Math.Max(0, Math.Min(Math.Round(start.Left + dx, 1), right - minSize));
        else
            right = Math.Max(Math.Round(start.Right + dx, 1), left + minSize);

        if (handle.StartsWith('N'))
            top = Math.Max(0, Math.Min(Math.Round(start.Top + dy, 1), bottom - minSize));
        else
            bottom = Math.Max(Math.Round(start.Bottom + dy, 1), top + minSize);

        vm.SetSelectedLayerBounds(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Moves one edge or corner of the crop box, keeping the opposite side pinned, and tells the view model to leave
    /// the picture where it is on the card. Holding Shift keeps the box's starting shape.
    /// </summary>
    private void CropFromHandle(MainWindowViewModel vm, string handle, Point pos, Rect cardBounds, bool keepAspect)
    {
        double minSize = MainWindowViewModel.MinLayerSizeMm;
        var start = _cropStartFrameMm;
        double dx = (pos.X - _dragStartPointerPos.X) * Format.WidthMm / cardBounds.Width;
        double dy = (pos.Y - _dragStartPointerPos.Y) * Format.HeightMm / cardBounds.Height;

        double left = start.Left, top = start.Top, right = start.Right, bottom = start.Bottom;

        if (handle.Contains('W'))
            left = Math.Max(0, Math.Min(start.Left + dx, right - minSize));
        else if (handle.Contains('E'))
            right = Math.Max(start.Right + dx, left + minSize);

        if (handle.Contains('N'))
            top = Math.Max(0, Math.Min(start.Top + dy, bottom - minSize));
        else if (handle.Contains('S'))
            bottom = Math.Max(start.Bottom + dy, top + minSize);

        double width = right - left;
        double height = bottom - top;

        if (keepAspect && start.Width > 0 && start.Height > 0)
        {
            // Match the larger change so the corner still follows the pointer, then pull the pinned edge back
            double aspect = start.Width / start.Height;
            if (width / aspect >= height)
            {
                height = Math.Max(minSize, width / aspect);
            }
            else
            {
                width = Math.Max(minSize, height * aspect);
            }

            if (handle.Contains('W')) left = right - width; else right = left + width;
            if (handle.Contains('N')) top = bottom - height; else bottom = top + height;

            left = Math.Max(0, left);
            top = Math.Max(0, top);
        }

        vm.SetSelectedImageCropFrame(
            new Rect(left, top, Math.Max(minSize, right - left), Math.Max(minSize, bottom - top)),
            _cropStartContentMm);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverLayerId != null || _pickedColor != null)
        {
            _hoverLayerId = null;
            _pickedColor = null;
            InvalidateVisual();
        }
    }

    private void UpdateHoverCursor(MainWindowViewModel vm, Point pos, Rect cardBounds)
    {
        StandardCursorType cursor = StandardCursorType.Arrow;

        var hovered = HitTestLayer(vm, pos, cardBounds);
        if (hovered?.Id != _hoverLayerId)
        {
            _hoverLayerId = hovered?.Id;
            InvalidateVisual();
        }

        if (vm.IsRepositioningImage && vm.SelectedLayer != null)
        {
            var cropBox = GetLayerScreenRect(vm.SelectedLayer, cardBounds);
            string? cropHandle = vm.SelectedLayer.IsLocked ? null : HitTestCropHandles(cropBox, pos);
            cursor = cropHandle switch
            {
                "NW" => StandardCursorType.TopLeftCorner,
                "NE" => StandardCursorType.TopRightCorner,
                "SW" => StandardCursorType.BottomLeftCorner,
                "SE" => StandardCursorType.BottomRightCorner,
                "N" => StandardCursorType.TopSide,
                "S" => StandardCursorType.BottomSide,
                "W" => StandardCursorType.LeftSide,
                "E" => StandardCursorType.RightSide,
                _ => cropBox.Contains(pos) ? StandardCursorType.Hand : cursor
            };
        }
        else
        {
            if (vm.SelectedLayer != null)
            {
                cursor = HitTestHandles(GetLayerScreenRect(vm.SelectedLayer, cardBounds), pos) switch
                {
                    "NW" => StandardCursorType.TopLeftCorner,
                    "NE" => StandardCursorType.TopRightCorner,
                    "SW" => StandardCursorType.BottomLeftCorner,
                    "SE" => StandardCursorType.BottomRightCorner,
                    _ => cursor
                };
            }

            // Locked layers can be selected but not dragged, so don't show the move cursor over them
            if (cursor == StandardCursorType.Arrow && hovered is { IsLocked: false })
            {
                cursor = StandardCursorType.SizeAll;
            }
        }

        Cursor = new Cursor(cursor);
    }

    private static string? HitTestHandles(Rect r, Point p)
    {
        const double handleRadius = HandleHitRadius;

        if (new Rect(r.Right - handleRadius, r.Bottom - handleRadius, handleRadius * 2, handleRadius * 2).Contains(p))
            return "SE";
        if (new Rect(r.Right - handleRadius, r.Top - handleRadius, handleRadius * 2, handleRadius * 2).Contains(p))
            return "NE";
        if (new Rect(r.Left - handleRadius, r.Bottom - handleRadius, handleRadius * 2, handleRadius * 2).Contains(p))
            return "SW";
        if (new Rect(r.Left - handleRadius, r.Top - handleRadius, handleRadius * 2, handleRadius * 2).Contains(p))
            return "NW";

        return null;
    }
}
