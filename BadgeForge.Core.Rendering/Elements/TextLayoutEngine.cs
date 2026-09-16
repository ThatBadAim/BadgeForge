using System.Globalization;
using BadgeForge.Core.Templates.Enums;

namespace BadgeForge.Core.Rendering.Elements;

/// <summary>
/// The lines a text layer draws and the font size it draws them at.
/// </summary>
/// <param name="FontSizePx">Font size in canvas pixels after any shrinking.</param>
/// <param name="Lines">Lines to draw, top to bottom.</param>
/// <param name="WasShrunk">True when the font was made smaller than requested to fit.</param>
/// <param name="WasTruncated">True when text was cut with an ellipsis or dropped because it didn't fit.</param>
public sealed record TextLayoutResult(float FontSizePx, IReadOnlyList<string> Lines, bool WasShrunk, bool WasTruncated);

/// <summary>
/// Measures how wide a string is at a given font size, in canvas pixels.
/// </summary>
public delegate float TextMeasurer(string text, float fontSizePx);

/// <summary>
/// Fits text into a layer frame according to its <see cref="TextOverflowMode"/>: shrinking the font, wrapping at
/// word boundaries, and cutting with an ellipsis. Pure layout arithmetic over a supplied measuring function, so the
/// print renderer and the designer's inline editor share exactly the same fitting rules, and it can be tested
/// without a font engine.
/// </summary>
public static class TextLayoutEngine
{
    public const string Ellipsis = "…";

    // Shrinking narrows in on the largest size that fits; this many steps gets within a fraction of a pixel
    private const int MaxShrinkSteps = 24;

    /// <summary>
    /// Lays out <paramref name="text"/> in a frame of <paramref name="boxWidth"/> × <paramref name="boxHeight"/> pixels.
    /// </summary>
    /// <param name="text">Resolved text. Explicit line breaks (\n) always start a new line.</param>
    /// <param name="nominalSizePx">The size the designer chose, in pixels.</param>
    /// <param name="minSizePx">Smallest size shrinking may reach, in pixels.</param>
    /// <param name="lineHeight">Baseline-to-baseline distance as a multiple of the font size.</param>
    /// <param name="lineExtentRatio">Height of one line's glyph box (descent − ascent) per pixel of font size.</param>
    /// <param name="measure">Measures a string's advance width at a font size.</param>
    public static TextLayoutResult Layout(
        string text,
        float boxWidth,
        float boxHeight,
        float nominalSizePx,
        float minSizePx,
        TextOverflowMode mode,
        float lineHeight,
        float lineExtentRatio,
        TextMeasurer measure)
    {
        ArgumentNullException.ThrowIfNull(measure);

        var paragraphs = SplitParagraphs(text);
        nominalSizePx = Math.Max(0.5f, nominalSizePx);
        minSizePx = Math.Clamp(minSizePx, 0.5f, nominalSizePx);
        lineHeight = lineHeight > 0 ? lineHeight : 1.2f;

        switch (mode)
        {
            case TextOverflowMode.Overflow:
                return new TextLayoutResult(nominalSizePx, paragraphs, false, false);

            case TextOverflowMode.Ellipsis:
                return FitHeight(TruncateLines(paragraphs, boxWidth, nominalSizePx, measure, out bool cut),
                    nominalSizePx, boxHeight, lineHeight, lineExtentRatio, boxWidth, measure, shrunk: false, cut);

            case TextOverflowMode.Wrap:
                return LayoutWrapped(paragraphs, boxWidth, boxHeight, nominalSizePx, minSizePx, lineHeight, lineExtentRatio, measure);

            case TextOverflowMode.ShrinkToFit:
            default:
                return LayoutShrunk(paragraphs, boxWidth, boxHeight, nominalSizePx, minSizePx, lineHeight, lineExtentRatio, measure);
        }
    }

    /// <summary>
    /// Height of a block of lines, from the top of the first line's glyph box to the bottom of the last.
    /// </summary>
    public static float BlockHeight(int lineCount, float fontSizePx, float lineHeight, float lineExtentRatio) =>
        lineCount <= 0 ? 0 : fontSizePx * lineExtentRatio + (lineCount - 1) * fontSizePx * lineHeight;

    private static TextLayoutResult LayoutShrunk(
        IReadOnlyList<string> paragraphs, float boxWidth, float boxHeight, float nominal, float min,
        float lineHeight, float extentRatio, TextMeasurer measure)
    {
        // A single line only has to fit the width: frames are often drawn tighter than the font's full glyph box,
        // and shrinking those would change existing designs that print fine. Several lines must fit the height too.
        bool checkHeight = paragraphs.Count > 1;
        bool Fits(float size) =>
            paragraphs.All(p => measure(p, size) <= boxWidth) &&
            (!checkHeight || BlockHeight(paragraphs.Count, size, lineHeight, extentRatio) <= boxHeight);

        if (Fits(nominal))
        {
            return new TextLayoutResult(nominal, paragraphs, false, false);
        }

        float size = LargestFittingSize(nominal, min, Fits);
        if (Fits(size))
        {
            return new TextLayoutResult(size, paragraphs, true, false);
        }

        // Too long even at the minimum size: cut it there
        var lines = TruncateLines(paragraphs, boxWidth, min, measure, out bool cut);
        return FitHeight(lines, min, boxHeight, lineHeight, extentRatio, boxWidth, measure, shrunk: min < nominal, cut);
    }

    private static TextLayoutResult LayoutWrapped(
        IReadOnlyList<string> paragraphs, float boxWidth, float boxHeight, float nominal, float min,
        float lineHeight, float extentRatio, TextMeasurer measure)
    {
        bool Fits(float size) =>
            BlockHeight(Wrap(paragraphs, boxWidth, size, measure).Count, size, lineHeight, extentRatio) <= boxHeight;

        if (Fits(nominal))
        {
            return new TextLayoutResult(nominal, Wrap(paragraphs, boxWidth, nominal, measure), false, false);
        }

        float size = LargestFittingSize(nominal, min, Fits);
        var lines = Wrap(paragraphs, boxWidth, size, measure);
        return FitHeight(lines, size, boxHeight, lineHeight, extentRatio, boxWidth, measure, shrunk: size < nominal, truncated: false);
    }

    /// <summary>
    /// Binary search for the largest size between <paramref name="min"/> and <paramref name="max"/> that fits,
    /// or <paramref name="min"/> when none does.
    /// </summary>
    private static float LargestFittingSize(float max, float min, Func<float, bool> fits)
    {
        if (!fits(min))
        {
            return min;
        }

        float lo = min, hi = max;
        for (int i = 0; i < MaxShrinkSteps && hi - lo > 0.1f; i++)
        {
            float mid = (lo + hi) / 2;
            if (fits(mid))
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>
    /// Drops lines that fall below the frame, ending the last visible one with an ellipsis. Always keeps one line.
    /// </summary>
    private static TextLayoutResult FitHeight(
        IReadOnlyList<string> lines, float size, float boxHeight, float lineHeight, float extentRatio,
        float boxWidth, TextMeasurer measure, bool shrunk, bool truncated)
    {
        int maxLines = 1;
        while (maxLines < lines.Count && BlockHeight(maxLines + 1, size, lineHeight, extentRatio) <= boxHeight)
        {
            maxLines++;
        }

        if (lines.Count <= maxLines)
        {
            return new TextLayoutResult(size, lines, shrunk, truncated);
        }

        var kept = lines.Take(maxLines).ToList();
        kept[^1] = AppendEllipsis(kept[^1], boxWidth, size, measure);
        return new TextLayoutResult(size, kept, shrunk, true);
    }

    private static List<string> TruncateLines(
        IReadOnlyList<string> lines, float boxWidth, float size, TextMeasurer measure, out bool truncated)
    {
        truncated = false;
        var result = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            string fitted = TruncateToWidth(line, boxWidth, size, measure);
            truncated |= !ReferenceEquals(fitted, line);
            result.Add(fitted);
        }

        return result;
    }

    /// <summary>
    /// Returns the line itself when it fits, otherwise the longest prefix (cut at a character, never inside a
    /// surrogate pair or combining sequence) followed by an ellipsis.
    /// </summary>
    public static string TruncateToWidth(string line, float boxWidth, float size, TextMeasurer measure)
    {
        if (measure(line, size) <= boxWidth)
        {
            return line;
        }

        return AppendEllipsis(line, boxWidth, size, measure, alwaysAppend: false);
    }

    private static string AppendEllipsis(string line, float boxWidth, float size, TextMeasurer measure, bool alwaysAppend = true)
    {
        if (alwaysAppend && line.EndsWith(Ellipsis, StringComparison.Ordinal))
        {
            return line;
        }

        if (alwaysAppend && measure(line + Ellipsis, size) <= boxWidth)
        {
            return line + Ellipsis;
        }

        int[] boundaries = StringInfo.ParseCombiningCharacters(line);
        int lo = 0, hi = boundaries.Length; // number of text elements kept
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            string candidate = Prefix(line, boundaries, mid) + Ellipsis;
            if (measure(candidate, size) <= boxWidth)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (lo == 0)
        {
            return measure(Ellipsis, size) <= boxWidth ? Ellipsis : string.Empty;
        }

        return Prefix(line, boundaries, lo) + Ellipsis;
    }

    private static string Prefix(string line, int[] boundaries, int elements) =>
        (elements >= boundaries.Length ? line : line[..boundaries[elements]]).TrimEnd();

    /// <summary>
    /// Greedy word wrap. A single word wider than the frame is broken between characters.
    /// </summary>
    public static List<string> Wrap(IReadOnlyList<string> paragraphs, float boxWidth, float size, TextMeasurer measure)
    {
        var lines = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            string current = string.Empty;
            foreach (var word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (measure(candidate, size) <= boxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }

                current = word;
                while (current.Length > 1 && measure(current, size) > boxWidth)
                {
                    int split = LongestFittingPrefix(current, boxWidth, size, measure);
                    lines.Add(current[..split]);
                    current = current[split..];
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }
        }

        return lines;
    }

    private static int LongestFittingPrefix(string word, float boxWidth, float size, TextMeasurer measure)
    {
        int[] boundaries = StringInfo.ParseCombiningCharacters(word);
        int lo = 1, hi = boundaries.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (measure(word[..boundaries[mid]], size) <= boxWidth)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Always make progress, even when a single character is wider than the frame
        return boundaries.Length > 1 ? boundaries[Math.Max(1, lo)] : word.Length;
    }

    private static IReadOnlyList<string> SplitParagraphs(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
}
