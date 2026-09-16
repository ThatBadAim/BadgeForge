using System;
using System.Collections.Generic;
using System.Linq;

namespace BadgeForge.App.ViewModels;

/// <summary>
/// The order the People grid shows its columns in — and nothing else.
/// </summary>
/// <remarks>
/// This is pure view state, deliberately kept out of the data and template layers. A column is identified by its
/// template token, the same key <see cref="PersonFieldCell.Key"/> resolves against, so dragging a column somewhere
/// else moves a screen position only: no record field is renamed or moved, no template layer's <c>{{Token}}</c>
/// changes, and <c>TemplateBinder</c> binds exactly what it bound before. Nothing here is ever written back to a
/// <c>BadgeRecord</c> or a <c>TemplateDefinition</c>.
/// <para>
/// The remembered order outlives the columns themselves: editing a layer's text makes its token vanish and come back,
/// and a token that returns should return to where the user put it rather than to the end of the row.
/// </para>
/// </remarks>
public sealed class PeopleColumnLayout
{
    // Tokens in the user's order. May name tokens the template no longer has; they cost one string each and let a
    // token that comes back land where it was left
    private readonly List<string> _order = new();

    /// <summary>
    /// Whether the user has moved a column, i.e. the grid no longer follows the template's own field order.
    /// </summary>
    public bool IsCustomised => _order.Count > 0;

    /// <summary>
    /// Puts <paramref name="tokens"/> into display order: remembered columns first, in the order the user left them,
    /// then anything new in the order the template supplied it. The result is always the same set that came in —
    /// this orders columns, it never adds or hides one.
    /// </summary>
    public IReadOnlyList<string> Arrange(IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (_order.Count == 0 || tokens.Count < 2)
        {
            return tokens;
        }

        var remaining = new List<string>(tokens);
        var arranged = new List<string>(tokens.Count);

        foreach (string token in _order)
        {
            int index = remaining.FindIndex(t => Same(t, token));
            if (index >= 0)
            {
                arranged.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
        }

        arranged.AddRange(remaining);
        return arranged;
    }

    /// <summary>
    /// Moves <paramref name="token"/> to <paramref name="targetIndex"/> within the current display order and
    /// remembers the result. Returns the move actually made as (from, to), or null when nothing moved.
    /// </summary>
    /// <param name="tokens">The tokens currently displayed, in any order; display order is derived from them.</param>
    public (int From, int To)? Move(IReadOnlyList<string> tokens, string token, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var arranged = Arrange(tokens).ToList();
        int from = arranged.FindIndex(t => Same(t, token));
        if (from < 0)
        {
            return null;
        }

        int to = Math.Clamp(targetIndex, 0, arranged.Count - 1);
        if (to == from)
        {
            return null;
        }

        string moved = arranged[from];
        arranged.RemoveAt(from);
        arranged.Insert(to, moved);
        Remember(arranged);
        return (from, to);
    }

    /// <summary>
    /// Forgets the custom order, so the grid follows the template's field order again.
    /// </summary>
    public void Reset() => _order.Clear();

    /// <summary>
    /// Records a new order, keeping any remembered token that isn't currently on screen next to the column it
    /// used to follow.
    /// </summary>
    private void Remember(IReadOnlyList<string> arranged)
    {
        var absent = _order.Where(t => !arranged.Any(a => Same(a, t))).ToList();
        if (absent.Count == 0)
        {
            _order.Clear();
            _order.AddRange(arranged);
            return;
        }

        // Rebuild by walking the old order, substituting the new order wherever a present token appears, so
        // tokens that are off screen keep their neighbours
        var rebuilt = new List<string>(_order.Count + arranged.Count);
        var queue = new Queue<string>(arranged);
        foreach (string token in _order)
        {
            if (absent.Any(a => Same(a, token)))
            {
                rebuilt.Add(token);
            }
            else if (queue.Count > 0)
            {
                rebuilt.Add(queue.Dequeue());
            }
        }

        while (queue.Count > 0)
        {
            rebuilt.Add(queue.Dequeue());
        }

        _order.Clear();
        _order.AddRange(rebuilt);
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
