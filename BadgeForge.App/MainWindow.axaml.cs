using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App.ViewModels;
using BadgeForge.App.Views;
using BadgeForge.Core.Rendering.Elements;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.App;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel
        {
            ConfirmPrintWithWarningsAsync = ConfirmPrintWithWarningsAsync
        };
        DataContext = viewModel;
        BadgeCanvas.ChooseImageRequested += OnCanvasChooseImageRequested;

        // Selecting a layer brings its settings into view, even while the Data tab is open
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedLayer) && viewModel.SelectedLayer != null)
            {
                InspectorTabs.SelectedIndex = 0;
            }
        };

        // Image files dropped on a person set their photo; dropped anywhere else they add new people
        DragDrop.SetAllowDrop(PeoplePage, true);
        PeoplePage.AddHandler(DragDrop.DragEnterEvent, OnPeopleDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        PeoplePage.AddHandler(DragDrop.DragOverEvent, OnPeopleDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        PeoplePage.AddHandler(DragDrop.DragLeaveEvent, OnPeopleDragLeave, RoutingStrategies.Bubble, handledEventsToo: true);
        PeoplePage.AddHandler(DragDrop.DropEvent, OnPeopleDrop, RoutingStrategies.Bubble, handledEventsToo: true);

        // Tunnelled so Tab is handled before focus navigation moves on to the row's buttons
        PeopleList.AddHandler(KeyDownEvent, OnPeopleListKeyDown, RoutingStrategies.Tunnel);

        AddHandler(KeyDownEvent, OnDesignerKeyDown, RoutingStrategies.Tunnel);
    }

    private bool _closeConfirmed;
    private bool _closePromptOpen;

    /// <summary>
    /// Asks before closing would throw away unsaved template changes or stop a print run part-way.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || e.Cancel || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        // Text still being typed on the canvas counts as a template change
        viewModel.CommitInlineTextEdit();

        string? warning = !viewModel.CanStartBatch
            ? "Badges are still printing. Closing now stops the print run part-way."
            : viewModel.IsExporting
                ? "A PDF export is still running. Closing now stops it, and no file is saved."
            : viewModel.HasUnsavedTemplateChanges
                ? "The current template has changes that haven't been saved. They will be lost."
                : null;
        if (warning == null)
        {
            return;
        }

        e.Cancel = true;
        if (!_closePromptOpen)
        {
            _ = ConfirmCloseAsync(warning);
        }
    }

    private async Task ConfirmCloseAsync(string warning)
    {
        _closePromptOpen = true;
        try
        {
            if (await ShowDialogAsync("Close BadgeForge?", warning, "Close anyway", "Keep working"))
            {
                _closeConfirmed = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[BadgeForge] Couldn't ask before closing: {ex}");
        }
        finally
        {
            _closePromptOpen = false;
        }
    }

    /// <summary>How far an arrow key nudges the selected layer: fine by default, a whole millimetre with Shift.</summary>
    private const double NudgeStepMm = 0.1;
    private const double CoarseNudgeStepMm = 1.0;

    // Designer keyboard shortcuts: the File menu's Ctrl+N/O/S (its handlers are plain event handlers rather than
    // ICommands, so the accelerators are wired here instead of via Window.KeyBindings/InputGesture, which only
    // display gesture text for a MenuItem), undo/redo, Delete/Backspace to remove the selected layer, and the
    // arrow keys to nudge it. Tunnelled so it is seen before the layer list or the canvas act on the key.
    private void OnDesignerKeyDown(object? sender, KeyEventArgs e)
    {
        if (!ViewModel.IsDesignPageActive)
        {
            return;
        }

        // Anything typed into a text box belongs to that text box: its own undo, and its own Delete/Backspace.
        // That includes text being edited in place on the card, which is an editor laid over the layer.
        if (IsTextEntryFocused())
        {
            return;
        }

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            // Nudge the selected layer. Skipped while a list, combo box or slider has the keyboard, since those
            // steer with the arrows themselves — click the card (or a layer's row) first, then nudge.
            case Key.Left or Key.Right or Key.Up or Key.Down when !ctrl && !ArrowKeysBelongToFocus():
                if (NudgeSelectedLayer(e.Key, shift ? CoarseNudgeStepMm : NudgeStepMm))
                {
                    e.Handled = true;
                }
                break;

            // Esc leaves colour matching from anywhere, including the dropper button that started it
            case Key.Escape when ViewModel.IsPickingColor:
                ViewModel.IsPickingColor = false;
                e.Handled = true;
                break;

            // Delete and Backspace both remove the selected layer: whichever one the user reaches for works
            case Key.Delete or Key.Back when e.KeyModifiers == KeyModifiers.None:
                if (ViewModel.SelectedLayer is { IsLocked: false })
                {
                    ViewModel.DeleteSelectedLayer();
                    e.Handled = true;
                }
                break;

            case Key.Z when ctrl && !shift:
                ViewModel.Undo();
                e.Handled = true;
                break;

            // Ctrl+Y and Ctrl+Shift+Z both redo, the two conventions users arrive with
            case Key.Y when ctrl && !shift:
            case Key.Z when ctrl && shift:
                ViewModel.Redo();
                e.Handled = true;
                break;

            case Key.N when ctrl && !shift:
                OnNewDefaultTemplateClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.O when ctrl && !shift:
                OnOpenTemplateClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S when ctrl && !shift:
                OnSaveTemplateClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// True when the keyboard is currently going into a text field, which owns its own editing keys.
    /// </summary>
    private bool IsTextEntryFocused()
    {
        var focused = FocusManager?.GetFocusedElement();
        return focused is TextBox || (focused is Visual visual && visual.FindAncestorOfType<TextBox>() != null);
    }

    /// <summary>
    /// True when the focused control moves its own selection with the arrow keys (the layer list, a combo box, a
    /// slider), so nudging must leave them alone.
    /// </summary>
    private bool ArrowKeysBelongToFocus()
    {
        if (FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }

        return focused is ListBox or ComboBox or Slider
               || focused.FindAncestorOfType<ListBox>() != null
               || focused.FindAncestorOfType<ComboBox>() != null
               || focused.FindAncestorOfType<Slider>() != null;
    }

    /// <summary>
    /// Moves the selected layer by one step. Successive presses land in a single undo step, so holding an arrow
    /// down and then pressing Ctrl+Z puts the layer back where it started rather than one step back.
    /// </summary>
    private bool NudgeSelectedLayer(Key key, double stepMm)
    {
        if (ViewModel.SelectedLayer is not { IsLocked: false })
        {
            return false;
        }

        var (dx, dy) = key switch
        {
            Key.Left => (-stepMm, 0.0),
            Key.Right => (stepMm, 0.0),
            Key.Up => (0.0, -stepMm),
            Key.Down => (0.0, stepMm),
            _ => (0.0, 0.0)
        };

        ViewModel.MoveSelectedLayer(dx, dy);
        return true;
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => ViewModel.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => ViewModel.Redo();

    // --- Template File Operations ---

    private void OnNewBlankCr80Click(object? sender, RoutedEventArgs e) =>
        PromptAndApplyNewTemplate("start a blank CR80 template", () => ViewModel.NewBlankTemplate(BadgeForge.Core.Printing.Models.CardFormat.CR80));

    private void OnNewBlankCr79Click(object? sender, RoutedEventArgs e) =>
        PromptAndApplyNewTemplate("start a blank CR79 template", () => ViewModel.NewBlankTemplate(BadgeForge.Core.Printing.Models.CardFormat.CR79));

    private void OnNewBlankPortraitClick(object? sender, RoutedEventArgs e) =>
        PromptAndApplyNewTemplate("start a blank portrait template", () => ViewModel.NewBlankTemplate(BadgeForge.Core.Printing.Models.CardFormat.CR80.WithOrientation(true)));

    private void OnNewDefaultTemplateClick(object? sender, RoutedEventArgs e) =>
        PromptAndApplyNewTemplate("start a starter template", () => ViewModel.NewTemplate());

    private void PromptAndApplyNewTemplate(string actionDescription, Action apply)
    {
        _ = RunSafelyAsync(actionDescription, async () =>
        {
            if (await ConfirmDiscardTemplateChangesAsync("Start a new template"))
            {
                apply();
            }
        });
    }

    private async void OnOpenTemplateClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("open the template", async () =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null || !await ConfirmDiscardTemplateChangesAsync("Open another template")) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Badge Template",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Badge Template (*.json)") { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count > 0)
            {
                await ViewModel.LoadTemplateAsync(files[0].Path.LocalPath);
            }
        });
    }

    private async void OnSaveTemplateClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("save the template", async () =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Badge Template",
                DefaultExtension = "json",
                SuggestedFileName = $"{SafeFileName(ViewModel.Template.Name, "badge_template")}.json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Badge Template (*.json)") { Patterns = new[] { "*.json" } }
                }
            });

            if (file != null)
            {
                await ViewModel.SaveTemplateAsync(file.Path.LocalPath);
            }
        });
    }

    /// <summary>
    /// A file name based on free text such as a template name, which can hold characters a file name can't
    /// (e.g. "Staff / Visitors").
    /// </summary>
    private static string SafeFileName(string text, string fallback)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
        string baseName = new string(text.Select(c => c == ' ' || invalid.Contains(c) ? '_' : c).ToArray()).Trim('_', '.');
        return baseName.Length > 0 ? baseName : fallback;
    }

    // --- Add Layer Toolbar ---

    private void OnAddTextClick(object? sender, RoutedEventArgs e) => ViewModel.AddTextLayer();
    private void OnAddPhotoClick(object? sender, RoutedEventArgs e) => ViewModel.AddPhotoLayer();
    private void OnAddBarcodeClick(object? sender, RoutedEventArgs e) => ViewModel.AddBarcodeLayer(symbology: BarcodeSymbology.Code128);
    private void OnAddQrClick(object? sender, RoutedEventArgs e) => ViewModel.AddBarcodeLayer(symbology: BarcodeSymbology.QrCode);

    private void OnAddDataFieldClick(object? sender, RoutedEventArgs e)
    {
        // Tag is "<kind>:<token>", e.g. "Text:FullName" or "QrCode:EmployeeId"
        if (sender is MenuItem { Tag: string tag } && tag.Split(':', 2) is [var kind, var token]
            && Enum.TryParse<DataFieldKind>(kind, out var fieldKind))
        {
            ViewModel.AddDataFieldLayer(fieldKind, token);
        }
    }

    private async void OnAddImageClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("add the image", async () =>
        {
            string? path = await PickImageFileAsync("Add an image");
            if (path != null)
            {
                await ViewModel.AddImageLayerFromFileAsync(path);
            }
        });
    }

    private void OnOpenPrintListClick(object? sender, RoutedEventArgs e) => ViewModel.IsPeoplePageActive = true;

    private void OnDuplicateLayerClick(object? sender, RoutedEventArgs e) => ViewModel.DuplicateSelectedLayer();
    private void OnDeleteLayerClick(object? sender, RoutedEventArgs e) => ViewModel.DeleteSelectedLayer();

    // --- Per-row layer list actions ---

    private static TemplateLayer? LayerFromRow(object? sender) =>
        (sender as Control)?.DataContext as TemplateLayer;

    private void OnToggleLayerVisibilityClick(object? sender, RoutedEventArgs e)
    {
        if (LayerFromRow(sender) is { } layer)
        {
            ViewModel.ToggleLayerVisibility(layer);
        }
    }

    private void OnToggleLayerLockClick(object? sender, RoutedEventArgs e)
    {
        if (LayerFromRow(sender) is { } layer)
        {
            ViewModel.ToggleLayerLock(layer);
        }
    }

    private void OnMoveLayerUpClick(object? sender, RoutedEventArgs e)
    {
        if (LayerFromRow(sender) is { } layer)
        {
            ViewModel.MoveLayerStackOrder(layer, +1);
        }
    }

    private void OnMoveLayerDownClick(object? sender, RoutedEventArgs e)
    {
        if (LayerFromRow(sender) is { } layer)
        {
            ViewModel.MoveLayerStackOrder(layer, -1);
        }
    }

    private void OnDuplicateLayerRowClick(object? sender, RoutedEventArgs e)
    {
        if (LayerFromRow(sender) is { } layer)
        {
            ViewModel.DuplicateLayer(layer);
        }
    }

    private void OnDeleteLayerRowClick(object? sender, RoutedEventArgs e)
    {
        if (LayerFromRow(sender) is { } layer)
        {
            ViewModel.DeleteLayer(layer);
        }
    }

    // --- Image Editing ---

    private void OnChooseImageClick(object? sender, RoutedEventArgs e) => _ = RunSafelyAsync("use that image", ChooseImageForSelectedLayerAsync);
    private void OnCanvasChooseImageRequested(object? sender, EventArgs e) => _ = RunSafelyAsync("use that image", ChooseImageForSelectedLayerAsync);

    private async Task ChooseImageForSelectedLayerAsync()
    {
        string title = ViewModel.IsPhotoLayerSelected ? "Choose a fallback photo" : "Choose an image";
        string? path = await PickImageFileAsync(title);
        if (path != null)
        {
            await ViewModel.SetSelectedLayerImageFromFileAsync(path);
        }
    }

    private void OnClearImageClick(object? sender, RoutedEventArgs e) => ViewModel.ClearSelectedLayerImage();
    private void OnRotateLeftClick(object? sender, RoutedEventArgs e) => ViewModel.RotateSelectedImage(-90);
    private void OnRotateRightClick(object? sender, RoutedEventArgs e) => ViewModel.RotateSelectedImage(90);
    private void OnFitFrameToImageClick(object? sender, RoutedEventArgs e) => ViewModel.FitFrameToSelectedImage();
    private void OnResetImageAdjustmentsClick(object? sender, RoutedEventArgs e) => ViewModel.ResetSelectedImageAdjustments();

    private void OnCropRatioClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag } && double.TryParse(tag, System.Globalization.CultureInfo.InvariantCulture, out double ratio))
        {
            ViewModel.ApplyCropAspectRatio(ratio);
        }
    }

    private void OnCropAnchorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string anchor })
        {
            ViewModel.SetCropAnchor(anchor);
        }
    }

    private async Task<string?> PickImageFileAsync(string title) =>
        (await PickImageFilesAsync(title, allowMultiple: false)).FirstOrDefault();

    private async Task<List<string>> PickImageFilesAsync(string title, bool allowMultiple)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return new List<string>();

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = new[]
            {
                new FilePickerFileType($"Images ({ImageSourceLoader.SupportedFormatsDescription})")
                {
                    Patterns = ImageSourceLoader.SupportedExtensions.Select(ext => "*" + ext).ToArray(),
                    MimeTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/bmp", "image/gif" }
                },
                FilePickerFileTypes.All
            }
        });

        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    private void OnTextColorSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TextColorSwatch swatch })
        {
            ViewModel.TextColorHex = swatch.Hex;
        }
    }

    private void OnTextColorHexKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
        {
            ViewModel.TextColorHex = box.Text ?? string.Empty;
            e.Handled = true;
        }
    }

    // --- Zoom Controls ---

    private void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        BadgeCanvas.Zoom = Math.Min(3.0, Math.Round(BadgeCanvas.Zoom + 0.15, 2));
    }

    private void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        BadgeCanvas.Zoom = Math.Max(0.4, Math.Round(BadgeCanvas.Zoom - 0.15, 2));
    }

    private void OnZoomResetClick(object? sender, RoutedEventArgs e)
    {
        BadgeCanvas.ResetView();
    }

    // --- Record Pagination ---

    private void OnFirstRecordClick(object? sender, RoutedEventArgs e) => ViewModel.FirstRecord();
    private void OnPrevRecordClick(object? sender, RoutedEventArgs e) => ViewModel.PreviousRecord();
    private void OnNextRecordClick(object? sender, RoutedEventArgs e) => ViewModel.NextRecord();
    private void OnLastRecordClick(object? sender, RoutedEventArgs e) => ViewModel.LastRecord();

    // --- Data Ingestion ---

    private async void OnBrowseCsvClick(object? sender, RoutedEventArgs e) =>
        await RunSafelyAsync("load the roster", ImportRosterWithSummaryAsync);

    /// <summary>
    /// The whole import flow: pick a file, read it without disturbing the list, show what was found in it, and only
    /// then append it or replace the list with it.
    /// </summary>
    private async Task ImportRosterWithSummaryAsync()
    {
        string? path = await PickRosterFileAsync();
        if (path == null)
        {
            return;
        }

        var table = await ViewModel.ReadRosterTableAsync(path);
        if (table.ColumnCount == 0)
        {
            await ShowDialogAsync(
                "Nothing to import",
                $"No columns were found in {System.IO.Path.GetFileName(path)}. The first row that isn't empty has to " +
                "hold the column headings.",
                "OK");
            return;
        }

        var summary = new ImportSummaryViewModel(table, ViewModel.People.Count);
        var choice = await ImportSummaryDialog.AskAsync(this, summary);
        if (choice != ImportChoice.Cancel)
        {
            await ViewModel.ApplyRosterTableAsync(table, append: choice == ImportChoice.Append);
        }
    }

    private async Task<string?> PickRosterFileAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a Roster (CSV or Excel)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Roster (*.csv, *.xlsx)") { Patterns = new[] { "*.csv", "*.xlsx" } },
                new FilePickerFileType("CSV Spreadsheet (*.csv)") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("Excel Workbook (*.xlsx)") { Patterns = new[] { "*.xlsx" } }
            }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async void OnBrowsePhotosClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("match photos from that folder", async () =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Employee Photos Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                await ViewModel.SelectPhotoFolderAsync(folders[0].Path.LocalPath);
            }
        });
    }

    // --- People Page ---

    private void OnAddPersonClick(object? sender, RoutedEventArgs e)
    {
        // Put the cursor in the new person's first field so their details can be typed straight away
        FocusPersonField(ViewModel.AddPerson(), 0);
    }

    /// <summary>
    /// Spreadsheet-style entry: Tab and Shift+Tab step through the fields (continuing onto the next or previous
    /// person), Enter goes to the next person's first field, adding a person at the end, and Shift+Enter goes back up.
    /// </summary>
    private void OnPeopleListKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is not { DataContext: PersonFieldCell cell })
        {
            return;
        }

        var person = cell.Owner;
        int column = person.Cells.IndexOf(cell);
        int row = ViewModel.People.IndexOf(person);
        bool shift = e.KeyModifiers == KeyModifiers.Shift;
        if (column < 0 || row < 0 || (e.KeyModifiers != KeyModifiers.None && !shift))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Tab when !shift && column + 1 < person.Cells.Count:
                e.Handled = FocusPersonField(person, column + 1);
                break;
            case Key.Tab when !shift && row + 1 < ViewModel.People.Count:
                e.Handled = FocusPersonField(ViewModel.People[row + 1], 0);
                break;
            case Key.Tab when shift && column > 0:
                e.Handled = FocusPersonField(person, column - 1);
                break;
            case Key.Tab when shift && row > 0:
                var previous = ViewModel.People[row - 1];
                e.Handled = FocusPersonField(previous, previous.Cells.Count - 1);
                break;
            case Key.Enter when !shift:
                // At the end of the list and still blank: stay put rather than adding another empty person
                e.Handled = FocusPersonField(ViewModel.GetOrAddPersonAfter(person) ?? person, 0);
                break;
            case Key.Enter when shift && row > 0:
                e.Handled = FocusPersonField(ViewModel.People[row - 1], 0);
                break;
        }
    }

    /// <summary>
    /// Scrolls to a person and puts the cursor at the end of one of their fields. Returns false if they have no such field.
    /// </summary>
    private bool FocusPersonField(PersonItem person, int column)
    {
        if (column < 0 || column >= person.Cells.Count)
        {
            return false;
        }

        var cell = person.Cells[column];
        PeopleList.ScrollIntoView(person);
        if (!TryFocus())
        {
            // A row that has just been added or scrolled to may not be laid out yet
            Dispatcher.UIThread.Post(() => TryFocus(), DispatcherPriority.Loaded);
        }

        return true;

        bool TryFocus()
        {
            var box = PeopleList.ContainerFromItem(person)?
                .GetVisualDescendants().OfType<TextBox>()
                .FirstOrDefault(b => b.DataContext == cell);
            if (box == null || !box.Focus(NavigationMethod.Tab))
            {
                return false;
            }

            box.CaretIndex = box.Text?.Length ?? 0;
            box.ClearSelection();
            return true;
        }
    }

    private async void OnAddPeopleFromPhotosClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("add people from those photos", async () =>
        {
            var paths = await PickImageFilesAsync("Add people from photos", allowMultiple: true);
            if (paths.Count > 0)
            {
                await ViewModel.AddPeopleFromPhotosAsync(paths);
            }
        });
    }

    private async void OnImportRosterClick(object? sender, RoutedEventArgs e) =>
        await RunSafelyAsync("import the roster", ImportRosterWithSummaryAsync);

    // --- People column order (display only) ---

    // How far the pointer has to travel before a press on a heading becomes a drag rather than a click
    private const double ColumnDragThreshold = 4;

    private PeopleFieldColumn? _draggedColumn;
    private double _columnDragOriginX;
    private bool _columnDragActive;

    private void OnColumnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control header
            || header.DataContext is not PeopleFieldColumn column
            || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed
            || !ViewModel.CanReorderPeopleColumns)
        {
            return;
        }

        _draggedColumn = column;
        _columnDragOriginX = e.GetPosition(PeopleColumnHeaders).X;
        _columnDragActive = false;

        // Captured on the headings strip rather than the heading itself: the heading moves out from under the
        // pointer as soon as the drag reorders the columns
        e.Pointer.Capture(PeopleColumnHeaders);
    }

    private void OnColumnHeadersPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedColumn == null)
        {
            return;
        }

        double x = e.GetPosition(PeopleColumnHeaders).X;
        if (!_columnDragActive)
        {
            if (Math.Abs(x - _columnDragOriginX) < ColumnDragThreshold)
            {
                return;
            }

            _columnDragActive = true;
        }

        ViewModel.MovePeopleColumn(_draggedColumn, ColumnSlotAt(x));
        MarkDraggedHeader();
    }

    private void OnColumnHeadersPointerReleased(object? sender, PointerReleasedEventArgs e) => EndColumnDrag();

    private void OnColumnHeadersPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndColumnDrag();

    /// <summary>
    /// Which column the pointer is over. The headings share the strip evenly, so its share of the width is the slot.
    /// </summary>
    private int ColumnSlotAt(double x)
    {
        int count = ViewModel.PeopleFieldColumns.Count;
        double width = PeopleColumnHeaders.Bounds.Width;
        return count <= 1 || width <= 0 ? 0 : Math.Clamp((int)(x / (width / count)), 0, count - 1);
    }

    private void EndColumnDrag()
    {
        if (_columnDragActive && _draggedColumn != null)
        {
            int position = ViewModel.PeopleFieldColumns.IndexOf(_draggedColumn) + 1;
            ViewModel.StatusText = $"{_draggedColumn.Label} is now column {position}. " +
                                   "Column order only changes this list — badges still print the same fields.";
        }

        _draggedColumn = null;
        _columnDragActive = false;
        MarkDraggedHeader();
    }

    /// <summary>
    /// Highlights the heading being dragged, and only that one.
    /// </summary>
    private void MarkDraggedHeader()
    {
        int dragged = _columnDragActive && _draggedColumn != null
            ? ViewModel.PeopleFieldColumns.IndexOf(_draggedColumn)
            : -1;

        for (int i = 0; i < ViewModel.PeopleFieldColumns.Count; i++)
        {
            if (PeopleColumnHeaders.ContainerFromIndex(i) is not Control container)
            {
                continue;
            }

            if (i == dragged)
            {
                container.Classes.Add("dragging");
            }
            else
            {
                container.Classes.Remove("dragging");
            }
        }
    }

    private void OnMoveColumnLeftClick(object? sender, RoutedEventArgs e) => MoveColumnFromMenu(sender, -1);

    private void OnMoveColumnRightClick(object? sender, RoutedEventArgs e) => MoveColumnFromMenu(sender, 1);

    private void OnResetColumnOrderClick(object? sender, RoutedEventArgs e) => ViewModel.ResetPeopleColumnOrder();

    private void MoveColumnFromMenu(object? sender, int offset)
    {
        if (sender is MenuItem { DataContext: PeopleFieldColumn column })
        {
            ViewModel.MovePeopleColumnBy(column, offset);
        }
    }

    // --- Batch selection and PDF export ---

    private void OnInvertPeopleSelectionClick(object? sender, RoutedEventArgs e) => ViewModel.InvertPeopleSelection();
    private void OnCancelExportClick(object? sender, RoutedEventArgs e) => ViewModel.CancelExport();

    private async void OnExportPdfClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("export the badges", async () =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Badges to PDF",
                DefaultExtension = "pdf",
                SuggestedFileName = $"{SafeFileName(ViewModel.Template.Name, "badges")}.pdf",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PDF Document (*.pdf)") { Patterns = new[] { "*.pdf" }, MimeTypes = new[] { "application/pdf" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (path == null) return;

            // Rendering runs off the UI thread; the progress bar and "Rendering n of N…" update meanwhile
            var result = await ViewModel.ExportSelectedToPdfAsync(path);
            if (result is { Warnings.Count: > 0 })
            {
                const int shown = 12;
                string list = string.Join(Environment.NewLine, result.Warnings.Take(shown).Select(w => "• " + w));
                if (result.Warnings.Count > shown)
                {
                    list += $"{Environment.NewLine}…and {result.Warnings.Count - shown} more.";
                }

                await ShowDialogAsync("Exported with warnings",
                    $"The PDF was saved, but some badges differ from the design:{Environment.NewLine}{Environment.NewLine}{list}", "OK");
            }
        });
    }

    private async void OnSavePeopleListClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("save the people list", async () =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save People List",
                DefaultExtension = "csv",
                SuggestedFileName = "people.csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Spreadsheet (*.csv)") { Patterns = new[] { "*.csv" } }
                }
            });

            string? path = file?.TryGetLocalPath();
            if (path != null)
            {
                await ViewModel.SavePeopleListAsync(path);
            }
        });
    }

    private void OnSortPeopleClick(object? sender, RoutedEventArgs e) => ViewModel.SortPeopleByName();

    private async void OnClearPeopleClick(object? sender, RoutedEventArgs e)
    {
        if (await ShowDialogAsync("Clear the print list?",
                "Everyone on the print list will be removed. This can't be undone.",
                "Remove everyone", "Cancel"))
        {
            ViewModel.ClearPeople();
        }
    }

    private async void OnPersonPhotoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PersonItem person })
        {
            ViewModel.SelectedPerson = person;
            await RunSafelyAsync("set the photo", () => ChoosePhotoForPersonAsync(person));
        }
    }

    private async void OnChooseSelectedPersonPhotoClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPerson is { } person)
        {
            await RunSafelyAsync("set the photo", () => ChoosePhotoForPersonAsync(person));
        }
    }

    private async Task ChoosePhotoForPersonAsync(PersonItem person)
    {
        string? path = await PickImageFileAsync($"Choose a photo for {person.Name}");
        if (path != null)
        {
            await ViewModel.SetPersonPhotoAsync(person, path);
        }
    }

    private void OnRemoveSelectedPersonPhotoClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPerson is { } person)
        {
            ViewModel.ClearPersonPhoto(person);
        }
    }

    private void OnRemovePersonPhotoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PersonItem person }) ViewModel.ClearPersonPhoto(person);
    }

    private void OnMovePersonUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PersonItem person }) ViewModel.MovePerson(person, -1);
    }

    private void OnMovePersonDownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PersonItem person }) ViewModel.MovePerson(person, 1);
    }

    private void OnRemovePersonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PersonItem person }) ViewModel.RemovePerson(person);
    }

    private void OnPersonFieldGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PersonFieldCell cell })
        {
            ViewModel.SelectedPerson = cell.Owner;
        }
    }

    private async void OnPrintSelectedPersonClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("print the badge", ViewModel.PrintSelectedPersonAsync);
    }

    private const string DropTargetClass = "dropTarget";
    private Control? _dropHighlight;

    private void OnPeopleDragOver(object? sender, DragEventArgs e)
    {
        bool hasFiles = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        // Light up the photo box (or preview) the image will land on
        var target = hasFiles ? FindDropTarget(e.Source) : default;
        SetDropHighlight(target.Person != null ? target.Highlight : null);
    }

    private void OnPeopleDragLeave(object? sender, DragEventArgs e) => SetDropHighlight(null);

    private void SetDropHighlight(Control? control)
    {
        if (control == _dropHighlight)
        {
            return;
        }

        _dropHighlight?.Classes.Remove(DropTargetClass);
        _dropHighlight = control;
        _dropHighlight?.Classes.Add(DropTargetClass);
    }

    private async void OnPeopleDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        SetDropHighlight(null);
        var target = FindDropTarget(e.Source);
        await RunSafelyAsync("use the dropped photos", async () =>
        {
            var paths = e.DataTransfer.TryGetFiles()?
                .Select(f => f.TryGetLocalPath())
                .OfType<string>()
                .Where(ImageSourceLoader.HasSupportedExtension)
                .ToList();

            if (paths is not { Count: > 0 })
            {
                ViewModel.StatusText = $"Drop image files ({ImageSourceLoader.SupportedFormatsDescription}) to add photos.";
                return;
            }

            // Several images dropped on a row's text add a person each; on a photo box the first one is used
            if (target.Person is { } person && (paths.Count == 1 || target.OnPhoto))
            {
                ViewModel.SelectedPerson = person;
                await ViewModel.SetPersonPhotoAsync(person, paths[0]);
            }
            else
            {
                await ViewModel.AddPeopleFromPhotosAsync(paths);
            }
        });
    }

    /// <summary>
    /// Where an image is being dropped: a person's photo box or row, or the badge preview (the selected person).
    /// <c>Highlight</c> is the photo box or preview to light up; <c>OnPhoto</c> is set when it is directly under the pointer.
    /// </summary>
    private (PersonItem? Person, Control? Highlight, bool OnPhoto) FindDropTarget(object? source)
    {
        for (var visual = source as Visual; visual != null && visual != PeoplePage; visual = visual.GetVisualParent())
        {
            if (visual == PeoplePreviewPanel)
            {
                return (ViewModel.SelectedPerson, PeoplePreviewDropZone, IsInside(source, PeoplePreviewDropZone));
            }

            if (visual is ListBoxItem { DataContext: PersonItem person } row)
            {
                var photoBox = row.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("personPhoto"));
                return (person, photoBox, photoBox != null && IsInside(source, photoBox));
            }
        }

        return default;
    }

    private static bool IsInside(object? source, Visual ancestor) =>
        source is Visual visual && (visual == ancestor || ancestor.IsVisualAncestorOf(visual));

    // --- Batch Execution Controls ---

    private async void OnStartBatchClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("start printing", ViewModel.StartBatchRunAsync);
    }

    private async void OnPauseBatchClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("pause printing", ViewModel.PauseBatchRunAsync);
    }

    private async void OnResumeBatchClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("resume printing", ViewModel.ResumeBatchRunAsync);
    }

    private async void OnReprintCardClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("reprint the card", ViewModel.ReprintCardAsync);
    }

    private void OnDismissErrorPromptClick(object? sender, RoutedEventArgs e) => ViewModel.DismissErrorPrompt();

    private async void OnStopBatchClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("stop printing", ViewModel.StopBatchRunAsync);
    }

    private async void OnFinishCurrentCardClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("finish the current card", ViewModel.FinishCurrentCardAsync);
    }

    private async void OnEjectCurrentCardClick(object? sender, RoutedEventArgs e)
    {
        await RunSafelyAsync("eject the current card", ViewModel.EjectCurrentCardAsync);
    }

    // --- Dialogs and error handling ---

    /// <summary>
    /// Runs a UI action, reporting any failure to the user instead of letting it crash the app (unhandled exceptions
    /// in async event handlers end the process).
    /// </summary>
    private async Task RunSafelyAsync(string action, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[BadgeForge] Couldn't {action}: {ex}");
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.StatusText = $"Couldn't {action}: {ex.Message}";
            }

            try
            {
                await ShowDialogAsync($"Couldn't {action}", ex.Message, "OK");
            }
            catch (Exception dialogError)
            {
                Trace.TraceError($"[BadgeForge] Couldn't show the error dialog: {dialogError}");
            }
        }
    }

    private Task<bool> ConfirmDiscardTemplateChangesAsync(string actionLabel) =>
        ViewModel.HasUnsavedTemplateChanges
            ? ShowDialogAsync("Unsaved template changes",
                "The current template has changes that haven't been saved. They will be lost.",
                actionLabel, "Keep editing")
            : Task.FromResult(true);

    private Task<bool> ConfirmPrintWithWarningsAsync(string warnings) =>
        ShowDialogAsync("Print with warnings?", $"{warnings}{Environment.NewLine}{Environment.NewLine}Print anyway?", "Print anyway", "Don't print");

    /// <summary>
    /// Shows a small modal message. Returns true when the confirm button was pressed. Without cancel text only the
    /// confirm button is offered.
    /// </summary>
    private async Task<bool> ShowDialogAsync(string title, string message, string confirmText, string? cancelText = null)
    {
        bool confirmed = false;
        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        if (cancelText != null)
        {
            var cancelButton = new Button { Content = cancelText, MinWidth = 96, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancelButton.Click += (_, _) => dialog.Close();
            buttons.Children.Add(cancelButton);
        }

        var confirmButton = new Button { Content = confirmText, MinWidth = 96, HorizontalContentAlignment = HorizontalAlignment.Center };
        confirmButton.Classes.Add("primary");
        confirmButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        buttons.Children.Add(confirmButton);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons
            }
        };

        await dialog.ShowDialog(this);
        return confirmed;
    }
}
