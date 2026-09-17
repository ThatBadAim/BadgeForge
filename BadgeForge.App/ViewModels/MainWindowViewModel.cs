using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using BadgeForge.Core.Data.Validation;
using BadgeForge.Core.Printing.Batch;
using BadgeForge.Core.Printing.Drivers;
using BadgeForge.Core.Printing.Interfaces;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Rendering;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using BadgeForge.Core.Templates.Storage;
using SkiaSharp;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// Preset color offered in the text layer color palette.
/// </summary>
public record TextColorSwatch(string Name, string Hex)
{
    public Avalonia.Media.IBrush Brush { get; } = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(Hex));
}

/// <summary>
/// Column mapping item for connecting CSV header fields to template tokens.
/// </summary>
public class ColumnMappingItem : ViewModelBase
{
    public string TemplateToken { get; init; } = string.Empty;
    private string _csvColumn = string.Empty;
    public string CsvColumn
    {
        get => _csvColumn;
        set => SetProperty(ref _csvColumn, value);
    }
}

/// <summary>
/// Presentation coordinator for the BadgeForge single-window dashboard.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Printer list entry for the built-in printer simulator.
    /// </summary>
    public const string MockPrinterName = "Mock IDP SMART-31 DTC";

    private readonly JsonTemplateStorage _templateStorage = new();
    private readonly CardRenderer _renderer = new();
    private readonly CardRenderer _printRenderer = new() { StrictLayerRendering = true };
    private readonly RosterImportService _rosterImportService = new();
    private readonly PhotoMatchingService _photoService = new();
    private readonly PreFlightValidationService _preFlightService = new();
    private readonly BatchPrintEngine _batchEngine = new();
    private readonly RosterService _rosterService = new();

    private TemplateDefinition _template = null!;
    private TemplateLayer? _selectedLayer;
    private CardFormat _activeFormat = CardFormat.CR80;
    private bool _isPortrait = false;
    private AvaloniaBitmap? _previewBitmap;

    private List<BadgeRecord> _records = new();
    private int _currentRecordIndex = 0;
    private string _csvFilePath = string.Empty;
    private string _photoFolderPath = string.Empty;
    private string _statusText = "Ready";

    private ICardPrinterDriver _activeDriver;
    private string _selectedPrinter = MockPrinterName;
    private BatchExecutionState _batchState = BatchExecutionState.Idle;
    private bool _hopperPausePromptVisible;
    private string _hopperPauseMessage = string.Empty;
    private bool _stopPromptVisible;
    private string _stopPromptMessage = string.Empty;
    private bool _errorPromptVisible;
    private string _errorPromptMessage = string.Empty;
    private int _batchProgressPercent = 0;

    private long _renderRevision = 0;
    private long _compatibilityRevision = 0;
    private CancellationTokenSource? _batchCts;
    private bool _batchInFlight;
    private int _batchGeneration;
    private string _errorPromptTitle = string.Empty;
    private ErrorPromptKind _errorPromptKind = ErrorPromptKind.Dismiss;
    private string _printerCompatibilityMessage = string.Empty;
    private string _previewErrorText = string.Empty;
    private string _savedTemplateJson = string.Empty;

    // Photos the user picked for a person themselves; matching a photo folder never replaces these
    private readonly HashSet<BadgeRecord> _manualPhotoRecords = new(ReferenceEqualityComparer.Instance);

    // CSV columns the user mapped template tokens to, remembered while the tokens come and go
    private readonly Dictionary<string, string> _mappingMemory = new(StringComparer.OrdinalIgnoreCase);

    // Live preview: one render at a time, always of the newest request
    private readonly object _previewLock = new();
    private PreviewRequest? _pendingPreview;
    private bool _previewWorkerRunning;

    private sealed record PreviewRequest(long Revision, TemplateDefinition Template, IReadOnlyDictionary<string, string> FieldData);

    private bool _isReplacingLayer;
    private bool _isRepositioningImage;
    private bool _isPickingColor;
    private string? _thumbnailSource;
    private AvaloniaBitmap? _selectedImageThumbnail;

    private PersonItem? _selectedPerson;
    private bool _isPeoplePageActive;
    private string? _peopleColumnsSignature;

    // Where the user dragged each People column. Display order only — it is never written back to a record or a
    // template, so reordering the grid can't rename a field or break a badge layer's binding
    private readonly PeopleColumnLayout _columnLayout = new();
    private readonly Dictionary<int, PersonItem> _printQueue = new();

    public ObservableCollection<TemplateLayer> Layers { get; } = new();
    public ObservableCollection<ColumnMappingItem> ColumnMappings { get; } = new();
    /// <summary>
    /// Everyone on the print list, in print order. Mirrors the loaded records one-to-one.
    /// </summary>
    public ObservableCollection<PersonItem> People { get; } = new();
    public ObservableCollection<PeopleFieldColumn> PeopleFieldColumns { get; } = new();
    public ObservableCollection<string> AvailablePrinters { get; } = new();
    public ObservableCollection<CardFormat> AvailableFormats { get; } = new();

    private static readonly string[] StandardFontWeights = { "Light", "Normal", "Medium", "SemiBold", "Bold", "Black" };

    public IReadOnlyList<string> FontWeightOptions { get; } = StandardFontWeights;

    public IReadOnlyList<string> FontFamilyOptions { get; } = BundledFonts.AvailableFamilies;

    /// <summary>
    /// The bundled font families, plus the selected layer's own family when a loaded template uses another one.
    /// A live collection: replacing the whole list would make the combo box drop or rewrite its selection.
    /// </summary>
    public ObservableCollection<string> TextFontFamilyOptions { get; } = new(BundledFonts.AvailableFamilies);

    public ObservableCollection<string> TextFontWeightOptions { get; } = new(StandardFontWeights);

    public IReadOnlyList<TextColorSwatch> TextColorOptions { get; } = new[]
    {
        new TextColorSwatch("Black", "#000000"),
        new TextColorSwatch("White", "#FFFFFF"),
        new TextColorSwatch("Charcoal", "#1D2624"),
        new TextColorSwatch("Grey Olive", "#7A918D"),
        new TextColorSwatch("Muted Teal", "#93B1A7"),
        new TextColorSwatch("Tea Green", "#C5EDAC"),
        new TextColorSwatch("Navy", "#1F3A5F"),
        new TextColorSwatch("Red", "#B42318"),
    };

    public TemplateDefinition Template
    {
        get => _template;
        private set => SetProperty(ref _template, value);
    }

    public TemplateLayer? SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (_isReplacingLayer && value == null)
            {
                // Transient ListBox deselect while the layer list is rebuilt or a layer is swapped for its updated copy
                return;
            }

            // Layers are records, so compare by reference: an updated copy with equal values is still a new selection
            if (ReferenceEquals(_selectedLayer, value))
            {
                return;
            }

            // Moving the selection off text being edited on the canvas keeps what was typed. The layer list can
            // change the selection before the editor notices it lost focus, so this can't be left to the editor.
            if (!_isReplacingLayer && _inlineEditingLayer != null && value?.Id != _inlineEditingLayer.Id)
            {
                CommitInlineTextEdit();
            }

            var previous = _selectedLayer;
            _selectedLayer = value;

            // While the editors are pointed at the new layer, some write their old or coerced value back (a font
            // combo box keeps the previous layer's font while its items change, a number box clamps to its range).
            // Those writes aren't edits, and would land on the newly selected layer.
            bool wasAnnouncing = _isAnnouncingSelection;
            _isAnnouncingSelection = true;
            try
            {
                RaiseSelectedLayerChanged(previous, value);
            }
            finally
            {
                _isAnnouncingSelection = wasAnnouncing;
            }
        }
    }

    private bool _isAnnouncingSelection;

    private void RaiseSelectedLayerChanged(TemplateLayer? previous, TemplateLayer? value)
    {
        OnPropertyChanged(nameof(SelectedLayer));

        if (!_isReplacingLayer && value?.Id != previous?.Id)
        {
            IsRepositioningImage = false;
            IsPickingColor = false;
        }

        OnPropertyChanged(nameof(IsLayerSelected));
        OnPropertyChanged(nameof(IsTextLayerSelected));
        OnPropertyChanged(nameof(IsPhotoLayerSelected));
        OnPropertyChanged(nameof(IsBarcodeLayerSelected));
        OnPropertyChanged(nameof(IsLinearBarcodeSelected));
        OnPropertyChanged(nameof(IsStaticImageLayerSelected));
        OnPropertyChanged(nameof(SelectedTextLayer));
        OnPropertyChanged(nameof(SelectedPhotoLayer));
        OnPropertyChanged(nameof(SelectedBarcodeLayer));
        OnPropertyChanged(nameof(SelectedStaticImageLayer));
        OnPropertyChanged(nameof(SelectedLayerTypeLabel));
        OnPropertyChanged(nameof(LayerX));
        OnPropertyChanged(nameof(LayerY));
        OnPropertyChanged(nameof(LayerWidth));
        OnPropertyChanged(nameof(LayerHeight));
        OnPropertyChanged(nameof(TextContent));
        // Offer the layer's own font before announcing it, so the combo boxes can find it among their items
        AddOptionIfMissing(TextFontFamilyOptions, SelectedTextLayer?.FontFamily);
        AddOptionIfMissing(TextFontWeightOptions, SelectedTextLayer?.FontWeight);
        OnPropertyChanged(nameof(TextFontFamily));
        OnPropertyChanged(nameof(TextFontWeight));
        RemoveUnusedOptions(TextFontFamilyOptions, FontFamilyOptions, SelectedTextLayer?.FontFamily);
        RemoveUnusedOptions(TextFontWeightOptions, FontWeightOptions, SelectedTextLayer?.FontWeight);
        OnPropertyChanged(nameof(TextFontSize));
        OnPropertyChanged(nameof(TextColorHex));
        OnPropertyChanged(nameof(TextColorBrush));
        OnPropertyChanged(nameof(CanPickColor));
        OnPropertyChanged(nameof(TextPrintsAsPureBlack));
        OnPropertyChanged(nameof(IsTextAlignLeft));
        OnPropertyChanged(nameof(IsTextAlignCenter));
        OnPropertyChanged(nameof(IsTextAlignRight));
        OnPropertyChanged(nameof(TextOverflowIndex));
        OnPropertyChanged(nameof(SelectedLayerTokensText));
        OnPropertyChanged(nameof(HasSelectedLayerTokens));
        OnPropertyChanged(nameof(PhotoSourceToken));
        OnPropertyChanged(nameof(BarcodeContent));
        OnPropertyChanged(nameof(BarcodeIncludeText));
        OnPropertyChanged(nameof(BarcodePrintsAsPureBlack));
        RaiseSelectedImageProperties();
    }

    public bool IsLayerSelected => SelectedLayer != null;
    public bool IsTextLayerSelected => SelectedLayer is TextLayer;
    public bool IsPhotoLayerSelected => SelectedLayer is PhotoLayer;
    public bool IsBarcodeLayerSelected => SelectedLayer is BarcodeLayer;
    public bool IsLinearBarcodeSelected => SelectedLayer is BarcodeLayer { Symbology: not BarcodeSymbology.QrCode };
    public bool IsStaticImageLayerSelected => SelectedLayer is StaticImageLayer;

    public TextLayer? SelectedTextLayer => SelectedLayer as TextLayer;
    public PhotoLayer? SelectedPhotoLayer => SelectedLayer as PhotoLayer;
    public BarcodeLayer? SelectedBarcodeLayer => SelectedLayer as BarcodeLayer;
    public StaticImageLayer? SelectedStaticImageLayer => SelectedLayer as StaticImageLayer;

    /// <summary>
    /// Readable kind of the selected layer, shown under its name.
    /// </summary>
    public string SelectedLayerTypeLabel => SelectedLayer switch
    {
        null => string.Empty,
        TextLayer => "Text",
        PhotoLayer => "Photo",
        BarcodeLayer { Symbology: BarcodeSymbology.QrCode } => "QR code",
        BarcodeLayer => "Barcode",
        StaticImageLayer => "Image",
        var layer => layer.LayerType
    };

    /// <summary>
    /// Smallest width or height a layer can be given in the designer.
    /// </summary>
    public const double MinLayerSizeMm = 2.0;

    // --- Selected layer properties ---
    // The editors bind here rather than to the layer itself: layers are immutable records shared with the preview
    // and print renderers on other threads, so every edit goes through ReplaceLayer as a new copy.

    public double LayerX
    {
        get => SelectedLayer?.X ?? 0.0;
        set { if (SelectedLayer is { } l) SetSelectedLayerBounds(value, l.Y, l.Width, l.Height); }
    }

    public double LayerY
    {
        get => SelectedLayer?.Y ?? 0.0;
        set { if (SelectedLayer is { } l) SetSelectedLayerBounds(l.X, value, l.Width, l.Height); }
    }

    public double LayerWidth
    {
        get => SelectedLayer?.Width ?? 0.0;
        set { if (SelectedLayer is { } l) SetSelectedLayerBounds(l.X, l.Y, value, l.Height); }
    }

    public double LayerHeight
    {
        get => SelectedLayer?.Height ?? 0.0;
        set { if (SelectedLayer is { } l) SetSelectedLayerBounds(l.X, l.Y, l.Width, value); }
    }

    public string TextContent
    {
        get => SelectedTextLayer?.Text ?? string.Empty;
        set
        {
            if (SelectedLayer is TextLayer text && value != null && value != text.Text)
            {
                ReplaceSelectedLayer(text with { Text = value });
            }
        }
    }

    public string? TextFontFamily
    {
        get => SelectedTextLayer?.FontFamily;
        set
        {
            // A combo box pushes null when it can't find the value among its items; that isn't a choice
            if (SelectedLayer is TextLayer text && !string.IsNullOrWhiteSpace(value) && value != text.FontFamily)
            {
                ReplaceSelectedLayer(text with { FontFamily = value });
            }
        }
    }

    public string? TextFontWeight
    {
        get => SelectedTextLayer?.FontWeight;
        set
        {
            if (SelectedLayer is TextLayer text && !string.IsNullOrWhiteSpace(value) && value != text.FontWeight)
            {
                ReplaceSelectedLayer(text with { FontWeight = value });
            }
        }
    }

    public double TextFontSize
    {
        get => SelectedTextLayer?.FontSize ?? 12.0;
        set
        {
            double size = Math.Clamp(Math.Round(value, 1), 1.0, 400.0);
            if (SelectedLayer is TextLayer text && !double.IsNaN(value) && size != text.FontSize)
            {
                ReplaceSelectedLayer(text with { FontSize = size });
            }
        }
    }

    public string PhotoSourceToken
    {
        get => SelectedPhotoLayer?.SourceToken ?? string.Empty;
        set
        {
            if (SelectedLayer is PhotoLayer photo && value != null && value != photo.SourceToken)
            {
                ReplaceSelectedLayer(photo with { SourceToken = value });
            }
        }
    }

    public string BarcodeContent
    {
        get => SelectedBarcodeLayer?.ContentToken ?? string.Empty;
        set
        {
            if (SelectedLayer is BarcodeLayer barcode && value != null && value != barcode.ContentToken)
            {
                ReplaceSelectedLayer(barcode with { ContentToken = value });
            }
        }
    }

    public bool BarcodeIncludeText
    {
        get => SelectedBarcodeLayer?.IncludeText ?? false;
        set
        {
            if (SelectedLayer is BarcodeLayer barcode && value != barcode.IncludeText)
            {
                ReplaceSelectedLayer(barcode with { IncludeText = value });
            }
        }
    }

    public bool BarcodePrintsAsPureBlack
    {
        get => SelectedBarcodeLayer?.IsPureBlackKResin ?? false;
        set
        {
            if (SelectedLayer is BarcodeLayer barcode && value != barcode.IsPureBlackKResin)
            {
                ReplaceSelectedLayer(barcode with { IsPureBlackKResin = value });
            }
        }
    }

    private static void AddOptionIfMissing(ObservableCollection<string> options, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !options.Contains(value))
        {
            options.Add(value);
        }
    }

    private static void RemoveUnusedOptions(ObservableCollection<string> options, IReadOnlyList<string> standard, string? current)
    {
        for (int i = options.Count - 1; i >= 0; i--)
        {
            if (!standard.Contains(options[i]) && options[i] != current)
            {
                options.RemoveAt(i);
            }
        }
    }

    // --- Selected text layer color ---

    /// <summary>
    /// The color the selected text layer prints in. K-resin text always prints black, so picking any
    /// other color moves the layer onto the color (dye-sublimation) panels.
    /// </summary>
    public string TextColorHex
    {
        // A hand-edited template can hold a color that doesn't parse; show black rather than failing the brush
        get => SelectedTextLayer is { IsPureBlackKResin: false } text && TryNormalizeColorHex(text.ColorHex, out string hex, out _)
            ? hex
            : "#000000";
        set
        {
            if (SelectedLayer is not TextLayer text || !TryNormalizeColorHex(value, out string hex, out bool isBlack))
            {
                // Revert the editor to the layer's current color
                OnPropertyChanged(nameof(TextColorHex));
                return;
            }

            bool pureBlack = text.IsPureBlackKResin && isBlack;
            if (string.Equals(text.ColorHex, hex, StringComparison.OrdinalIgnoreCase) && text.IsPureBlackKResin == pureBlack)
            {
                OnPropertyChanged(nameof(TextColorHex));
                return;
            }

            ReplaceSelectedLayer(text with { ColorHex = hex, IsPureBlackKResin = pureBlack });
        }
    }

    public Avalonia.Media.IBrush TextColorBrush =>
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(TextColorHex));

    /// <summary>
    /// When on, clicking the card picks up the colour under the pointer and gives it to the selected text layer.
    /// Reading it off the rendered card means a colour can be matched from a logo, a photo or another layer
    /// without anyone having to know its hex code.
    /// </summary>
    public bool IsPickingColor
    {
        get => _isPickingColor;
        set
        {
            bool allowed = value && CanPickColor;
            if (!SetProperty(ref _isPickingColor, allowed) && allowed != value)
            {
                // Bounce a rejected toggle back to the UI
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Matching needs a text layer to give the colour to, and a drawn card to read it from.
    /// </summary>
    public bool CanPickColor => SelectedLayer is TextLayer { IsLocked: false } && PreviewBitmap != null;

    /// <summary>
    /// Gives the selected text layer a colour picked off the card, then leaves picking mode.
    /// Returns false when the pick couldn't be used, in which case picking still ends.
    /// </summary>
    public bool ApplyPickedColor(string? hex)
    {
        bool wasPicking = IsPickingColor;
        IsPickingColor = false;

        if (!wasPicking || SelectedLayer is not TextLayer || !TryNormalizeColorHex(hex, out string colour, out _))
        {
            return false;
        }

        NameNextEdit("matching the colour");
        TextColorHex = colour;
        StatusText = $"Matched the colour {colour}.";
        return true;
    }

    public bool TextPrintsAsPureBlack
    {
        get => SelectedTextLayer?.IsPureBlackKResin ?? false;
        set
        {
            if (SelectedLayer is TextLayer text && text.IsPureBlackKResin != value)
            {
                ReplaceSelectedLayer(text with { IsPureBlackKResin = value });
            }
        }
    }

    public bool IsTextAlignLeft
    {
        get => (SelectedTextLayer?.Alignment ?? TextAlignment.Left) == TextAlignment.Left;
        set { if (value) SetTextAlignment(TextAlignment.Left); }
    }

    public bool IsTextAlignCenter
    {
        get => SelectedTextLayer?.Alignment == TextAlignment.Center;
        set { if (value) SetTextAlignment(TextAlignment.Center); }
    }

    public bool IsTextAlignRight
    {
        get => SelectedTextLayer?.Alignment == TextAlignment.Right;
        set { if (value) SetTextAlignment(TextAlignment.Right); }
    }

    private void SetTextAlignment(TextAlignment alignment)
    {
        if (SelectedLayer is TextLayer text && text.Alignment != alignment)
        {
            ReplaceSelectedLayer(text with { Alignment = alignment });
        }
    }

    /// <summary>
    /// Accepts #RGB, #ARGB, #RRGGBB or #AARRGGBB (the leading # is optional) and returns it as
    /// upper-case #RRGGBB, or #AARRGGBB when not fully opaque.
    /// </summary>
    public static bool TryNormalizeColorHex(string? input, out string hex, out bool isOpaqueBlack)
    {
        hex = "#000000";
        isOpaqueBlack = false;

        string trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || !SKColor.TryParse(trimmed, out var color))
        {
            return false;
        }

        hex = color.Alpha == 255
            ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
            : $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
        isOpaqueBlack = color == SKColors.Black;
        return true;
    }

    // --- Selected image (static image or photo layer) ---

    public bool IsImageLayerSelected => SelectedLayer is IImageLayer;

    private IImageLayer? SelectedImage => SelectedLayer as IImageLayer;
    private ImageAdjustments SelectedAdjustments => SelectedImage?.Adjustments ?? ImageAdjustments.None;

    /// <summary>
    /// True when the selected layer currently draws an image (for photos: the record photo or the fallback).
    /// </summary>
    public bool SelectedLayerHasImage => LayerHasImage(SelectedLayer);

    /// <summary>
    /// Small preview of the uploaded file: the static image, or a photo layer's fallback image.
    /// </summary>
    public AvaloniaBitmap? SelectedImageThumbnail
    {
        get => _selectedImageThumbnail;
        private set => SetProperty(ref _selectedImageThumbnail, value);
    }

    public bool HasSelectedImageThumbnail => SelectedImageThumbnail != null;

    public string SelectedImageInfo
    {
        get
        {
            string? source = GetUploadedImageSource(SelectedLayer);
            if (!ImageSourceLoader.TryGetSize(source, out int w, out int h))
            {
                return SelectedLayer is PhotoLayer ? "No fallback image" : "No image chosen";
            }

            string name = SelectedLayer switch
            {
                StaticImageLayer { SourceFileName: { Length: > 0 } fileName } => fileName,
                StaticImageLayer { ImagePath: { Length: > 0 } path } when !ImageSourceLoader.IsDataUri(path) => Path.GetFileName(path),
                PhotoLayer => "Fallback image",
                _ => "Embedded image"
            };
            return $"{name} · {w} × {h} px";
        }
    }

    public bool IsFillMode
    {
        get => SelectedImage?.CropMode == PhotoCropMode.AspectFill;
        set { if (value) UpdateSelectedImageLayer(cropMode: PhotoCropMode.AspectFill); }
    }

    public bool IsFitMode
    {
        get => SelectedImage?.CropMode == PhotoCropMode.AspectFit;
        set { if (value) UpdateSelectedImageLayer(cropMode: PhotoCropMode.AspectFit); }
    }

    public bool IsStretchMode
    {
        get => SelectedImage?.CropMode == PhotoCropMode.Stretch;
        set { if (value) UpdateSelectedImageLayer(cropMode: PhotoCropMode.Stretch); }
    }

    public double ImageZoom
    {
        get => SelectedAdjustments.Zoom;
        set => UpdateSelectedImageAdjustments(a => a with { Zoom = value });
    }

    public double ImageOffsetX
    {
        get => SelectedAdjustments.OffsetX;
        set => UpdateSelectedImageAdjustments(a => a with { OffsetX = value });
    }

    public double ImageOffsetY
    {
        get => SelectedAdjustments.OffsetY;
        set => UpdateSelectedImageAdjustments(a => a with { OffsetY = value });
    }

    public string ImageRotationText => $"{SelectedAdjustments.NormalizedRotation}°";

    public bool ImageFlipHorizontal
    {
        get => SelectedAdjustments.FlipHorizontal;
        set => UpdateSelectedImageAdjustments(a => a with { FlipHorizontal = value });
    }

    public bool ImageFlipVertical
    {
        get => SelectedAdjustments.FlipVertical;
        set => UpdateSelectedImageAdjustments(a => a with { FlipVertical = value });
    }

    public double ImageBrightness
    {
        get => SelectedAdjustments.Brightness;
        set => UpdateSelectedImageAdjustments(a => a with { Brightness = value });
    }

    public double ImageContrast
    {
        get => SelectedAdjustments.Contrast;
        set => UpdateSelectedImageAdjustments(a => a with { Contrast = value });
    }

    public double ImageSaturation
    {
        get => SelectedAdjustments.Saturation;
        set => UpdateSelectedImageAdjustments(a => a with { Saturation = value });
    }

    public double ImageOpacity
    {
        get => SelectedLayer?.Opacity ?? 1.0;
        set
        {
            double clamped = Math.Clamp(Math.Round(value, 3), 0.0, 1.0);
            if (SelectedLayer is IImageLayer && Math.Abs(SelectedLayer.Opacity - clamped) > 1e-9)
            {
                ReplaceSelectedLayer(SelectedLayer with { Opacity = clamped });
            }
        }
    }

    public double ImageCornerRadius
    {
        get => SelectedImage?.BorderRadius ?? 0.0;
        set => UpdateSelectedImageLayer(borderRadius: Math.Max(0.0, value));
    }

    public bool HasImageAdjustments =>
        SelectedImage != null && (!SelectedAdjustments.IsIdentityTransform || SelectedAdjustments.HasColorAdjustments);

    /// <summary>
    /// When on, the card shows a crop box over the selected image: dragging its edges crops the picture
    /// (<see cref="SetSelectedImageCropFrame"/>), dragging inside moves the picture under it, and the wheel zooms.
    /// </summary>
    public bool IsRepositioningImage
    {
        get => _isRepositioningImage;
        set
        {
            bool allowed = value && SelectedLayer is { IsLocked: false } and IImageLayer && SelectedLayerHasImage;
            if (!SetProperty(ref _isRepositioningImage, allowed) && allowed != value)
            {
                // Bounce a rejected toggle back to the UI
                OnPropertyChanged();
            }
        }
    }

    public CardFormat ActiveFormat
    {
        get => _activeFormat;
        set
        {
            // A combo box can push null while its items change
            if (value == null)
            {
                OnPropertyChanged();
                return;
            }

            if (_activeFormat != value)
            {
                RecordHistory("the card size", "format");
            }

            if (SetProperty(ref _activeFormat, value))
            {
                Template = Template with { TargetFormat = value };
                OnActiveFormatChanged();
            }
        }
    }

    /// <summary>
    /// The card size chosen in the format list, always in its landscape form; <see cref="IsPortrait"/> sets the orientation.
    /// </summary>
    public CardFormat? SelectedCardSize
    {
        get
        {
            var landscape = _activeFormat.WithOrientation(false);
            return AvailableFormats.FirstOrDefault(f => f == landscape);
        }
        set
        {
            if (value != null)
            {
                ActiveFormat = value.WithOrientation(IsPortrait);
            }
        }
    }

    public static IReadOnlyList<string> OrientationOptions { get; } = new[] { "Landscape", "Portrait" };

    public string SelectedOrientation
    {
        get => IsPortrait ? "Portrait" : "Landscape";
        set => IsPortrait = string.Equals(value, "Portrait", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPortrait
    {
        get => _isPortrait;
        set
        {
            if (SetProperty(ref _isPortrait, value))
            {
                OnPropertyChanged(nameof(SelectedOrientation));
                ActiveFormat = ActiveFormat.WithOrientation(value);
            }
        }
    }

    public AvaloniaBitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set
        {
            if (SetProperty(ref _previewBitmap, value))
            {
                // Nothing to sample a colour from until the card has been drawn at least once
                OnPropertyChanged(nameof(CanPickColor));
            }
        }
    }

    public int CurrentRecordIndex
    {
        get => _currentRecordIndex;
        set
        {
            if (SetProperty(ref _currentRecordIndex, value))
            {
                OnPropertyChanged(nameof(RecordPaginationText));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(SelectedLayerHasImage));
                SyncSelectedPersonToRecord();
                RequestLivePreviewUpdate();
            }
        }
    }

    public int TotalRecords => _records.Count > 0 ? _records.Count : 1;
    public bool HasRecords => _records.Count > 0;
    public string RecordPaginationText => _records.Count > 0
        ? $"Record {CurrentRecordIndex + 1} of {_records.Count}"
        : "Sample data — load a CSV to preview records";

    public bool CanGoPrevious => CurrentRecordIndex > 0;
    public bool CanGoNext => _records.Count > 0 && CurrentRecordIndex < _records.Count - 1;

    // --- People page ---

    public bool IsPeoplePageActive
    {
        get => _isPeoplePageActive;
        set
        {
            if (SetProperty(ref _isPeoplePageActive, value))
            {
                if (value)
                {
                    // Leaving the designer keeps text typed on the canvas, and puts the canvas tools away
                    CommitInlineTextEdit();
                    IsPickingColor = false;
                    IsRepositioningImage = false;
                }

                OnPropertyChanged(nameof(IsDesignPageActive));
                if (value)
                {
                    // Pick up fields typed into text layers since the list was last shown
                    RefreshPeopleColumns();
                    SyncSelectedPersonToRecord();
                }
            }
        }
    }

    public bool IsDesignPageActive
    {
        get => !_isPeoplePageActive;
        set => IsPeoplePageActive = !value;
    }

    public PersonItem? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            if (!SetProperty(ref _selectedPerson, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPersonSelected));
            OnPropertyChanged(nameof(CanPrintSelectedPerson));
            int index = value == null ? -1 : People.IndexOf(value);
            if (index >= 0)
            {
                CurrentRecordIndex = index;
            }
        }
    }

    public bool IsPersonSelected => SelectedPerson != null;

    public ICardPrinterDriver ActiveDriver => _activeDriver;

    public int PeopleToPrintCount => People.Count(p => p.IsIncluded);

    public bool TemplateHasPhotoLayer => Template.Layers.Any(l => l is PhotoLayer && l.IsVisible);

    public string PeopleTabText => People.Count > 0 ? $"People ({People.Count})" : "People";

    public string PeopleSummaryText
    {
        get
        {
            if (People.Count == 0)
            {
                return "Add people, give each one a photo, then print the whole list in order.";
            }

            string summary = $"{CountText(People.Count, "person", "people")} · {PeopleToPrintCount} to print";
            int missingPhotos = TemplateHasPhotoLayer ? People.Count(p => p.IsIncluded && !p.HasPhoto) : 0;
            return missingPhotos > 0 ? $"{summary} · {missingPhotos} without a photo" : summary;
        }
    }

    public string PrintPeopleButtonText => PeopleToPrintCount == 1 ? "Print 1 badge" : $"Print {PeopleToPrintCount} badges";

    public bool CanPrintPeople => CanStartBatch && PeopleToPrintCount > 0;

    public bool CanPrintSelectedPerson => CanStartBatch && SelectedPerson != null;

    /// <summary>
    /// Header checkbox: ticked when everyone prints, cleared when nobody does, mixed otherwise.
    /// </summary>
    public bool? AllPeopleIncluded
    {
        get
        {
            int included = PeopleToPrintCount;
            return included == 0 ? false : included == People.Count ? true : null;
        }
        set => SetAllPeopleIncluded(value != false);
    }

    public string CsvFilePath
    {
        get => _csvFilePath;
        set
        {
            if (SetProperty(ref _csvFilePath, value))
            {
                OnPropertyChanged(nameof(CsvFileName));
            }
        }
    }

    public string CsvFileName => string.IsNullOrEmpty(CsvFilePath) ? "No file selected" : Path.GetFileName(CsvFilePath);

    public string PhotoFolderPath
    {
        get => _photoFolderPath;
        set
        {
            if (SetProperty(ref _photoFolderPath, value))
            {
                OnPropertyChanged(nameof(PhotoFolderName));
            }
        }
    }

    public string PhotoFolderName => string.IsNullOrEmpty(PhotoFolderPath)
        ? "No folder selected"
        : Path.GetFileName(Path.TrimEndingDirectorySeparator(PhotoFolderPath));

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            // Switching printers mid-run would leave the run's prompts talking to a different driver
            if (string.IsNullOrEmpty(value) || !CanStartBatch)
            {
                OnPropertyChanged();
                return;
            }

            var previousDriver = _activeDriver;
            if (SetProperty(ref _selectedPrinter, value))
            {
                _activeDriver = value == MockPrinterName
                    ? new MockCardPrinterDriver()
                    : new IdpSmart31CardPrinterDriver(value);
                previousDriver.Dispose();
                OnPropertyChanged(nameof(ActiveDriver));
                RefreshPrinterCompatibility();
            }
        }
    }

    public BatchExecutionState BatchState
    {
        get => _batchState;
        set
        {
            if (SetProperty(ref _batchState, value))
            {
                RaiseBatchCommandsChanged();
            }
        }
    }

    public bool IsBatchRunning => BatchState == BatchExecutionState.Running;

    // A run held on the stop prompt is also PausedByUser, but its answer is Finish/Eject, never Resume
    public bool IsBatchPausedByUser => BatchState == BatchExecutionState.PausedByUser && !StopPromptVisible;
    public bool IsBatchActive => BatchState is BatchExecutionState.Running
        or BatchExecutionState.PausedForHopper
        or BatchExecutionState.PausedOnError
        or BatchExecutionState.PausedByUser;
    public bool CanStartBatch => !_batchInFlight &&
        BatchState is BatchExecutionState.Idle or BatchExecutionState.Completed or BatchExecutionState.Cancelled;

    public bool HopperPausePromptVisible
    {
        get => _hopperPausePromptVisible;
        set => SetProperty(ref _hopperPausePromptVisible, value);
    }

    /// <summary>
    /// True while the run is held on the card that is in the printer, waiting for the operator to say whether to
    /// let that card finish or to eject it as it is.
    /// </summary>
    public bool StopPromptVisible
    {
        get => _stopPromptVisible;
        private set
        {
            if (SetProperty(ref _stopPromptVisible, value))
            {
                OnPropertyChanged(nameof(IsBatchPausedByUser));
                OnPropertyChanged(nameof(CanStopBatchRun));
            }
        }
    }

    public string StopPromptMessage
    {
        get => _stopPromptMessage;
        private set => SetProperty(ref _stopPromptMessage, value);
    }

    /// <summary>
    /// Stopping is offered while a run is in flight and isn't already asking what to do about the card in the printer.
    /// </summary>
    public bool CanStopBatchRun => _batchInFlight && !StopPromptVisible;

    public string HopperPauseMessage
    {
        get => _hopperPauseMessage;
        set => SetProperty(ref _hopperPauseMessage, value);
    }

    public bool ErrorPromptVisible
    {
        get => _errorPromptVisible;
        set => SetProperty(ref _errorPromptVisible, value);
    }

    public string ErrorPromptMessage
    {
        get => _errorPromptMessage;
        set => SetProperty(ref _errorPromptMessage, value);
    }

    public int BatchProgressPercent
    {
        get => _batchProgressPercent;
        set => SetProperty(ref _batchProgressPercent, value);
    }

    public MainWindowViewModel()
    {
        _activeDriver = new MockCardPrinterDriver();

        // The simulator, then the printers actually installed (Windows only)
        AvailablePrinters.Add(MockPrinterName);
        foreach (string printerName in IdpSmart31CardPrinterDriver.GetInstalledPrinterNames())
        {
            AvailablePrinters.Add(printerName);
        }

        AvailableFormats.Add(CardFormat.CR80);
        AvailableFormats.Add(CardFormat.CR79);

        // Wire batch engine events
        _batchEngine.StateChanged += OnBatchEngineStateChanged;

        // Initialize default badge template (this also checks the printer and renders the first preview)
        ApplyTemplate(CreateDefaultTemplate());
    }

    private static TemplateDefinition CreateDefaultTemplate()
    {
        return new TemplateDefinition
        {
            Name = "Employee ID Badge",
            TargetFormat = CardFormat.CR80,
            Layers = new List<TemplateLayer>
            {
                new TextLayer
                {
                    Name = "Company Header",
                    Text = "APEX SYSTEMS • SECURE ACCESS",
                    FontFamily = "Arial",
                    FontSize = 9.0,
                    FontWeight = "Bold",
                    ColorHex = "#7A918D", // Grey Olive
                    IsPureBlackKResin = false,
                    Alignment = TextAlignment.Center,
                    X = 5.0,
                    Y = 4.0,
                    Width = 75.6,
                    Height = 6.0,
                    ZIndex = 1
                },
                new PhotoLayer
                {
                    Name = "Portrait Photo",
                    SourceToken = "{{Photo}}",
                    BorderRadius = 3.0,
                    CropMode = PhotoCropMode.AspectFill,
                    X = 6.0,
                    Y = 12.0,
                    Width = 24.0,
                    Height = 32.0,
                    ZIndex = 2
                },
                new TextLayer
                {
                    Name = "Full Name",
                    Text = "{{FirstName}} {{LastName}}",
                    FontFamily = "Arial",
                    FontSize = 14.0,
                    FontWeight = "Bold",
                    ColorHex = "#000000",
                    IsPureBlackKResin = true,
                    Alignment = TextAlignment.Left,
                    X = 33.0,
                    Y = 14.0,
                    Width = 48.0,
                    Height = 8.0,
                    ZIndex = 3
                },
                new TextLayer
                {
                    Name = "Department",
                    Text = "{{Department}}",
                    FontFamily = "Arial",
                    FontSize = 9.0,
                    FontWeight = "Normal",
                    ColorHex = "#000000",
                    IsPureBlackKResin = true,
                    Alignment = TextAlignment.Left,
                    X = 33.0,
                    Y = 23.0,
                    Width = 48.0,
                    Height = 6.0,
                    ZIndex = 4
                },
                new BarcodeLayer
                {
                    Name = "QR Code",
                    ContentToken = "https://badgeforge.io/v/{{EmployeeId}}",
                    Symbology = BarcodeSymbology.QrCode,
                    IsPureBlackKResin = true,
                    X = 64.0,
                    Y = 32.0,
                    Width = 16.0,
                    Height = 16.0,
                    ZIndex = 5
                },
                new BarcodeLayer
                {
                    Name = "Employee Barcode",
                    ContentToken = "{{EmployeeId}}",
                    Symbology = BarcodeSymbology.Code128,
                    IncludeText = true,
                    IsPureBlackKResin = true,
                    X = 6.0,
                    Y = 46.0,
                    Width = 55.0,
                    Height = 6.5,
                    ZIndex = 6
                }
            }
        };
    }

    /// <summary>
    /// Shows a template in the designer, matching the card size and orientation to it, and treats it as saved.
    /// </summary>
    private void ApplyTemplate(TemplateDefinition template)
    {
        // The layer being edited belongs to the design that is going away
        CancelInlineTextEdit();
        Template = template;
        if (!Equals(_activeFormat, template.TargetFormat))
        {
            _activeFormat = template.TargetFormat;
            OnPropertyChanged(nameof(ActiveFormat));
        }

        OnActiveFormatChanged();
        RefreshLayersList();
        SelectedLayer = Layers.FirstOrDefault();
        _savedTemplateJson = _templateStorage.Serialize(template);

        // Undoing across a new or opened file would resurrect layers from a design the user has closed
        _history.Clear();
        RaiseHistoryChanged();
    }

    /// <summary>
    /// Replaces the design with the starter badge template. The people list and any print run are left alone:
    /// each run prints from its own copy of the template.
    /// </summary>
    public void NewTemplate()
    {
        ApplyTemplate(CreateDefaultTemplate());
        StatusText = "Started a new template.";
    }

    /// <summary>
    /// Replaces the design with a completely blank badge template with no layers.
    /// </summary>
    public void NewBlankTemplate(CardFormat? format = null)
    {
        var targetFormat = format ?? CardFormat.CR80;
        ApplyTemplate(new TemplateDefinition
        {
            Name = "Blank Badge",
            TargetFormat = targetFormat,
            Layers = new List<TemplateLayer>()
        });
        StatusText = "Started a new blank template.";
    }

    /// <summary>
    /// True when the template differs from the one last opened, saved or started.
    /// </summary>
    public bool HasUnsavedTemplateChanges => _savedTemplateJson != _templateStorage.Serialize(Template);

    private void OnActiveFormatChanged()
    {
        if (_isPortrait != _activeFormat.IsPortrait)
        {
            _isPortrait = _activeFormat.IsPortrait;
            OnPropertyChanged(nameof(IsPortrait));
        }

        // A loaded template may use a card size that isn't offered yet
        var landscape = _activeFormat.WithOrientation(false);
        if (!AvailableFormats.Contains(landscape))
        {
            AvailableFormats.Add(landscape);
        }

        OnPropertyChanged(nameof(SelectedCardSize));
        RefreshPrinterCompatibility();
        RequestLivePreviewUpdate();
    }

    /// <summary>
    /// Rebuilds the layer list, topmost layer first (the reverse of drawing order), keeping the selected layer selected.
    /// </summary>
    public void RefreshLayersList()
    {
        var selected = _selectedLayer;
        bool wasReplacing = _isReplacingLayer;
        _isReplacingLayer = true;
        try
        {
            SyncLayersList(Template.Layers
                .Select((layer, index) => (layer, index))
                .OrderByDescending(t => t.layer.ZIndex)
                .ThenByDescending(t => t.index)
                .Select(t => t.layer)
                .ToList(), selected);
        }
        finally
        {
            _isReplacingLayer = wasReplacing;
        }

        // The selected layer may since have been swapped for an updated copy, or removed
        RestoreSelection(selected == null ? null : FindTemplateLayer(selected) is { } index and >= 0 ? Template.Layers[index] : null);
        UpdateColumnMappingPlaceholders();
    }

    /// <summary>
    /// Brings the layer list into the given order with the fewest changes. The ListBox drops its selection when the
    /// selected row is removed or moved, and doesn't pick it up again when the same layer is announced, so that row
    /// is never moved: the rows around it are.
    /// </summary>
    private void SyncLayersList(IReadOnlyList<TemplateLayer> desired, TemplateLayer? selected)
    {
        var wanted = new HashSet<TemplateLayer>(desired, ReferenceEqualityComparer.Instance);
        int i = 0;
        while (i < desired.Count)
        {
            if (i < Layers.Count && ReferenceEquals(Layers[i], desired[i]))
            {
                i++;
                continue;
            }

            int found = -1;
            for (int j = i; j < Layers.Count; j++)
            {
                if (ReferenceEquals(Layers[j], desired[i]))
                {
                    found = j;
                    break;
                }
            }

            if (found < 0)
            {
                Layers.Insert(i, desired[i]);
                i++;
            }
            else if (ReferenceEquals(desired[i], selected))
            {
                // Clear the way for the selected row instead of moving it
                if (wanted.Contains(Layers[i]))
                {
                    Layers.Move(i, Layers.Count - 1);
                }
                else
                {
                    Layers.RemoveAt(i);
                }
            }
            else
            {
                Layers.Move(found, i);
                i++;
            }
        }

        while (Layers.Count > desired.Count)
        {
            Layers.RemoveAt(Layers.Count - 1);
        }
    }

    /// <summary>
    /// Selects a layer, re-announcing the selection when it is unchanged so a list that dropped it while its items
    /// were replaced shows it again.
    /// </summary>
    private void RestoreSelection(TemplateLayer? layer)
    {
        if (ReferenceEquals(_selectedLayer, layer))
        {
            OnPropertyChanged(nameof(SelectedLayer));
        }
        else
        {
            SelectedLayer = layer;
        }
    }

    /// <summary>
    /// Where a layer sits in the template: that exact instance, or else the layer with its ID (an updated copy).
    /// </summary>
    private int FindTemplateLayer(TemplateLayer layer)
    {
        int index = Template.Layers.FindIndex(l => ReferenceEquals(l, layer));
        return index >= 0 ? index : Template.Layers.FindIndex(l => l.Id == layer.Id);
    }

    public void UpdateColumnMappingPlaceholders()
    {
        var tokens = Template.GetAllReferencedTokens();

        foreach (var old in ColumnMappings)
        {
            old.PropertyChanged -= OnColumnMappingChanged;
        }

        ColumnMappings.Clear();
        foreach (var t in tokens)
        {
            // A mapping the user typed survives the token briefly disappearing while a layer's text is edited
            var mapping = new ColumnMappingItem
            {
                TemplateToken = t,
                CsvColumn = _mappingMemory.TryGetValue(t, out var col) ? col : t
            };
            mapping.PropertyChanged += OnColumnMappingChanged;
            ColumnMappings.Add(mapping);
        }

        RefreshPeopleColumns();
        RaisePrintSelectionChanged();
    }

    // --- Record Pagination ---

    public void NextRecord()
    {
        if (CanGoNext)
        {
            CurrentRecordIndex++;
        }
    }

    public void PreviousRecord()
    {
        if (CanGoPrevious)
        {
            CurrentRecordIndex--;
        }
    }

    public void FirstRecord()
    {
        if (_records.Count > 0)
        {
            CurrentRecordIndex = 0;
        }
    }

    public void LastRecord()
    {
        if (_records.Count > 0)
        {
            CurrentRecordIndex = _records.Count - 1;
        }
    }

    public IReadOnlyDictionary<string, string> GetActiveRecordFieldData()
    {
        if (_records.Count > 0 && CurrentRecordIndex >= 0 && CurrentRecordIndex < _records.Count)
        {
            return GetRecordFieldData(_records[CurrentRecordIndex]);
        }

        // Clean default sample preview data when no CSV loaded
        var sample = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstName"] = "ALEXANDER",
            ["LastName"] = "CROSS",
            ["JobTitle"] = "SYSTEMS ENGINEER",
            ["Department"] = "INFRASTRUCTURE & DEV",
            ["EmployeeId"] = "SEC-4091-AC",
            ["ExpiryDate"] = "2027-12-31"
        };
        CardholderMapper.AddStandardTokens(sample, includePhoto: false);
        return sample;
    }

    /// <summary>
    /// The token values a record's badge renders with: its fields, the column mappings, then its photo.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetRecordFieldData(BadgeRecord rec)
    {
        var dict = new Dictionary<string, string>(rec.Fields, StringComparer.OrdinalIgnoreCase);

        // Standard cardholder tokens ({{FullName}}, {{JobTitle}}, …) also work on rosters that label those columns
        // differently ("Name", "Title", …). Photos are resolved below.
        CardholderMapper.AddStandardTokens(dict, includePhoto: false);

        foreach (var mapping in ColumnMappings)
        {
            if (!string.IsNullOrEmpty(mapping.CsvColumn) &&
                rec.Fields.TryGetValue(mapping.CsvColumn, out var val))
            {
                dict[mapping.TemplateToken] = val;
            }
        }

        // The photo chosen for this person wins over whatever a Photo column last said
        if (!string.IsNullOrEmpty(rec.ResolvedPhotoPath))
        {
            dict["Photo"] = rec.ResolvedPhotoPath;
            dict["{Photo}"] = rec.ResolvedPhotoPath;
            foreach (var token in GetPhotoOnlyTokens())
            {
                dict[token] = rec.ResolvedPhotoPath;
            }
        }

        return dict;
    }

    // --- People fields ---

    /// <summary>
    /// Tokens that only feed photo layers. They're set through each person's photo rather than typed.
    /// </summary>
    private HashSet<string> GetPhotoOnlyTokens()
    {
        var photoOnly = new HashSet<string>(
            Template.Layers.OfType<PhotoLayer>().SelectMany(l => l.GetReferencedTokens()),
            StringComparer.OrdinalIgnoreCase);
        photoOnly.ExceptWith(Template.Layers.Where(l => l is not PhotoLayer).SelectMany(l => l.GetReferencedTokens()));
        return photoOnly;
    }

    /// <summary>
    /// The template fields each person fills in, first and last name leading.
    /// </summary>
    private List<string> GetPersonFieldTokens()
    {
        var photoOnly = GetPhotoOnlyTokens();
        string[] leading = { "FirstName", "LastName" };
        return Template.GetAllReferencedTokens()
            .Where(t => !photoOnly.Contains(t))
            .OrderBy(t =>
            {
                int i = Array.FindIndex(leading, l => l.Equals(t, StringComparison.OrdinalIgnoreCase));
                return i < 0 ? leading.Length : i;
            })
            .ToList();
    }

    private string GetPhotoColumnKey()
    {
        string? token = GetPhotoOnlyTokens().FirstOrDefault();
        return token == null ? RosterService.PhotoColumn : ResolveFieldKey(token);
    }

    /// <summary>
    /// The record field a template token reads from: its mapped CSV column, or the token name.
    /// </summary>
    private string ResolveFieldKey(string token)
    {
        var mapping = ColumnMappings.FirstOrDefault(m => string.Equals(m.TemplateToken, token, StringComparison.OrdinalIgnoreCase));
        string? column = mapping?.CsvColumn;
        return string.IsNullOrWhiteSpace(column) ? token : column.Trim();
    }

    /// <summary>
    /// The template's fields in the order the People grid shows them: the schema order, rearranged by whatever the
    /// user dragged. Only the grid uses this; what each column *means* comes from its token either way.
    /// </summary>
    private IReadOnlyList<string> GetDisplayFieldTokens() => _columnLayout.Arrange(GetPersonFieldTokens());

    /// <summary>
    /// Identifies the shown columns by token, display position and the record field each reads from, so the grid is
    /// only rebuilt when one of those actually changed.
    /// </summary>
    private string ColumnsSignature(IEnumerable<string> tokens) =>
        string.Join("|", tokens.Select(t => t + "=" + ResolveFieldKey(t)));

    private void RefreshPeopleColumns()
    {
        var tokens = GetDisplayFieldTokens();
        string signature = ColumnsSignature(tokens);
        if (signature == _peopleColumnsSignature)
        {
            return;
        }

        _peopleColumnsSignature = signature;
        PeopleFieldColumns.Clear();
        foreach (var token in tokens)
        {
            PeopleFieldColumns.Add(new PeopleFieldColumn(token));
        }

        foreach (var person in People)
        {
            BuildCells(person, tokens);
        }

        RaiseColumnOrderChanged();
    }

    // --- People column order (display only) ---

    /// <summary>
    /// Whether there is more than one column to rearrange.
    /// </summary>
    public bool CanReorderPeopleColumns => PeopleFieldColumns.Count > 1;

    /// <summary>
    /// Whether the grid is showing a column order the user set, rather than the template's own field order.
    /// </summary>
    public bool IsPeopleColumnOrderCustomised => _columnLayout.IsCustomised;

    /// <summary>
    /// Moves a column to a position in the grid. This changes where values appear and nothing else: field names,
    /// record contents, the field mapping and every <c>{{Token}}</c> bound in the badge designer are untouched, so
    /// the badges printed after a reorder are identical to the ones printed before it.
    /// </summary>
    /// <returns>True when the grid changed.</returns>
    public bool MovePeopleColumn(PeopleFieldColumn? column, int targetIndex)
    {
        if (column == null)
        {
            return false;
        }

        var tokens = PeopleFieldColumns.Select(c => c.Token).ToList();
        if (_columnLayout.Move(tokens, column.Token, targetIndex) is not { } move)
        {
            return false;
        }

        MoveColumnAt(move.From, move.To);
        _peopleColumnsSignature = ColumnsSignature(PeopleFieldColumns.Select(c => c.Token));
        RaiseColumnOrderChanged();
        return true;
    }

    /// <summary>
    /// Moves a column left (negative) or right (positive) by that many places.
    /// </summary>
    public bool MovePeopleColumnBy(PeopleFieldColumn? column, int offset)
    {
        int from = column == null ? -1 : PeopleFieldColumns.IndexOf(column);
        return from >= 0 && MovePeopleColumn(column, from + offset);
    }

    /// <summary>
    /// Puts the columns back in the template's field order, forgetting the user's arrangement.
    /// </summary>
    public bool ResetPeopleColumnOrder()
    {
        if (!_columnLayout.IsCustomised)
        {
            return false;
        }

        _columnLayout.Reset();
        bool changed = ApplyDisplayOrder(GetPersonFieldTokens());
        RaiseColumnOrderChanged();
        StatusText = "Columns are back in the order the badge uses them.";
        return changed;
    }

    /// <summary>
    /// Rearranges the shown columns into <paramref name="target"/> order by moving them rather than rebuilding them.
    /// </summary>
    private bool ApplyDisplayOrder(IReadOnlyList<string> target)
    {
        bool changed = false;
        for (int to = 0; to < target.Count && to < PeopleFieldColumns.Count; to++)
        {
            int from = to;
            while (from < PeopleFieldColumns.Count &&
                   !string.Equals(PeopleFieldColumns[from].Token, target[to], StringComparison.OrdinalIgnoreCase))
            {
                from++;
            }

            if (from < PeopleFieldColumns.Count && from != to)
            {
                MoveColumnAt(from, to);
                changed = true;
            }
        }

        if (changed)
        {
            _peopleColumnsSignature = ColumnsSignature(PeopleFieldColumns.Select(c => c.Token));
        }

        return changed;
    }

    /// <summary>
    /// Moves one column and every row's matching cell. Moving rather than rebuilding keeps each cell's editor —
    /// and any half-typed value in it — alive across the reorder.
    /// </summary>
    private void MoveColumnAt(int from, int to)
    {
        PeopleFieldColumns.Move(from, to);
        foreach (var person in People)
        {
            if (from < person.Cells.Count && to < person.Cells.Count)
            {
                person.Cells.Move(from, to);
            }
        }
    }

    private void RaiseColumnOrderChanged()
    {
        OnPropertyChanged(nameof(CanReorderPeopleColumns));
        OnPropertyChanged(nameof(IsPeopleColumnOrderCustomised));
    }

    private void BuildCells(PersonItem person, IReadOnlyList<string> tokens)
    {
        person.Cells.Clear();
        foreach (var token in tokens)
        {
            person.Cells.Add(new PersonFieldCell(person, token, ResolveFieldKey(token)));
        }
        person.RaiseNameChanged();
    }

    private void OnColumnMappingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ColumnMappingItem mapping)
        {
            _mappingMemory[mapping.TemplateToken] = mapping.CsvColumn;
        }

        RefreshPeopleColumns();
    }

    private static string CountText(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";

    // --- Layer Operations ---

    public void AddTextLayer(double x = 10, double y = 10, string? text = null, string? name = null)
    {
        RecordHistory("adding the text");
        int nextZ = Template.Layers.Count > 0 ? Template.Layers.Max(l => l.ZIndex) + 1 : 1;
        var layer = new TextLayer
        {
            Name = name ?? $"Text {nextZ}",
            Text = text ?? "New Text Element",
            FontSize = 12.0,
            FontFamily = "Arial",
            X = x,
            Y = y,
            Width = 40.0,
            Height = 8.0,
            ZIndex = nextZ
        };

        Template.Layers.Add(layer);
        RefreshLayersList();
        SelectedLayer = layer;
        RequestLivePreviewUpdate();
    }

    public void AddPhotoLayer(double x = 10, double y = 10)
    {
        RecordHistory("adding the photo frame");
        int nextZ = Template.Layers.Count > 0 ? Template.Layers.Max(l => l.ZIndex) + 1 : 1;
        var layer = new PhotoLayer
        {
            Name = $"Photo {nextZ}",
            SourceToken = "{{Photo}}",
            BorderRadius = 2.0,
            CropMode = PhotoCropMode.AspectFill,
            X = x,
            Y = y,
            Width = 25.0,
            Height = 35.0,
            ZIndex = nextZ
        };

        Template.Layers.Add(layer);
        RefreshLayersList();
        SelectedLayer = layer;
        RequestLivePreviewUpdate();
    }

    public void AddBarcodeLayer(double x = 10, double y = 10, BarcodeSymbology symbology = BarcodeSymbology.Code128)
    {
        RecordHistory(symbology == BarcodeSymbology.QrCode ? "adding the QR code" : "adding the barcode");
        int nextZ = Template.Layers.Count > 0 ? Template.Layers.Max(l => l.ZIndex) + 1 : 1;
        var layer = new BarcodeLayer
        {
            Name = symbology == BarcodeSymbology.QrCode ? $"QR Code {nextZ}" : $"Barcode {nextZ}",
            ContentToken = "{{EmployeeId}}",
            Symbology = symbology,
            IncludeText = symbology != BarcodeSymbology.QrCode,
            IsPureBlackKResin = true,
            X = x,
            Y = y,
            Width = symbology == BarcodeSymbology.QrCode ? 18.0 : 45.0,
            Height = symbology == BarcodeSymbology.QrCode ? 18.0 : 8.0,
            ZIndex = nextZ
        };

        Template.Layers.Add(layer);
        RefreshLayersList();
        SelectedLayer = layer;
        RequestLivePreviewUpdate();
    }

    public void AddStaticImageLayer(double x = 10, double y = 10)
    {
        RecordHistory("adding the image");
        int nextZ = Template.Layers.Count > 0 ? Template.Layers.Max(l => l.ZIndex) + 1 : 1;
        var layer = new StaticImageLayer
        {
            Name = $"Image {nextZ}",
            MaintainAspectRatio = true,
            X = x,
            Y = y,
            Width = 20.0,
            Height = 20.0,
            ZIndex = nextZ
        };

        Template.Layers.Add(layer);
        RefreshLayersList();
        SelectedLayer = layer;
        RequestLivePreviewUpdate();
    }

    public void DeleteSelectedLayer()
    {
        if (SelectedLayer != null)
        {
            DeleteLayer(SelectedLayer);
        }
    }

    public void DeleteLayer(TemplateLayer layer)
    {
        int templateIndex = FindTemplateLayer(layer);
        if (layer.IsLocked || templateIndex < 0)
        {
            return;
        }

        if (_inlineEditingLayer?.Id == layer.Id)
        {
            CancelInlineTextEdit();
        }

        RecordHistory("deleting the layer");

        int listIndex = Layers.IndexOf(Template.Layers[templateIndex]);
        bool wasSelected = _selectedLayer?.Id == layer.Id;

        Template.Layers.RemoveAt(templateIndex);
        RefreshLayersList();

        // Deleting another layer from its row menu leaves the selection alone; deleting the selected one moves
        // the selection to the layer that took its place in the list
        if (wasSelected)
        {
            SelectedLayer = Layers.Count == 0 ? null : Layers[Math.Clamp(listIndex, 0, Layers.Count - 1)];
        }
        RequestLivePreviewUpdate();
    }

    public void DuplicateSelectedLayer()
    {
        if (SelectedLayer != null)
        {
            DuplicateLayer(SelectedLayer);
        }
    }

    public void DuplicateLayer(TemplateLayer layer)
    {
        RecordHistory("duplicating the layer");
        int nextZ = Template.Layers.Max(l => l.ZIndex) + 1;
        var duplicate = layer with
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{layer.Name} (Copy)",
            X = layer.X + 2.0,
            Y = layer.Y + 2.0,
            ZIndex = nextZ,
            // A copy of a locked layer lands on top of it, so it has to be movable
            IsLocked = false
        };

        Template.Layers.Add(duplicate);
        RefreshLayersList();
        SelectedLayer = duplicate;
        RequestLivePreviewUpdate();
    }

    /// <summary>
    /// Shows or hides a layer on the card without needing to select it first.
    /// </summary>
    public void ToggleLayerVisibility(TemplateLayer layer) => ReplaceLayer(layer, layer with { IsVisible = !layer.IsVisible });

    /// <summary>
    /// Locks or unlocks a layer against accidental drag/resize on the canvas without needing to select it first.
    /// </summary>
    public void ToggleLayerLock(TemplateLayer layer)
    {
        if (!layer.IsLocked && _selectedLayer?.Id == layer.Id)
        {
            IsRepositioningImage = false;
        }

        ReplaceLayer(layer, layer with { IsLocked = !layer.IsLocked });
    }

    /// <summary>
    /// Moves a layer one step up or down in stacking order (direction: +1 raises it, -1 lowers it).
    /// Stacking is renumbered 1..n in drawing order, so layers that shared a ZIndex can still be reordered.
    /// </summary>
    public void MoveLayerStackOrder(TemplateLayer layer, int direction)
    {
        // Bottom to top, in the order the card draws them
        var ordered = Template.Layers
            .Select((l, i) => (layer: l, index: i))
            .OrderBy(t => t.layer.ZIndex)
            .ThenBy(t => t.index)
            .Select(t => t.layer)
            .ToList();

        int index = ordered.FindIndex(l => l.Id == layer.Id);
        int neighborIndex = index + Math.Sign(direction);
        if (index < 0 || direction == 0 || neighborIndex < 0 || neighborIndex >= ordered.Count)
        {
            return;
        }

        RecordHistory("the layer order");

        (ordered[index], ordered[neighborIndex]) = (ordered[neighborIndex], ordered[index]);
        for (int z = 0; z < ordered.Count; z++)
        {
            var current = ordered[z];
            if (current.ZIndex != z + 1)
            {
                Template.Layers[Template.Layers.FindIndex(l => ReferenceEquals(l, current))] = current with { ZIndex = z + 1 };
            }
        }

        RefreshLayersList();
        RequestLivePreviewUpdate();
    }

    public void MoveSelectedLayer(double deltaXmm, double deltaYmm)
    {
        if (SelectedLayer != null && !SelectedLayer.IsLocked)
        {
            double newX = Math.Max(0, Math.Round(SelectedLayer.X + deltaXmm, 1));
            double newY = Math.Max(0, Math.Round(SelectedLayer.Y + deltaYmm, 1));

            ReplaceSelectedLayer(SelectedLayer with { X = newX, Y = newY });
        }
    }

    /// <summary>
    /// Sets the selected layer's position and size in one step (used by the property editors and resize handles),
    /// so the preview never draws a half-applied change.
    /// </summary>
    public void SetSelectedLayerBounds(double xMm, double yMm, double widthMm, double heightMm)
    {
        if (SelectedLayer is not { IsLocked: false } layer ||
            double.IsNaN(xMm) || double.IsNaN(yMm) || double.IsNaN(widthMm) || double.IsNaN(heightMm))
        {
            return;
        }

        double x = Math.Max(0, Math.Round(xMm, 2));
        double y = Math.Max(0, Math.Round(yMm, 2));
        double width = Math.Max(MinLayerSizeMm, Math.Round(widthMm, 2));
        double height = Math.Max(MinLayerSizeMm, Math.Round(heightMm, 2));
        if (x == layer.X && y == layer.Y && width == layer.Width && height == layer.Height)
        {
            // Bounce a clamped value back to the editor
            OnPropertyChanged(nameof(LayerX));
            OnPropertyChanged(nameof(LayerY));
            OnPropertyChanged(nameof(LayerWidth));
            OnPropertyChanged(nameof(LayerHeight));
            return;
        }

        ReplaceSelectedLayer(layer with { X = x, Y = y, Width = width, Height = height });
    }

    /// <summary>
    /// Places the selected layer's top-left corner at an exact card position (used by drag and snapping).
    /// </summary>
    public void SetSelectedLayerPosition(double xMm, double yMm)
    {
        if (SelectedLayer == null || SelectedLayer.IsLocked)
        {
            return;
        }

        double newX = Math.Max(0, Math.Round(xMm, 2));
        double newY = Math.Max(0, Math.Round(yMm, 2));
        if (newX == SelectedLayer.X && newY == SelectedLayer.Y)
        {
            return;
        }

        ReplaceSelectedLayer(SelectedLayer with { X = newX, Y = newY });
    }

    public void ResizeSelectedLayer(double deltaWmm, double deltaHmm)
    {
        if (SelectedLayer != null && !SelectedLayer.IsLocked)
        {
            double newW = Math.Max(4.0, Math.Round(SelectedLayer.Width + deltaWmm, 1));
            double newH = Math.Max(4.0, Math.Round(SelectedLayer.Height + deltaHmm, 1));

            ReplaceSelectedLayer(SelectedLayer with { Width = newW, Height = newH });
        }
    }

    /// <summary>
    /// Swaps the selected layer for an updated copy. The template list (insertion order) and the
    /// display list (Z order) can differ, so each is updated at its own index.
    /// </summary>
    private void ReplaceSelectedLayer(TemplateLayer updated)
    {
        // Values the editors push back while switching to another layer aren't edits (see SelectedLayer)
        if (_isAnnouncingSelection || SelectedLayer == null)
        {
            return;
        }

        ReplaceLayer(SelectedLayer, updated);
    }

    private void ReplaceLayer(TemplateLayer current, TemplateLayer updated)
    {
        // Read and clear together: an operation that names an edit but then changes nothing must not leave its
        // name behind for whatever the user does next
        var named = _namedEdit;
        _namedEdit = null;

        int templateIndex = FindTemplateLayer(current);
        if (templateIndex < 0)
        {
            return;
        }

        var existing = Template.Layers[templateIndex];
        if (updated.Equals(existing))
        {
            return;
        }

        var (label, mergeKey) = named ?? DescribeLayerChange(existing, updated);
        RecordHistory(label, mergeKey);

        var selected = _selectedLayer;
        bool wasSelected = selected?.Id == existing.Id;
        bool wasReplacing = _isReplacingLayer;
        _isReplacingLayer = true;
        try
        {
            Template.Layers[templateIndex] = updated;
            if (_inlineEditingLayer?.Id == updated.Id && updated is TextLayer editedCopy)
            {
                // Restyled from the properties panel mid-edit: the canvas editor follows the new copy
                InlineEditingLayer = editedCopy;
            }

            int listIndex = Layers.IndexOf(existing);
            if (listIndex >= 0)
            {
                Layers[listIndex] = updated;
            }

            if (wasSelected)
            {
                SelectedLayer = updated;
            }
        }
        finally
        {
            _isReplacingLayer = wasReplacing;
        }

        // Re-select after the list replacement, which may clear the ListBox selection. Toggling another layer's
        // visibility or lock from its row keeps the current selection.
        RestoreSelection(wasSelected ? updated : selected);

        if (!existing.GetReferencedTokens().SequenceEqual(updated.GetReferencedTokens(), StringComparer.OrdinalIgnoreCase))
        {
            // New or removed {Field} tokens show up in the field mapping and People columns straight away
            UpdateColumnMappingPlaceholders();
        }
        else if (existing.IsVisible != updated.IsVisible || existing is PhotoLayer)
        {
            RaisePrintSelectionChanged();
        }

        RequestLivePreviewUpdate();
    }

    // --- Image Operations ---

    /// <summary>
    /// The image source a layer draws in the preview: a static image's file or embedded data,
    /// or a photo layer's current record photo, falling back to its fallback image.
    /// </summary>
    public string? GetLayerImageSource(TemplateLayer? layer)
    {
        switch (layer)
        {
            case StaticImageLayer image:
                return ImageSourceLoader.TryLoad(image.ImagePath) != null ? image.ImagePath : image.Base64Data;

            case PhotoLayer photo:
                var fieldData = GetActiveRecordFieldData();
                string token = photo.SourceToken.Trim('{', '}');
                if (fieldData.TryGetValue(token, out var value) && ImageSourceLoader.TryLoad(value) != null)
                {
                    return value;
                }
                if (fieldData.TryGetValue(photo.SourceToken, out var direct) && ImageSourceLoader.TryLoad(direct) != null)
                {
                    return direct;
                }
                return photo.FallbackImagePath;

            default:
                return null;
        }
    }

    public bool TryGetLayerImageSize(TemplateLayer? layer, out int width, out int height) =>
        ImageSourceLoader.TryGetSize(GetLayerImageSource(layer), out width, out height);

    public bool LayerHasImage(TemplateLayer? layer) => TryGetLayerImageSize(layer, out _, out _);

    /// <summary>
    /// The rectangle (in card millimeters) the selected image covers before clipping to its frame.
    /// </summary>
    public Avalonia.Rect? GetSelectedImageContentRectMm()
    {
        if (SelectedLayer is not IImageLayer image || !TryGetLayerImageSize(SelectedLayer, out int w, out int h))
        {
            return null;
        }

        var frame = new SKRect((float)SelectedLayer.X, (float)SelectedLayer.Y,
            (float)(SelectedLayer.X + SelectedLayer.Width), (float)(SelectedLayer.Y + SelectedLayer.Height));
        var content = ImageLayout.ComputeContentRect(w, h, frame, image.CropMode, image.Adjustments);
        return new Avalonia.Rect(content.Left, content.Top, content.Width, content.Height);
    }

    /// <summary>
    /// Creates a new image layer from a file, sized to the image's aspect ratio and centered on the given point.
    /// </summary>
    public async Task<StaticImageLayer?> AddImageLayerFromFileAsync(string filePath, double? centerXmm = null, double? centerYmm = null)
    {
        var imported = await ImportImageAsync(filePath);
        if (imported == null)
        {
            return null;
        }

        var format = ActiveFormat;
        double maxW = Math.Min(35.0, format.WidthMm * 0.8);
        double maxH = Math.Min(35.0, format.HeightMm * 0.8);
        double aspect = (double)imported.Width / imported.Height;

        double width = maxW;
        double height = width / aspect;
        if (height > maxH)
        {
            height = maxH;
            width = height * aspect;
        }

        width = Math.Max(2.0, Math.Round(width, 1));
        height = Math.Max(2.0, Math.Round(height, 1));

        double cx = centerXmm ?? format.WidthMm / 2.0;
        double cy = centerYmm ?? format.HeightMm / 2.0;
        double x = Math.Round(Math.Clamp(cx - width / 2.0, 0.0, Math.Max(0.0, format.WidthMm - width)), 1);
        double y = Math.Round(Math.Clamp(cy - height / 2.0, 0.0, Math.Max(0.0, format.HeightMm - height)), 1);

        RecordHistory("adding the image");

        int nextZ = Template.Layers.Count > 0 ? Template.Layers.Max(l => l.ZIndex) + 1 : 1;
        var layer = new StaticImageLayer
        {
            Name = Path.GetFileNameWithoutExtension(imported.FileName),
            Base64Data = imported.DataUri,
            SourceFileName = imported.FileName,
            CropMode = PhotoCropMode.AspectFit,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ZIndex = nextZ
        };

        Template.Layers.Add(layer);
        RefreshLayersList();
        SelectedLayer = layer;
        RequestLivePreviewUpdate();
        StatusText = $"Added {imported.FileName} ({imported.Width} × {imported.Height} px). Double-click it on the card to reposition.";
        return layer;
    }

    /// <summary>
    /// Embeds a file into the selected image layer, or as the selected photo layer's fallback image.
    /// </summary>
    public async Task<bool> SetSelectedLayerImageFromFileAsync(string filePath)
    {
        if (SelectedLayer is not (StaticImageLayer or PhotoLayer))
        {
            return false;
        }

        string targetId = SelectedLayer.Id;
        var imported = await ImportImageAsync(filePath);

        // The selection or template may have changed while the file was being read
        var current = Template.Layers.FirstOrDefault(l => l.Id == targetId);
        if (imported == null || current == null)
        {
            return false;
        }

        TemplateLayer updated;
        if (current is StaticImageLayer image)
        {
            updated = image with
            {
                Base64Data = imported.DataUri,
                ImagePath = null,
                SourceFileName = imported.FileName,
                Adjustments = ImageAdjustments.None,
                Name = IsDefaultImageLayerName(image.Name) ? Path.GetFileNameWithoutExtension(imported.FileName) : image.Name
            };

            if (!image.HasImage)
            {
                // First image for a blank frame: shape the frame to the picture
                updated = WithFrameMatchedToImage(updated, imported.Width, imported.Height);
            }
        }
        else
        {
            updated = ((PhotoLayer)current) with { FallbackImagePath = imported.DataUri };
        }

        NameNextEdit("the image file");
        ReplaceLayer(current, updated);
        StatusText = current is PhotoLayer
            ? $"Fallback photo set from {imported.FileName}."
            : $"Image set to {imported.FileName} ({imported.Width} × {imported.Height} px).";
        return true;
    }

    public void ClearSelectedLayerImage()
    {
        TemplateLayer? updated = SelectedLayer switch
        {
            StaticImageLayer image => image with { Base64Data = null, ImagePath = null, SourceFileName = null },
            PhotoLayer photo => photo with { FallbackImagePath = null },
            _ => null
        };

        if (updated != null)
        {
            IsRepositioningImage = false;
            NameNextEdit("removing the image");
            ReplaceSelectedLayer(updated);
        }
    }

    /// <summary>
    /// Rotates the selected image by a quarter turn. If the frame was shaped to the image, the frame turns with it.
    /// </summary>
    public void RotateSelectedImage(int degrees)
    {
        if (SelectedLayer is not IImageLayer image)
        {
            return;
        }

        var rotated = image.Adjustments with { Rotation = image.Adjustments.NormalizedRotation + degrees };
        var updated = WithImageProperties(SelectedLayer, adjustments: rotated.Normalized());
        if (updated == null)
        {
            return;
        }

        if (degrees % 180 != 0 && TryGetLayerImageSize(SelectedLayer, out int w, out int h))
        {
            var (orientedW, orientedH) = ImageLayout.GetOrientedSize(w, h, image.Adjustments);
            double imageAspect = orientedW / orientedH;
            double frameAspect = SelectedLayer.Width / SelectedLayer.Height;
            if (Math.Abs(imageAspect - frameAspect) / imageAspect < 0.03)
            {
                double cx = SelectedLayer.X + SelectedLayer.Width / 2.0;
                double cy = SelectedLayer.Y + SelectedLayer.Height / 2.0;
                updated = updated with
                {
                    Width = SelectedLayer.Height,
                    Height = SelectedLayer.Width,
                    X = Math.Max(0.0, Math.Round(cx - SelectedLayer.Height / 2.0, 1)),
                    Y = Math.Max(0.0, Math.Round(cy - SelectedLayer.Width / 2.0, 1))
                };
            }
        }

        NameNextEdit("rotating the image");
        ReplaceSelectedLayer(updated);
    }

    public void ResetSelectedImageAdjustments()
    {
        NameNextEdit("resetting the image");
        UpdateSelectedImageLayer(adjustments: ImageAdjustments.None);
    }

    public void CenterSelectedImage()
    {
        NameNextEdit("centring the image");
        UpdateSelectedImageAdjustments(a => a with { OffsetX = 0.0, OffsetY = 0.0 });
    }

    /// <summary>
    /// Shrinks or expands the frame to the given aspect ratio (width / height) keeping its center and clamped to the card bounds.
    /// Also ensures crop mode is set to AspectFill so the cropped area fills the chosen frame ratio.
    /// </summary>
    public void ApplyCropAspectRatio(double targetAspect)
    {
        if (SelectedLayer is not IImageLayer || targetAspect <= 0.05)
        {
            return;
        }

        double cx = SelectedLayer.X + SelectedLayer.Width / 2.0;
        double cy = SelectedLayer.Y + SelectedLayer.Height / 2.0;
        double cardW = ActiveFormat.WidthMm;
        double cardH = ActiveFormat.HeightMm;

        // Choose frame size matching the target aspect ratio
        double currentArea = Math.Max(16.0, SelectedLayer.Width * SelectedLayer.Height);
        double w = Math.Sqrt(currentArea * targetAspect);
        double h = w / targetAspect;

        // Clamp to card dimensions
        if (w > cardW)
        {
            w = cardW;
            h = w / targetAspect;
        }
        if (h > cardH)
        {
            h = cardH;
            w = h * targetAspect;
        }

        w = Math.Max(4.0, Math.Round(w, 1));
        h = Math.Max(4.0, Math.Round(h, 1));
        double x = Math.Clamp(Math.Round(cx - w / 2.0, 1), 0.0, Math.Max(0.0, cardW - w));
        double y = Math.Clamp(Math.Round(cy - h / 2.0, 1), 0.0, Math.Max(0.0, cardH - h));

        var updated = WithImageProperties(SelectedLayer, cropMode: PhotoCropMode.AspectFill);
        if (updated != null)
        {
            NameNextEdit("the crop shape");
            ReplaceSelectedLayer(updated with
            {
                X = x,
                Y = y,
                Width = w,
                Height = h
            });
        }
    }

    /// <summary>
    /// Aligns the visible/cropped portion of the image to Top, Center, Bottom, Left, or Right.
    /// </summary>
    public void SetCropAnchor(string anchor)
    {
        NameNextEdit("the crop position");
        switch (anchor.ToLowerInvariant())
        {
            case "top":
                UpdateSelectedImageAdjustments(a => a with { OffsetY = -1.0 });
                break;
            case "bottom":
                UpdateSelectedImageAdjustments(a => a with { OffsetY = 1.0 });
                break;
            case "left":
                UpdateSelectedImageAdjustments(a => a with { OffsetX = -1.0 });
                break;
            case "right":
                UpdateSelectedImageAdjustments(a => a with { OffsetX = 1.0 });
                break;
            case "center":
            default:
                UpdateSelectedImageAdjustments(a => a with { OffsetX = 0.0, OffsetY = 0.0 });
                break;
        }
    }

    /// <summary>
    /// Shrinks the frame to the image's aspect ratio (keeping its center) and shows the whole image.
    /// </summary>
    public void FitFrameToSelectedImage()
    {
        if (SelectedLayer is not IImageLayer || !TryGetLayerImageSize(SelectedLayer, out int w, out int h))
        {
            return;
        }

        var reset = WithImageProperties(SelectedLayer,
            adjustments: SelectedAdjustments with { Zoom = 1.0, OffsetX = 0.0, OffsetY = 0.0 });
        if (reset != null)
        {
            NameNextEdit("fitting the frame to the image");
            ReplaceSelectedLayer(WithFrameMatchedToImage(reset, w, h));
        }
    }

    /// <summary>
    /// Applies a crop box drag: the box <em>is</em> the layer's frame, and the picture is re-anchored so it stays
    /// exactly where it sits on the card. Dragging an edge therefore reveals or hides part of the image — a real
    /// crop — rather than rescaling what is already shown.
    /// </summary>
    /// <param name="frameMm">The new frame, in card millimetres.</param>
    /// <param name="keepContentMm">
    /// Where the picture should stay, in card millimetres — normally the content rectangle captured when the drag
    /// started, so rounding during the drag can't let the image creep.
    /// </param>
    public bool SetSelectedImageCropFrame(Avalonia.Rect frameMm, Avalonia.Rect keepContentMm)
    {
        if (SelectedLayer is not IImageLayer image || SelectedLayer.IsLocked ||
            !TryGetLayerImageSize(SelectedLayer, out int imageW, out int imageH))
        {
            return false;
        }

        double x = Math.Max(0.0, Math.Round(frameMm.X, 2));
        double y = Math.Max(0.0, Math.Round(frameMm.Y, 2));
        double width = Math.Max(MinLayerSizeMm, Math.Round(frameMm.Width, 2));
        double height = Math.Max(MinLayerSizeMm, Math.Round(frameMm.Height, 2));

        var frame = new SKRect((float)x, (float)y, (float)(x + width), (float)(y + height));
        var content = new SKRect(
            (float)keepContentMm.X,
            (float)keepContentMm.Y,
            (float)(keepContentMm.X + keepContentMm.Width),
            (float)(keepContentMm.Y + keepContentMm.Height));

        var adjustments = ImageLayout.FitContentRect(imageW, imageH, frame, image.CropMode, image.Adjustments, content);
        var updated = WithImageProperties(SelectedLayer, adjustments: adjustments.Normalized());
        if (updated == null)
        {
            return false;
        }

        // One undo step for the whole drag, whether or not a given step happened to move the picture as well
        NameNextEdit("the crop", $"crop:{SelectedLayer.Id}");
        ReplaceSelectedLayer(updated with { X = x, Y = y, Width = width, Height = height });
        return true;
    }

    public void PanSelectedImage(double deltaXmm, double deltaYmm)
    {
        if (SelectedLayer is IImageLayer image && TryGetLayerImageSize(SelectedLayer, out int w, out int h))
        {
            UpdateSelectedImageLayer(adjustments: ImageLayout.Pan(w, h,
                (float)SelectedLayer.Width, (float)SelectedLayer.Height,
                image.CropMode, image.Adjustments, deltaXmm, deltaYmm));
        }
    }

    public void ZoomSelectedImage(double factor) => UpdateSelectedImageAdjustments(a => a with { Zoom = a.Zoom * factor });

    private async Task<ImportedImage?> ImportImageAsync(string filePath)
    {
        try
        {
            return await Task.Run(() => ImageSourceLoader.ImportFile(filePath));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText = $"Couldn't use {Path.GetFileName(filePath)}: {ex.Message}";
            return null;
        }
    }

    private void UpdateSelectedImageAdjustments(Func<ImageAdjustments, ImageAdjustments> change)
    {
        if (SelectedImage is { } image)
        {
            UpdateSelectedImageLayer(adjustments: change(image.Adjustments));
        }
    }

    private void UpdateSelectedImageLayer(PhotoCropMode? cropMode = null, double? borderRadius = null, ImageAdjustments? adjustments = null)
    {
        if (SelectedLayer == null)
        {
            return;
        }

        var updated = WithImageProperties(SelectedLayer, cropMode, borderRadius, adjustments?.Normalized());
        if (updated != null && !updated.Equals(SelectedLayer))
        {
            ReplaceSelectedLayer(updated);
        }
    }

    private static TemplateLayer? WithImageProperties(
        TemplateLayer layer, PhotoCropMode? cropMode = null, double? borderRadius = null, ImageAdjustments? adjustments = null)
    {
        return layer switch
        {
            StaticImageLayer image => image with
            {
                CropMode = cropMode ?? image.CropMode,
                BorderRadius = borderRadius ?? image.BorderRadius,
                Adjustments = adjustments ?? image.Adjustments
            },
            PhotoLayer photo => photo with
            {
                CropMode = cropMode ?? photo.CropMode,
                BorderRadius = borderRadius ?? photo.BorderRadius,
                Adjustments = adjustments ?? photo.Adjustments
            },
            _ => null
        };
    }

    /// <summary>
    /// Fits a frame with the image's (rotated) aspect ratio inside the layer's current frame, keeping its center.
    /// </summary>
    private static TemplateLayer WithFrameMatchedToImage(TemplateLayer layer, int imageW, int imageH)
    {
        var adjustments = (layer as IImageLayer)?.Adjustments ?? ImageAdjustments.None;
        var (orientedW, orientedH) = ImageLayout.GetOrientedSize(imageW, imageH, adjustments);
        double aspect = orientedW / orientedH;

        double width = layer.Width;
        double height = width / aspect;
        if (height > layer.Height)
        {
            height = layer.Height;
            width = height * aspect;
        }

        width = Math.Max(2.0, Math.Round(width, 1));
        height = Math.Max(2.0, Math.Round(height, 1));

        double cx = layer.X + layer.Width / 2.0;
        double cy = layer.Y + layer.Height / 2.0;
        return layer with
        {
            Width = width,
            Height = height,
            X = Math.Max(0.0, Math.Round(cx - width / 2.0, 1)),
            Y = Math.Max(0.0, Math.Round(cy - height / 2.0, 1))
        };
    }

    private static bool IsDefaultImageLayerName(string name) =>
        name.StartsWith("Image ", StringComparison.Ordinal) && int.TryParse(name.AsSpan(6), out _);

    /// <summary>
    /// The file the user uploaded for this layer (not per-record photos).
    /// </summary>
    private static string? GetUploadedImageSource(TemplateLayer? layer) => layer switch
    {
        StaticImageLayer image => ImageSourceLoader.TryLoad(image.ImagePath) != null ? image.ImagePath : image.Base64Data,
        PhotoLayer photo => photo.FallbackImagePath,
        _ => null
    };

    private void RaiseSelectedImageProperties()
    {
        OnPropertyChanged(nameof(IsImageLayerSelected));
        OnPropertyChanged(nameof(SelectedLayerHasImage));
        OnPropertyChanged(nameof(SelectedImageInfo));
        OnPropertyChanged(nameof(IsFillMode));
        OnPropertyChanged(nameof(IsFitMode));
        OnPropertyChanged(nameof(IsStretchMode));
        OnPropertyChanged(nameof(ImageZoom));
        OnPropertyChanged(nameof(ImageOffsetX));
        OnPropertyChanged(nameof(ImageOffsetY));
        OnPropertyChanged(nameof(ImageRotationText));
        OnPropertyChanged(nameof(ImageFlipHorizontal));
        OnPropertyChanged(nameof(ImageFlipVertical));
        OnPropertyChanged(nameof(ImageBrightness));
        OnPropertyChanged(nameof(ImageContrast));
        OnPropertyChanged(nameof(ImageSaturation));
        OnPropertyChanged(nameof(ImageOpacity));
        OnPropertyChanged(nameof(ImageCornerRadius));
        OnPropertyChanged(nameof(HasImageAdjustments));

        if (_isRepositioningImage && (!SelectedLayerHasImage || SelectedLayer is { IsLocked: true }))
        {
            IsRepositioningImage = false;
        }

        RefreshSelectedImageThumbnail();
    }

    private void RefreshSelectedImageThumbnail()
    {
        string? source = GetUploadedImageSource(SelectedLayer);
        if (ReferenceEquals(source, _thumbnailSource) || source == _thumbnailSource)
        {
            return;
        }

        _thumbnailSource = source;
        var previous = SelectedImageThumbnail;
        SelectedImageThumbnail = CreateThumbnail(source);
        OnPropertyChanged(nameof(HasSelectedImageThumbnail));
        previous?.Dispose();
    }

    internal static AvaloniaBitmap? CreateThumbnail(string? source, int maxSide = 160)
    {
        using var imageLease = ImageSourceLoader.AcquireLease();
        var bitmap = ImageSourceLoader.TryLoad(source);
        if (bitmap == null)
        {
            return null;
        }

        try
        {
            double scale = Math.Min(1.0, (double)maxSide / Math.Max(bitmap.Width, bitmap.Height));
            int w = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
            int h = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

            using var thumb = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(thumb))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, bitmap.Width, bitmap.Height), new SKRect(0, 0, w, h),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }

            using var image = SKImage.FromBitmap(thumb);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new AvaloniaBitmap(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImageThumbnail] {ex.Message}");
            return null;
        }
    }

    // --- Template File I/O ---

    public async Task SaveTemplateAsync(string filePath)
    {
        var template = Template;
        await _templateStorage.SaveAsync(template, filePath);
        _savedTemplateJson = _templateStorage.Serialize(template);
        StatusText = $"Template saved to {Path.GetFileName(filePath)}";
    }

    public async Task LoadTemplateAsync(string filePath)
    {
        var loaded = await _templateStorage.LoadAsync(filePath);
        ApplyTemplate(loaded);
        StatusText = $"Loaded template '{loaded.Name}'";
    }

    // --- CSV & Photo Ingestion ---

    /// <summary>
    /// Loads people from a CSV or Excel (.xlsx) roster, replacing the list or adding to the end of it when
    /// <paramref name="append"/> is set. A Photo column naming image files (absolute, or relative to the roster file)
    /// gives each person their photo.
    /// </summary>
    public async Task ImportRosterAsync(string filePath, bool append = false) =>
        await ApplyRosterTableAsync(await ReadRosterTableAsync(filePath), append);

    /// <summary>
    /// Reads a roster file into memory without touching the print list, so the import summary can show what the file
    /// holds and the user can still back out.
    /// </summary>
    /// <exception cref="NotSupportedException">The file is a legacy binary .xls workbook.</exception>
    /// <exception cref="InvalidDataException">A .xlsx file isn't a readable workbook.</exception>
    public Task<RosterTable> ReadRosterTableAsync(string filePath) =>
        Task.Run(() => _rosterImportService.ReadTable(filePath));

    /// <summary>
    /// Puts an already-read roster on the print list, adding to the end of it when <paramref name="append"/> is set
    /// and replacing it otherwise.
    /// </summary>
    public async Task ApplyRosterTableAsync(RosterTable table, bool append = false)
    {
        ArgumentNullException.ThrowIfNull(table);

        var loaded = table.Records.ToList();
        string photoColumn = GetPhotoColumnKey();
        string? rosterFolder = string.IsNullOrEmpty(table.SourcePath)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(table.SourcePath));

        await Task.Run(() => _rosterService.ResolvePhotoColumn(loaded, rosterFolder, photoColumn));

        // Photos named in the CSV win; the photo folder fills in the rest
        if (!string.IsNullOrEmpty(PhotoFolderPath) && Directory.Exists(PhotoFolderPath))
        {
            await MatchPhotosFromFolderAsync(loaded, onlyMissing: true);
        }

        CsvFilePath = table.SourcePath;
        string fileName = string.IsNullOrEmpty(table.SourceName) ? "the roster" : table.SourceName;

        if (append)
        {
            AppendRecords(loaded, showFirstAdded: false);
            StatusText = $"Added {CountText(loaded.Count, "person", "people")} from {fileName}. The list now has {People.Count}.";
        }
        else
        {
            _records = loaded;
            RebuildPeople();
            ShowRecord(0);
            StatusText = $"Loaded {loaded.Count} records from {fileName}";
        }
    }

    public async Task SelectPhotoFolderAsync(string folderPath)
    {
        PhotoFolderPath = folderPath;
        if (_records.Count == 0)
        {
            return;
        }

        int keptChosen = _records.Count(r => _manualPhotoRecords.Contains(r) && !string.IsNullOrEmpty(r.ResolvedPhotoPath));
        int matched = await MatchPhotosFromFolderAsync(_records.ToList(), onlyMissing: false);
        foreach (var person in People)
        {
            person.RefreshPhoto();
        }

        RaisePrintSelectionChanged();
        OnActiveRecordContentChanged();
        StatusText = $"Found photos for {matched} of {CountText(_records.Count, "person", "people")} in {PhotoFolderName}." +
                     (keptChosen > 0 ? $" Kept {CountText(keptChosen, "photo", "photos")} you chose yourself." : string.Empty);
    }

    /// <summary>
    /// Looks for "{EmployeeId}.jpg"-style files in the photo folder. People without a match keep the photo they have,
    /// and a photo the user picked for someone is never replaced.
    /// </summary>
    private async Task<int> MatchPhotosFromFolderAsync(IReadOnlyList<BadgeRecord> records, bool onlyMissing)
    {
        var options = new PhotoMatchingOptions
        {
            PhotoDirectory = PhotoFolderPath,
            FilenamePattern = "{" + ResolveFieldKey("EmployeeId") + "}"
        };
        var chosenByHand = new HashSet<BadgeRecord>(_manualPhotoRecords, ReferenceEqualityComparer.Instance);

        return await Task.Run(() =>
        {
            int matched = 0;
            var directoryIndex = _photoService.BuildDirectoryIndex(options.PhotoDirectory);
            foreach (var rec in records)
            {
                if (chosenByHand.Contains(rec) || (onlyMissing && !string.IsNullOrEmpty(rec.ResolvedPhotoPath)))
                {
                    continue;
                }

                var path = _photoService.ResolvePhotoPath(rec, options, directoryIndex);
                if (path != null)
                {
                    rec.ResolvedPhotoPath = path;
                    matched++;
                }
            }
            return matched;
        });
    }

    // --- People list editing ---

    /// <summary>
    /// Adds a blank person to the end of the list and shows their badge.
    /// </summary>
    public PersonItem AddPerson()
    {
        AppendRecords(new[] { CreateBlankRecord(NextRowNumber()) }, showFirstAdded: true);
        StatusText = $"Added person {People.Count}. Type their details and add a photo.";
        return People[^1];
    }

    /// <summary>
    /// The person to type into after pressing Enter on <paramref name="person"/>: the next one on the list, or a new
    /// blank person when they're last. Returns null when they're last and still blank, so Enter never piles up empty rows.
    /// </summary>
    public PersonItem? GetOrAddPersonAfter(PersonItem person)
    {
        int index = People.IndexOf(person);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 < People.Count)
        {
            return People[index + 1];
        }

        return person.IsBlank ? null : AddPerson();
    }

    /// <summary>
    /// Adds one person per image file, named from the file ("jane_smith.jpg" becomes Jane Smith).
    /// Files that aren't readable images are skipped. Returns how many people were added.
    /// </summary>
    public async Task<int> AddPeopleFromPhotosAsync(IEnumerable<string> filePaths)
    {
        var paths = filePaths.ToList();
        var readable = await Task.Run(() => paths
            .Where(ImageSourceLoader.IsReadableImageFile)
            .Select(Path.GetFullPath)
            .ToList());

        int rowNumber = NextRowNumber();
        var records = readable.Select(path =>
        {
            var record = CreateBlankRecord(rowNumber++);
            var (firstName, lastName) = RosterService.GuessNameFromFileName(path);
            record.Fields[ResolveFieldKey("FirstName")] = firstName;
            record.Fields[ResolveFieldKey("LastName")] = lastName;
            record.ResolvedPhotoPath = path;
            _manualPhotoRecords.Add(record);
            return record;
        }).ToList();

        AppendRecords(records, showFirstAdded: true);

        int skipped = paths.Count - readable.Count;
        StatusText = records.Count == 0
            ? $"None of those files are images BadgeForge can read. Use {ImageSourceLoader.SupportedFormatsDescription}."
            : $"Added {CountText(records.Count, "person", "people")} from photos" +
              (skipped > 0 ? $" ({skipped} skipped: not a readable image)" : string.Empty) +
              ". Check their names before printing.";
        return records.Count;
    }

    public void RemovePerson(PersonItem person)
    {
        int index = People.IndexOf(person);
        if (index < 0)
        {
            return;
        }

        DetachPerson(person);
        People.RemoveAt(index);
        _records.RemoveAt(index);

        int current = CurrentRecordIndex > index ? CurrentRecordIndex - 1 : CurrentRecordIndex;
        RaisePeopleListChanged();
        ShowRecord(current);
        StatusText = $"Removed {person.Name} from the list.";
    }

    /// <summary>
    /// Moves a person up (negative offset) or down the print order.
    /// </summary>
    public void MovePerson(PersonItem person, int offset)
    {
        int from = People.IndexOf(person);
        if (from < 0)
        {
            return;
        }

        int to = Math.Clamp(from + offset, 0, People.Count - 1);
        if (to == from)
        {
            return;
        }

        People.Move(from, to);
        var record = _records[from];
        _records.RemoveAt(from);
        _records.Insert(to, record);

        RaisePeopleListChanged();
        ShowRecord(to);
    }

    /// <summary>
    /// Orders the list by last name, then full name.
    /// </summary>
    public void SortPeopleByName()
    {
        var selected = SelectedPerson;
        var ordered = People
            .OrderBy(p => p.SortLastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        People.Clear();
        _records.Clear();
        foreach (var person in ordered)
        {
            People.Add(person);
            _records.Add(person.Record);
        }

        RaisePeopleListChanged();
        ShowRecord(selected == null ? 0 : People.IndexOf(selected));
        StatusText = "Sorted the list by last name.";
    }

    public void ClearPeople()
    {
        foreach (var person in People)
        {
            DetachPerson(person);
        }

        People.Clear();
        _records.Clear();
        CsvFilePath = string.Empty;
        RaisePeopleListChanged();
        ShowRecord(0);
        StatusText = "Cleared the list.";
    }

    public void SetAllPeopleIncluded(bool included)
    {
        foreach (var person in People)
        {
            person.IsIncluded = included;
        }
    }

    public async Task<bool> SetPersonPhotoAsync(PersonItem person, string filePath)
    {
        bool readable = await Task.Run(() => ImageSourceLoader.IsReadableImageFile(filePath));
        if (!readable)
        {
            StatusText = $"'{Path.GetFileName(filePath)}' isn't an image BadgeForge can read. Use {ImageSourceLoader.SupportedFormatsDescription}.";
            return false;
        }

        int index = People.IndexOf(person);
        if (index < 0)
        {
            return false;
        }

        person.SetPhoto(Path.GetFullPath(filePath));
        _manualPhotoRecords.Add(person.Record);
        if (index == CurrentRecordIndex)
        {
            OnActiveRecordContentChanged();
        }

        StatusText = $"Photo for {person.Name} set to {Path.GetFileName(filePath)}.";
        return true;
    }

    public void ClearPersonPhoto(PersonItem person)
    {
        int index = People.IndexOf(person);
        if (index < 0 || !person.HasPhoto)
        {
            return;
        }

        person.SetPhoto(null);
        _manualPhotoRecords.Remove(person.Record);
        if (index == CurrentRecordIndex)
        {
            OnActiveRecordContentChanged();
        }

        StatusText = $"Removed the photo for {person.Name}.";
    }

    /// <summary>
    /// Saves the list in print order as CSV, including each person's photo file, so it can be imported again.
    /// </summary>
    public async Task SavePeopleListAsync(string filePath)
    {
        // What you see is what you save: the CSV's columns come out in the grid's order
        var columns = GetDisplayFieldTokens().Select(ResolveFieldKey).ToList();
        var records = _records.ToList();
        await Task.Run(() => _rosterService.SaveToFile(filePath, records, columns));
        StatusText = $"Saved {CountText(records.Count, "person", "people")} to {Path.GetFileName(filePath)}.";
    }

    private BadgeRecord CreateBlankRecord(int rowNumber)
    {
        var record = new BadgeRecord { RowNumber = rowNumber, BatchIndex = _records.Count };
        foreach (var token in GetPersonFieldTokens())
        {
            record.Fields[ResolveFieldKey(token)] = string.Empty;
        }
        return record;
    }

    private int NextRowNumber() => _records.Count == 0 ? 1 : _records.Max(r => r.RowNumber) + 1;

    private void AppendRecords(IReadOnlyList<BadgeRecord> records, bool showFirstAdded)
    {
        if (records.Count == 0)
        {
            return;
        }

        bool wasEmpty = _records.Count == 0;
        int firstAdded = _records.Count;
        var tokens = GetDisplayFieldTokens();
        foreach (var record in records)
        {
            _records.Add(record);
            People.Add(CreatePersonItem(record, tokens));
        }

        RaisePeopleListChanged();
        ShowRecord(showFirstAdded || wasEmpty ? firstAdded : CurrentRecordIndex);
    }

    private void RebuildPeople()
    {
        foreach (var person in People)
        {
            DetachPerson(person);
        }

        People.Clear();
        var tokens = GetDisplayFieldTokens();
        foreach (var record in _records)
        {
            People.Add(CreatePersonItem(record, tokens));
        }

        RaisePeopleListChanged();
    }

    private PersonItem CreatePersonItem(BadgeRecord record, IReadOnlyList<string> tokens)
    {
        var person = new PersonItem(record);
        BuildCells(person, tokens);
        person.FieldsEdited += OnPersonFieldsEdited;
        person.PropertyChanged += OnPersonPropertyChanged;
        return person;
    }

    private void DetachPerson(PersonItem person)
    {
        person.FieldsEdited -= OnPersonFieldsEdited;
        person.PropertyChanged -= OnPersonPropertyChanged;
        _manualPhotoRecords.Remove(person.Record);
    }

    private void OnPersonFieldsEdited(object? sender, EventArgs e)
    {
        if (sender is PersonItem person && People.IndexOf(person) == CurrentRecordIndex)
        {
            RequestLivePreviewUpdate();
        }
    }

    private void OnPersonPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PersonItem.IsIncluded) or nameof(PersonItem.HasPhoto))
        {
            RaisePrintSelectionChanged();
        }
    }

    private void RaisePeopleListChanged()
    {
        for (int i = 0; i < People.Count; i++)
        {
            People[i].Position = i + 1;
        }

        OnPropertyChanged(nameof(TotalRecords));
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(PeopleTabText));
        RaisePrintSelectionChanged();
    }

    private void RaisePrintSelectionChanged()
    {
        OnPropertyChanged(nameof(PeopleToPrintCount));
        OnPropertyChanged(nameof(TemplateHasPhotoLayer));
        OnPropertyChanged(nameof(PeopleSummaryText));
        OnPropertyChanged(nameof(PrintPeopleButtonText));
        OnPropertyChanged(nameof(CanPrintPeople));
        OnPropertyChanged(nameof(AllPeopleIncluded));
        OnPropertyChanged(nameof(CanExportPdf));
        OnPropertyChanged(nameof(ExportPdfButtonText));
    }

    /// <summary>
    /// Shows a record's badge, refreshing even when the index is unchanged but the record behind it changed.
    /// </summary>
    private void ShowRecord(int index)
    {
        index = Math.Clamp(index, 0, Math.Max(0, _records.Count - 1));
        if (index != CurrentRecordIndex)
        {
            CurrentRecordIndex = index;
            return;
        }

        OnPropertyChanged(nameof(RecordPaginationText));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        SyncSelectedPersonToRecord();
        OnActiveRecordContentChanged();
    }

    private void OnActiveRecordContentChanged()
    {
        OnPropertyChanged(nameof(SelectedLayerHasImage));
        RequestLivePreviewUpdate();
    }

    private void SyncSelectedPersonToRecord()
    {
        var person = CurrentRecordIndex >= 0 && CurrentRecordIndex < People.Count ? People[CurrentRecordIndex] : null;
        if (SetProperty(ref _selectedPerson, person, nameof(SelectedPerson)))
        {
            OnPropertyChanged(nameof(IsPersonSelected));
            OnPropertyChanged(nameof(CanPrintSelectedPerson));
        }
    }

    // --- Live Preview Rendering (Off-UI-Thread) ---

    public void RequestLivePreviewUpdate()
    {
        // Rendered on a worker thread, so it reads its own copy of the layer list while the designer keeps editing
        var request = new PreviewRequest(Interlocked.Increment(ref _renderRevision), SnapshotPreviewTemplate(), GetActiveRecordFieldData());

        // Dragging or typing asks for a preview on every event; render one at a time and skip straight to the newest
        // request instead of starting a full-resolution render for each
        lock (_previewLock)
        {
            _pendingPreview = request;
            if (_previewWorkerRunning)
            {
                return;
            }

            _previewWorkerRunning = true;
        }

        Task.Run(RenderPendingPreviews);
    }

    private void RenderPendingPreviews()
    {
        while (true)
        {
            PreviewRequest? request;
            lock (_previewLock)
            {
                request = _pendingPreview;
                _pendingPreview = null;
                if (request == null)
                {
                    _previewWorkerRunning = false;
                    return;
                }
            }

            RenderPreview(request);
        }
    }

    private void RenderPreview(PreviewRequest request)
    {
        long revision = request.Revision;
        var template = request.Template;
        var fieldData = request.FieldData;

        {
            try
            {
                // Previews don't need the printer, so the design stays visible even if the printer can't print it
                using var skBitmap = _renderer.RenderPreview(template, fieldData);

                // Only apply if this is still the newest render request
                if (Interlocked.Read(ref _renderRevision) != revision)
                {
                    return;
                }

                // Marshal SKBitmap to Avalonia Bitmap via memory stream
                using var image = SKImage.FromBitmap(skBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var memStream = new MemoryStream(data.ToArray());
                var avaloniaBitmap = new AvaloniaBitmap(memStream);

                Dispatcher.UIThread.Post(() =>
                {
                    if (Interlocked.Read(ref _renderRevision) == revision)
                    {
                        PreviewBitmap?.Dispose();
                        PreviewBitmap = avaloniaBitmap;
                        PreviewErrorText = string.Empty;
                    }
                    else
                    {
                        avaloniaBitmap.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LivePreview] Error rendering card: {ex}");
                Dispatcher.UIThread.Post(() =>
                {
                    if (Interlocked.Read(ref _renderRevision) == revision)
                    {
                        PreviewErrorText = $"The preview couldn't be drawn: {ex.Message}";
                    }
                });
            }
        }
    }

    // --- Batch Execution ---

    private enum ErrorPromptKind
    {
        /// <summary>Nothing to resume: the message explains why printing didn't start or stopped.</summary>
        Dismiss,

        /// <summary>Paused on a fault before the card was sent; Retry tries the card again.</summary>
        Retry,

        /// <summary>The printer already took the card; the operator confirms it printed or asks for a reprint.</summary>
        OutcomeUnknown
    }

    private sealed record PrintJobInfo(
        PersonItem Person,
        string Name,
        int Position,
        string RecordId,
        IReadOnlyDictionary<string, string> Fields,
        string? PhotoPath);

    private sealed record PrintCheckResult(string? Blocker, string? Warnings);

    public string ErrorPromptTitle
    {
        get => _errorPromptTitle;
        private set => SetProperty(ref _errorPromptTitle, value);
    }

    /// <summary>
    /// The prompt is about a card the printer already took, so the choice is "it printed correctly" or "reprint".
    /// </summary>
    public bool IsErrorPromptOutcomeUnknown => _errorPromptKind == ErrorPromptKind.OutcomeUnknown;

    /// <summary>
    /// The prompt is a fault from before the card was sent, so the card can simply be tried again.
    /// </summary>
    public bool IsErrorPromptRetryable => _errorPromptKind == ErrorPromptKind.Retry;

    /// <summary>
    /// The prompt only explains why printing didn't start or stopped.
    /// </summary>
    public bool IsErrorPromptDismissable => _errorPromptKind == ErrorPromptKind.Dismiss;

    /// <summary>
    /// Why the selected printer can't print the current card, when it can't.
    /// </summary>
    public string PrinterCompatibilityMessage
    {
        get => _printerCompatibilityMessage;
        private set
        {
            if (SetProperty(ref _printerCompatibilityMessage, value))
            {
                OnPropertyChanged(nameof(CanvasNoticeText));
                OnPropertyChanged(nameof(HasCanvasNotice));
            }
        }
    }

    /// <summary>
    /// Why the live preview couldn't be drawn, when it couldn't.
    /// </summary>
    public string PreviewErrorText
    {
        get => _previewErrorText;
        private set
        {
            if (SetProperty(ref _previewErrorText, value))
            {
                OnPropertyChanged(nameof(CanvasNoticeText));
                OnPropertyChanged(nameof(HasCanvasNotice));
            }
        }
    }

    /// <summary>
    /// Problems shown over the card preview: a printer that can't print this card, or a preview that failed to draw.
    /// </summary>
    public string CanvasNoticeText => string.Join(Environment.NewLine,
        new[] { _printerCompatibilityMessage, _previewErrorText }.Where(text => !string.IsNullOrEmpty(text)));

    public bool HasCanvasNotice => CanvasNoticeText.Length > 0;

    /// <summary>
    /// Asked before printing when the pre-flight check found only warnings. Receives the warnings and returns whether
    /// to print anyway. The window replaces this with a confirmation dialog.
    /// </summary>
    public Func<string, Task<bool>> ConfirmPrintWithWarningsAsync { get; set; } = _ => Task.FromResult(true);

    /// <summary>
    /// Prints everyone ticked on the People list, from the top of the list down.
    /// </summary>
    public Task StartBatchRunAsync() => PrintPeopleAsync(People.Where(p => p.IsIncluded).ToList());

    /// <summary>
    /// Prints (or reprints) just the selected person's badge.
    /// </summary>
    public Task PrintSelectedPersonAsync() =>
        SelectedPerson == null ? Task.CompletedTask : PrintPeopleAsync(new[] { SelectedPerson });

    private async Task PrintPeopleAsync(IReadOnlyList<PersonItem> people)
    {
        if (_records.Count == 0)
        {
            StatusText = "Add people or load a CSV before printing.";
            return;
        }

        if (people.Count == 0)
        {
            StatusText = "Tick at least one person to print.";
            return;
        }

        if (!CanStartBatch)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _batchCts = cts;
        _batchGeneration++;
        SetBatchInFlight(true);

        BatchState = BatchExecutionState.Running;
        HopperPausePromptVisible = false;
        ErrorPromptVisible = false;
        StopPromptVisible = false;
        BatchProgressPercent = 0;
        StatusText = $"Checking {CountText(people.Count, "badge", "badges")} before printing…";

        try
        {
            await RunPrintJobAsync(people, cts.Token);
        }
        catch (Exception ex)
        {
            BatchState = BatchExecutionState.Idle;
            HopperPausePromptVisible = false;
            StopPromptVisible = false;
            StatusText = $"Printing stopped: {ex.Message}";
            ShowErrorPrompt(ErrorPromptKind.Dismiss, "Printing stopped", ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_batchCts, cts))
            {
                _batchCts = null;
            }

            cts.Dispose();
            SetBatchInFlight(false);
        }
    }

    private async Task RunPrintJobAsync(IReadOnlyList<PersonItem> people, CancellationToken ct)
    {
        // The run prints from its own copy of the design, so editing it meanwhile can't change queued badges
        var template = SnapshotTemplate();
        var driver = _activeDriver;

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var jobs = new List<PrintJobInfo>(people.Count);
        foreach (var person in people)
        {
            // The engine tracks cards by record ID, so a repeated ID would be skipped as already printed
            string baseId = person.Record.GetPrimaryIdentifier();
            string recordId = baseId;
            for (int n = 2; !usedIds.Add(recordId); n++)
            {
                recordId = $"{baseId}-{n}";
            }

            jobs.Add(new PrintJobInfo(person, person.Name, person.Position, recordId,
                GetRecordFieldData(person.Record), person.Record.ResolvedPhotoPath));
        }

        PrintCheckResult check;
        try
        {
            check = await Task.Run(() => CheckBeforePrintingAsync(template, driver, jobs, ct), ct);
        }
        catch (OperationCanceledException)
        {
            BatchState = BatchExecutionState.Cancelled;
            StatusText = "Printing cancelled before any badges were sent.";
            return;
        }

        if (check.Blocker != null)
        {
            BatchState = BatchExecutionState.Idle;
            StatusText = "Nothing was printed. Fix the problems shown, then print again.";
            ShowErrorPrompt(ErrorPromptKind.Dismiss, "Can't print yet", check.Blocker);
            return;
        }

        if (check.Warnings != null && !await ConfirmPrintWithWarningsAsync(check.Warnings))
        {
            BatchState = BatchExecutionState.Idle;
            StatusText = "Printing cancelled. Nothing was sent.";
            return;
        }

        if (ct.IsCancellationRequested)
        {
            BatchState = BatchExecutionState.Cancelled;
            StatusText = "Printing cancelled before any badges were sent.";
            return;
        }

        _printQueue.Clear();
        var cards = new List<BatchCardItem>(jobs.Count);
        for (int i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            job.Person.UpdateStatus(BatchRecordStatus.Pending);
            _printQueue[i + 1] = job.Person;

            cards.Add(new BatchCardItem
            {
                RecordId = job.RecordId,
                BatchIndex = i + 1,
                Label = job.Name,
                Format = template.TargetFormat,
                // Rendered as the engine reaches each card, so a long list never holds every badge image in memory
                RenderFrontAsync = token => RenderCardPngAsync(template, job.Fields, driver, token),
                Metadata = new Dictionary<string, string>
                {
                    ["RecordId"] = job.RecordId,
                    ["EmployeeId"] = job.RecordId
                }
            });
        }

        StatusText = $"Printing {CountText(cards.Count, "badge", "badges")}…";

        var request = new BatchPrintRequest
        {
            BatchRunId = Guid.NewGuid().ToString("N"),
            BatchName = template.Name,
            Cards = cards,
            Driver = driver,
            MandatoryPauseThreshold = 20
        };

        try
        {
            var summary = await _batchEngine.ExecuteBatchAsync(request, ct);
            BatchState = summary.FinalState;
            HopperPausePromptVisible = false;
            ErrorPromptVisible = false;
            StopPromptVisible = false;
            if (summary.FinalState == BatchExecutionState.Completed)
            {
                BatchProgressPercent = 100;
            }

            StatusText = summary.TerminalMessage ?? "Batch completed.";
        }
        catch (Exception ex)
        {
            BatchState = BatchExecutionState.Idle;
            HopperPausePromptVisible = false;
            StopPromptVisible = false;
            StatusText = $"Printing stopped: {ex.Message}";
            ShowErrorPrompt(ErrorPromptKind.Dismiss, "Printing stopped", ex.Message);
        }
    }

    /// <summary>
    /// Everything that must hold before the first card is sent: the printer is available and takes this card size,
    /// the pre-flight check finds no errors, and every barcode can be printed at a scannable size.
    /// </summary>
    private async Task<PrintCheckResult> CheckBeforePrintingAsync(
        TemplateDefinition template,
        ICardPrinterDriver driver,
        IReadOnlyList<PrintJobInfo> jobs,
        CancellationToken ct)
    {
        // 1. The printer is there and takes this card size
        var capabilities = await driver.ProbeCapabilitiesAsync(driver.TargetPrinterName ?? string.Empty, ct);
        string printerName = driver.TargetPrinterName ?? driver.DisplayName;
        if (capabilities.IsSimulated)
        {
            return new PrintCheckResult($"{printerName} isn't available: {DescribeUnavailable(capabilities)}.", null);
        }

        if (!capabilities.ValidateCompatibility(template.TargetFormat, out string mismatch))
        {
            return new PrintCheckResult($"{printerName} can't print {template.TargetFormat.Name} cards. {mismatch}", null);
        }

        // 2. Pre-flight: no set requirements block printing; missing data or duplicates are reported as warnings
        var records = jobs.Select((job, index) => new BadgeRecord
        {
            RowNumber = job.Position,
            BatchIndex = index,
            Fields = new Dictionary<string, string>(job.Fields, StringComparer.OrdinalIgnoreCase),
            ResolvedPhotoPath = job.PhotoPath
        }).ToList();

        var validationOptions = new PreFlightValidationOptions
        {
            EnforceRequirements = false,
            AllowMissingPhotoAsWarning = true,
            CheckDuplicates = true
        };

        var report = _preFlightService.ValidateBatch(template, records, validationOptions: validationOptions);
        var errors = report.Errors.Select(issue => DescribeIssue(issue, jobs)).ToList();

        // 3. Every barcode encodes and fits its frame at a scannable size
        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            errors.AddRange(_printRenderer.FindPrintProblems(template, job.Fields, capabilities)
                .Select(problem => $"{job.Name}: {problem}"));
        }

        if (errors.Count > 0)
        {
            return new PrintCheckResult(FormatIssueList(errors), null);
        }

        var warnings = report.Warnings.Select(issue => DescribeIssue(issue, jobs)).ToList();
        return new PrintCheckResult(null, warnings.Count > 0 ? FormatIssueList(warnings) : null);
    }

    private Task<byte[]> RenderCardPngAsync(
        TemplateDefinition template,
        IReadOnlyDictionary<string, string> fields,
        ICardPrinterDriver driver,
        CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            using var bitmap = await _printRenderer.RenderCardAsync(template, fields, driver, ct);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }, ct);
    }

    /// <summary>
    /// A copy of the template whose layer list can be read on another thread while the designer keeps editing.
    /// </summary>
    private TemplateDefinition SnapshotTemplate() => Template with
    {
        Layers = new List<TemplateLayer>(Template.Layers),
        Metadata = new Dictionary<string, string>(Template.Metadata)
    };

    /// <summary>
    /// Checks in the background whether the selected printer is available and can print the current card size,
    /// and shows why not over the preview.
    /// </summary>
    private void RefreshPrinterCompatibility()
    {
        long revision = Interlocked.Increment(ref _compatibilityRevision);
        var driver = _activeDriver;
        var format = _activeFormat;
        string printerName = _selectedPrinter;

        Task.Run(async () =>
        {
            string message;
            try
            {
                var capabilities = await driver.ProbeCapabilitiesAsync(driver.TargetPrinterName ?? printerName);
                if (capabilities.IsSimulated)
                {
                    message = $"{printerName} isn't available, so badges can't be printed: {DescribeUnavailable(capabilities)}.";
                }
                else
                {
                    message = capabilities.ValidateCompatibility(format, out string mismatch)
                        ? string.Empty
                        : $"{printerName} can't print {format.Name} cards. {mismatch}";
                }
            }
            catch (Exception ex)
            {
                message = $"Couldn't check {printerName}: {ex.Message}";
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (Interlocked.Read(ref _compatibilityRevision) == revision)
                {
                    PrinterCompatibilityMessage = message;
                }
            });
        });
    }

    private static string DescribeUnavailable(PrinterCapabilities capabilities) =>
        capabilities.RawDriverProperties.TryGetValue("UnavailableReason", out string? reason) ? reason : "no printer answered";

    private static string DescribeIssue(ValidationIssue issue, IReadOnlyList<PrintJobInfo> jobs)
    {
        string who = issue.BatchIndex is int index && index >= 0 && index < jobs.Count
            ? jobs[index].Name
            : issue.RecordIdentifier ?? "Badge";
        return $"{who}: {issue.Message}";
    }

    private static string FormatIssueList(IReadOnlyList<string> issues)
    {
        const int shown = 8;
        string list = string.Join(Environment.NewLine, issues.Take(shown).Select(issue => "• " + issue));
        return issues.Count > shown ? $"{list}{Environment.NewLine}…and {issues.Count - shown} more." : list;
    }

    private void ShowErrorPrompt(ErrorPromptKind kind, string title, string message)
    {
        _errorPromptKind = kind;
        ErrorPromptTitle = title;
        ErrorPromptMessage = message;
        ErrorPromptVisible = true;
        OnPropertyChanged(nameof(IsErrorPromptOutcomeUnknown));
        OnPropertyChanged(nameof(IsErrorPromptRetryable));
        OnPropertyChanged(nameof(IsErrorPromptDismissable));
    }

    private void SetBatchInFlight(bool inFlight)
    {
        _batchInFlight = inFlight;
        RaiseBatchCommandsChanged();
    }

    private void RaiseBatchCommandsChanged()
    {
        OnPropertyChanged(nameof(IsBatchRunning));
        OnPropertyChanged(nameof(IsBatchActive));
        OnPropertyChanged(nameof(IsBatchPausedByUser));
        OnPropertyChanged(nameof(CanStopBatchRun));
        OnPropertyChanged(nameof(CanStartBatch));
        OnPropertyChanged(nameof(CanPrintPeople));
        OnPropertyChanged(nameof(CanPrintSelectedPerson));
    }

    public async Task PauseBatchRunAsync()
    {
        if (!IsBatchRunning)
        {
            return;
        }

        await _batchEngine.PauseAsync();
        StatusText = "Pausing after the current card finishes…";
    }

    /// <summary>
    /// Stops the run on the card being printed right now, rather than after it finishes. When a card is in the
    /// printer the operator is asked whether to let it finish or eject it as it is
    /// (<see cref="FinishCurrentCardAsync"/> / <see cref="EjectCurrentCardAsync"/>); with nothing in the printer
    /// the run just ends.
    /// </summary>
    public async Task StopBatchRunAsync()
    {
        if (!_batchInFlight)
        {
            return;
        }

        StatusText = "Stopping…";

        // Before the first card is sent the engine hasn't started: the run is still rendering and pre-flighting,
        // and only its own token can stop that
        if (_batchEngine.State == BatchExecutionState.Idle)
        {
            _batchCts?.Cancel();
        }

        await _batchEngine.StopAsync();
        HopperPausePromptVisible = false;
        ErrorPromptVisible = false;
    }

    /// <summary>
    /// Answers the stop prompt: let the card in the printer finish, then stop.
    /// </summary>
    public async Task FinishCurrentCardAsync()
    {
        if (!StopPromptVisible)
        {
            return;
        }

        StopPromptVisible = false;
        StatusText = "Finishing this card, then stopping…";
        await _batchEngine.FinishCurrentCardAsync();
    }

    /// <summary>
    /// Answers the stop prompt: give up on the card in the printer, eject it as it is, and stop.
    /// </summary>
    public async Task EjectCurrentCardAsync()
    {
        if (!StopPromptVisible)
        {
            return;
        }

        StopPromptVisible = false;
        StatusText = "Ejecting the card and stopping…";
        await _batchEngine.AbortCurrentCardAsync();
    }

    public async Task ResumeBatchRunAsync()
    {
        HopperPausePromptVisible = false;
        ErrorPromptVisible = false;
        await _batchEngine.ResumeAsync();
    }

    /// <summary>
    /// Answers a "check the last card" prompt by printing that card again.
    /// </summary>
    public async Task ReprintCardAsync()
    {
        ErrorPromptVisible = false;
        await _batchEngine.ReprintCardAsync();
    }

    public void DismissErrorPrompt()
    {
        if (!IsBatchActive)
        {
            ErrorPromptVisible = false;
        }
    }

    private void OnBatchEngineStateChanged(object? sender, BatchExecutionEventArgs e)
    {
        // The engine keeps mutating its checkpoint record, so capture this moment's status before switching threads
        var record = e.CurrentRecord;
        int cardIndex = record?.BatchIndex ?? 0;
        var status = record?.Status;
        string? error = record?.ErrorMessage;
        int generation = _batchGeneration;

        Dispatcher.UIThread.Post(() =>
        {
            // Ignore stragglers from an earlier run
            if (generation != _batchGeneration)
            {
                return;
            }

            // A card's own outcome is applied even once the run has ended: the last card's result is often still
            // in flight when the run finishes, and dropping it would leave that row reading "Printing" forever
            if (status != null && _printQueue.TryGetValue(cardIndex, out var person))
            {
                person.UpdateStatus(status.Value, error);
            }

            // Everything below is about the run as a whole, which the finished run has already had the last word on
            if (!_batchInFlight)
            {
                return;
            }

            BatchState = e.CurrentState;
            StatusText = e.Message;

            if (e.TotalCards > 0)
            {
                BatchProgressPercent = (int)Math.Round((double)e.CurrentCardIndex / e.TotalCards * 100);
            }

            HopperPausePromptVisible = e.CurrentState == BatchExecutionState.PausedForHopper;
            if (HopperPausePromptVisible)
            {
                HopperPauseMessage = e.Message;
            }

            // Held on the card that is in the printer: the only answers are to let it finish or eject it
            StopPromptVisible = e.CurrentState == BatchExecutionState.PausedByUser
                                && e.PauseReason == BatchPauseReason.StopRequested;
            if (StopPromptVisible)
            {
                StopPromptMessage = e.Message;
            }

            if (e.CurrentState == BatchExecutionState.PausedOnError)
            {
                bool outcomeUnknown = e.PauseReason == BatchPauseReason.CardOutcomeUnknown;
                ShowErrorPrompt(
                    outcomeUnknown ? ErrorPromptKind.OutcomeUnknown : ErrorPromptKind.Retry,
                    outcomeUnknown ? "Check the last card" : "Printer problem",
                    e.Message);
            }
            else if (e.CurrentState == BatchExecutionState.Running)
            {
                ErrorPromptVisible = false;
            }
        });
    }
}
