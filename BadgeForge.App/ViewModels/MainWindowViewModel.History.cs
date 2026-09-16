using System;
using System.Collections.Generic;
using System.Linq;
using BadgeForge.Core.Templates.Enums;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// Undo and redo for the badge designer (Ctrl+Z / Ctrl+Y).
///
/// Every edit goes through one of a handful of choke points — ReplaceLayer for property changes, and the add,
/// delete, duplicate and reorder operations — so each of those records the design as it was just before the change.
/// Nothing else in the view model needs to know history exists.
///
/// The people list and the print run are deliberately outside this: undo belongs to the design, and quietly
/// rewriting a roster or a queued badge behind the operator's back would be worse than no undo at all.
/// </summary>
public partial class MainWindowViewModel
{
    private readonly TemplateHistory _history = new();

    /// <summary>Set while a snapshot is being restored, so restoring doesn't record itself as a new edit.</summary>
    private bool _isRestoringHistory;

    /// <summary>
    /// A name for the next layer replacement, when the operation knows better than the field-by-field diff does
    /// ("rotating the image" rather than "the crop"). Consumed by the first ReplaceLayer that follows.
    /// </summary>
    private (string Label, string? MergeKey)? _namedEdit;

    /// <summary>
    /// Names the edit the next ReplaceLayer will make, for the undo stack.
    /// </summary>
    private void NameNextEdit(string label, string? mergeKey = null) => _namedEdit = (label, mergeKey);

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    public string UndoTooltip => _history.UndoLabel is { } label ? $"Undo {label}  (Ctrl+Z)" : "Nothing to undo  (Ctrl+Z)";
    public string RedoTooltip => _history.RedoLabel is { } label ? $"Redo {label}  (Ctrl+Y)" : "Nothing to redo  (Ctrl+Y)";

    /// <summary>
    /// Steps the design back to before the last edit. Returns false when there was nothing to undo.
    /// </summary>
    public bool Undo()
    {
        // Text still being typed on the canvas is part of the design, so it counts as the edit being undone
        CommitInlineTextEdit();
        return RestoreSnapshot(_history.Undo(CaptureSnapshot(_history.UndoLabel ?? "the change")), "Undid");
    }

    /// <summary>
    /// Re-applies the edit that was last undone. Returns false when there was nothing to redo.
    /// </summary>
    public bool Redo()
    {
        CommitInlineTextEdit();
        return RestoreSnapshot(_history.Redo(CaptureSnapshot(_history.RedoLabel ?? "the change")), "Redid");
    }

    /// <summary>
    /// Ends a continuous edit (a drag, a slider) so the next one starts its own undo step. Called when the pointer
    /// is released on the canvas.
    /// </summary>
    public void EndHistoryGesture() => _history.EndGesture();

    /// <summary>
    /// The design as it stands, ready to be put on the undo stack. Layers are immutable records, so copying the
    /// list is a complete snapshot — the layers themselves are shared, not cloned.
    /// </summary>
    private TemplateSnapshot CaptureSnapshot(string label) =>
        new(Template with { Layers = new List<TemplateLayer>(Template.Layers) }, _selectedLayer?.Id, label);

    /// <summary>
    /// Records the design as it is now, before <paramref name="label"/> changes it.
    /// </summary>
    /// <param name="mergeKey">
    /// Identifies a continuous gesture so its many small edits collapse into one undo step; null for a one-off edit.
    /// </param>
    private void RecordHistory(string label, string? mergeKey = null)
    {
        if (_isRestoringHistory)
        {
            return;
        }

        _history.Record(CaptureSnapshot(label), mergeKey);
        RaiseHistoryChanged();
    }

    private bool RestoreSnapshot(TemplateSnapshot? snapshot, string verb)
    {
        if (snapshot == null)
        {
            return false;
        }

        // The layer being edited or repositioned belongs to the design that is being replaced
        CancelInlineTextEdit();
        IsRepositioningImage = false;

        _isRestoringHistory = true;
        try
        {
            Template = snapshot.Template;
            if (!Equals(_activeFormat, snapshot.Template.TargetFormat))
            {
                _activeFormat = snapshot.Template.TargetFormat;
                OnPropertyChanged(nameof(ActiveFormat));
                OnActiveFormatChanged();
            }

            RefreshLayersList();
            SelectedLayer = snapshot.SelectedLayerId == null
                ? null
                : Layers.FirstOrDefault(layer => layer.Id == snapshot.SelectedLayerId);

            // A layer that came back (or went away) may carry {Field} tokens the mapping and People columns show
            UpdateColumnMappingPlaceholders();
        }
        finally
        {
            _isRestoringHistory = false;
        }

        RaiseHistoryChanged();
        OnPropertyChanged(nameof(HasUnsavedTemplateChanges));
        RaisePrintSelectionChanged();
        RequestLivePreviewUpdate();
        StatusText = $"{verb} {snapshot.Label}.";
        return true;
    }

    private void RaiseHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoTooltip));
        OnPropertyChanged(nameof(RedoTooltip));
    }

    /// <summary>
    /// Names an edit for the undo stack, and says whether it is part of a continuous gesture.
    /// A gesture's key has to identify both the layer and the kind of change, so that letting go of a drag and
    /// immediately changing something else on the same layer stays two separate undo steps.
    /// </summary>
    private static (string Label, string? MergeKey) DescribeLayerChange(TemplateLayer before, TemplateLayer after)
    {
        string id = after.Id;
        bool moved = before.X != after.X || before.Y != after.Y;
        bool resized = before.Width != after.Width || before.Height != after.Height;
        bool reshaped = moved || resized;

        var beforeImage = before as IImageLayer;
        var afterImage = after as IImageLayer;
        bool imageAdjusted = afterImage != null && beforeImage != null &&
                             (!Equals(beforeImage.Adjustments, afterImage.Adjustments) || beforeImage.CropMode != afterImage.CropMode);

        // The crop box moves the frame and re-anchors the picture in one go; a plain resize leaves the picture alone
        if (reshaped && imageAdjusted)
        {
            return ("the crop", $"crop:{id}");
        }

        if (resized)
        {
            return ("the layer size", $"resize:{id}");
        }

        if (moved)
        {
            return ("the layer position", $"move:{id}");
        }

        if (imageAdjusted)
        {
            return ("the image", $"image:{id}");
        }

        if (before.IsVisible != after.IsVisible)
        {
            return (after.IsVisible ? "showing the layer" : "hiding the layer", null);
        }

        if (before.IsLocked != after.IsLocked)
        {
            return (after.IsLocked ? "locking the layer" : "unlocking the layer", null);
        }

        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
        {
            return ("the layer name", $"name:{id}");
        }

        if (before is TextLayer oldText && after is TextLayer newText)
        {
            if (!string.Equals(oldText.Text, newText.Text, StringComparison.Ordinal))
            {
                return ("the text", $"text:{id}");
            }

            if (oldText.FontSize != newText.FontSize)
            {
                return ("the text size", $"fontsize:{id}");
            }

            if (!string.Equals(oldText.ColorHex, newText.ColorHex, StringComparison.OrdinalIgnoreCase))
            {
                return ("the text colour", $"colour:{id}");
            }
        }

        if (before is BarcodeLayer oldCode && after is BarcodeLayer newCode &&
            !string.Equals(oldCode.ContentToken, newCode.ContentToken, StringComparison.Ordinal))
        {
            return ("the barcode content", $"text:{id}");
        }

        if (before is PhotoLayer oldPhoto && after is PhotoLayer newPhoto &&
            !string.Equals(oldPhoto.SourceToken, newPhoto.SourceToken, StringComparison.Ordinal))
        {
            return ("the photo field", $"text:{id}");
        }

        // Anything else the properties panel can change. Sliders and spinners fire many times while being dragged,
        // so these merge too — just never across a change of layer.
        return ("the layer", $"edit:{id}");
    }
}
