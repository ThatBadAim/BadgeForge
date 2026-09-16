using System;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// In-place (WYSIWYG) text editing on the badge canvas.
/// The view model owns the edit session: the canvas shows its editor while <see cref="InlineEditingLayer"/> is set and
/// pushes every keystroke into <see cref="InlineEditingText"/>. Because the draft lives here, the edit can be committed
/// from anywhere — Enter or focus loss in the editor, the selection moving, switching page — and every commit goes
/// through ReplaceLayer like any other edit, so the shared immutable layer records are never mutated in place.
/// </summary>
public partial class MainWindowViewModel
{
    private TextLayer? _inlineEditingLayer;
    private string _inlineEditingText = string.Empty;

    /// <summary>
    /// The text layer being edited directly on the canvas, or null when none is.
    /// </summary>
    public TextLayer? InlineEditingLayer
    {
        get => _inlineEditingLayer;
        private set
        {
            if (ReferenceEquals(_inlineEditingLayer, value))
            {
                return;
            }

            _inlineEditingLayer = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInlineTextEditing));
        }
    }

    public bool IsInlineTextEditing => _inlineEditingLayer != null;

    /// <summary>
    /// The text typed so far in the canvas editor; applied to the layer on commit.
    /// </summary>
    public string InlineEditingText
    {
        get => _inlineEditingText;
        set => SetProperty(ref _inlineEditingText, value ?? string.Empty);
    }

    /// <summary>
    /// Starts editing a text layer in place. Locked or hidden layers can't be edited. Editing another layer first
    /// commits the one in progress. Returns whether editing started.
    /// </summary>
    public bool BeginInlineTextEdit(TextLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        int index = FindTemplateLayer(layer);
        if (index < 0 || Template.Layers[index] is not TextLayer current || current.IsLocked || !current.IsVisible)
        {
            return false;
        }

        if (_inlineEditingLayer?.Id == current.Id)
        {
            return true;
        }

        CommitInlineTextEdit();
        IsRepositioningImage = false;
        SelectedLayer = current;

        InlineEditingText = current.Text;
        InlineEditingLayer = current;

        // Re-render without this layer, so the editor's text isn't drawn over the old rendered copy
        RequestLivePreviewUpdate();
        StatusText = "Editing text on the card. Enter saves, Shift+Enter adds a line, Esc cancels.";
        return true;
    }

    /// <summary>
    /// Ends the edit session, applying the typed text to the layer. Returns true when the layer's text changed.
    /// </summary>
    public bool CommitInlineTextEdit()
    {
        var editing = _inlineEditingLayer;
        if (editing == null)
        {
            return false;
        }

        string text = InlineEditingText;
        InlineEditingLayer = null;

        int index = FindTemplateLayer(editing);
        if (index >= 0 && Template.Layers[index] is TextLayer current && !current.IsLocked && text != current.Text)
        {
            // ReplaceLayer re-renders the preview with the layer shown again
            ReplaceLayer(current, current with { Text = text });
            StatusText = "Text updated.";
            return true;
        }

        RequestLivePreviewUpdate();
        StatusText = "Ready";
        return false;
    }

    /// <summary>
    /// Ends the edit session without changing the layer.
    /// </summary>
    public void CancelInlineTextEdit()
    {
        if (_inlineEditingLayer == null)
        {
            return;
        }

        InlineEditingLayer = null;
        InlineEditingText = string.Empty;
        RequestLivePreviewUpdate();
    }

    /// <summary>
    /// The template the live preview renders: the design, minus the layer being edited in place (the editor draws it).
    /// Printing and export use <see cref="SnapshotTemplate"/>, which never hides anything.
    /// </summary>
    private TemplateDefinition SnapshotPreviewTemplate()
    {
        var snapshot = SnapshotTemplate();
        if (_inlineEditingLayer is { } editing)
        {
            int index = snapshot.Layers.FindIndex(l => l.Id == editing.Id);
            if (index >= 0)
            {
                snapshot.Layers[index] = snapshot.Layers[index] with { IsVisible = false };
            }
        }

        return snapshot;
    }
}
