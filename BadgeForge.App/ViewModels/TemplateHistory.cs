using System;
using System.Collections.Generic;
using BadgeForge.Core.Templates.Models;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// One remembered state of the badge design: the template itself, and which layer was selected at the time so that
/// undoing puts the user back where they were rather than on some other layer.
/// </summary>
/// <param name="Label">What the change was, phrased to follow "Undo" / "Redo" in the UI (e.g. "move the layer").</param>
public sealed record TemplateSnapshot(TemplateDefinition Template, string? SelectedLayerId, string Label);

/// <summary>
/// Undo/redo for the badge designer.
///
/// Layers are immutable records and every edit swaps one for a changed copy, so a snapshot only has to copy the
/// layer <em>list</em> — never the layers themselves. That keeps a step cheap enough to take on every edit, even
/// on a card with many layers.
///
/// Continuous gestures (dragging a layer, dragging a crop handle, typing, sliding a slider) would otherwise leave
/// one undo step per mouse move. Each edit carries a merge key: while the same key keeps arriving within
/// <see cref="MergeWindow"/>, the step already on the stack — which holds the state the gesture started from —
/// is kept and no new one is pushed. <see cref="EndGesture"/> seals it when the mouse button comes up.
/// </summary>
public sealed class TemplateHistory
{
    /// <summary>How many undo steps are kept. Older ones fall off the bottom.</summary>
    public const int Capacity = 80;

    /// <summary>How long edits sharing a merge key keep folding into one undo step.</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(900);

    private readonly List<TemplateSnapshot> _undo = new();
    private readonly List<TemplateSnapshot> _redo = new();
    private readonly Func<DateTime> _clock;

    private string? _mergeKey;
    private DateTime _mergeStamp;

    public TemplateHistory(Func<DateTime>? clock = null) => _clock = clock ?? (() => DateTime.UtcNow);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>What undoing would reverse, or null when there is nothing to undo.</summary>
    public string? UndoLabel => CanUndo ? _undo[^1].Label : null;

    /// <summary>What redoing would re-apply, or null when there is nothing to redo.</summary>
    public string? RedoLabel => CanRedo ? _redo[^1].Label : null;

    public int UndoDepth => _undo.Count;

    /// <summary>
    /// Forgets everything, for a design that is being replaced wholesale (a new or opened template). Undoing across
    /// such a swap would silently resurrect layers from a file the user has closed.
    /// </summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _mergeKey = null;
    }

    /// <summary>
    /// Remembers the design as it is <em>before</em> a change is applied.
    /// </summary>
    /// <param name="before">The state to come back to.</param>
    /// <param name="mergeKey">
    /// Identifies a continuous gesture (e.g. "move:layerId"). Repeats within <see cref="MergeWindow"/> fold into the
    /// step already recorded. Null for a one-off change, which always gets its own step.
    /// </param>
    public void Record(TemplateSnapshot before, string? mergeKey = null)
    {
        // Anything redone was branched away from the moment a new edit happened
        _redo.Clear();

        var now = _clock();
        if (mergeKey != null && _undo.Count > 0 && _mergeKey == mergeKey && now - _mergeStamp <= MergeWindow)
        {
            // Still the same gesture: the step on the stack already holds where it started
            _mergeStamp = now;
            return;
        }

        _undo.Add(before);
        if (_undo.Count > Capacity)
        {
            _undo.RemoveAt(0);
        }

        _mergeKey = mergeKey;
        _mergeStamp = now;
    }

    /// <summary>
    /// Seals the gesture in progress so the next edit always starts its own undo step. Called when a drag ends or
    /// the selection moves elsewhere.
    /// </summary>
    public void EndGesture() => _mergeKey = null;

    /// <summary>
    /// Steps back: returns the state to restore, and remembers <paramref name="current"/> so it can be redone.
    /// </summary>
    public TemplateSnapshot? Undo(TemplateSnapshot current) => Step(_undo, _redo, current);

    /// <summary>
    /// Steps forward again after an undo.
    /// </summary>
    public TemplateSnapshot? Redo(TemplateSnapshot current) => Step(_redo, _undo, current);

    private TemplateSnapshot? Step(List<TemplateSnapshot> from, List<TemplateSnapshot> to, TemplateSnapshot current)
    {
        if (from.Count == 0)
        {
            return null;
        }

        var target = from[^1];
        from.RemoveAt(from.Count - 1);

        // The other side gets the state being left behind, under the same name: undoing "move the layer" leaves
        // "move the layer" to redo
        to.Add(current with { Label = target.Label });

        // A gesture can't continue across an undo
        _mergeKey = null;
        return target;
    }
}
