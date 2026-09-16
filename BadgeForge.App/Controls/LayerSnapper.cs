using System;
using System.Collections.Generic;
using BadgeForge.Core.Templates.Layers;

namespace BadgeForge.App.Controls;

/// <summary>
/// Where a dragged layer locks to, in card millimetres. A guide is null when that axis didn't snap.
/// </summary>
public readonly record struct SnapResult(double X, double Y, double? GuideXmm, double? GuideYmm);

/// <summary>
/// PowerPoint-style smart guides: a dragged layer's left edge, centre or right edge (and top, middle or bottom)
/// locks onto the card centre, the card and safe-area edges, another layer's edges and centres, or the
/// midpoint of the gap between two other layers that face each other (equal-spacing).
/// </summary>
public static class LayerSnapper
{
    public static SnapResult Snap(
        double x,
        double y,
        double width,
        double height,
        double cardWidthMm,
        double cardHeightMm,
        double safeMarginMm,
        IEnumerable<TemplateLayer> otherLayers,
        double thresholdMm)
    {
        var layerList = otherLayers as IReadOnlyList<TemplateLayer> ?? new List<TemplateLayer>(otherLayers);

        // Card centre first so it wins ties
        var targetsX = new List<double> { cardWidthMm / 2, 0, safeMarginMm, cardWidthMm - safeMarginMm, cardWidthMm };
        var targetsY = new List<double> { cardHeightMm / 2, 0, safeMarginMm, cardHeightMm - safeMarginMm, cardHeightMm };

        foreach (var layer in layerList)
        {
            targetsX.Add(layer.X);
            targetsX.Add(layer.X + layer.Width / 2);
            targetsX.Add(layer.X + layer.Width);

            targetsY.Add(layer.Y);
            targetsY.Add(layer.Y + layer.Height / 2);
            targetsY.Add(layer.Y + layer.Height);
        }

        // Equal-spacing: lock into the middle of the gap between two other layers that face each other
        for (int i = 0; i < layerList.Count; i++)
        {
            for (int j = i + 1; j < layerList.Count; j++)
            {
                AddGapMidpoints(layerList[i], layerList[j], targetsX, targetsY);
            }
        }

        var (snappedX, guideX) = SnapAxis(x, width, targetsX, thresholdMm);
        var (snappedY, guideY) = SnapAxis(y, height, targetsY, thresholdMm);

        return new SnapResult(snappedX, snappedY, guideX, guideY);
    }

    /// <summary>
    /// Adds the midpoint of the horizontal gap between two layers that overlap vertically (and vice versa)
    /// as a snap target, so a layer dragged between them can lock into the middle, equidistant from both.
    /// </summary>
    private static void AddGapMidpoints(TemplateLayer a, TemplateLayer b, List<double> targetsX, List<double> targetsY)
    {
        if (RangesOverlap(a.Y, a.Y + a.Height, b.Y, b.Y + b.Height))
        {
            var (left, right) = a.X <= b.X ? (a, b) : (b, a);
            double gapStart = left.X + left.Width;
            double gapEnd = right.X;
            if (gapEnd > gapStart)
            {
                targetsX.Add((gapStart + gapEnd) / 2);
            }
        }

        if (RangesOverlap(a.X, a.X + a.Width, b.X, b.X + b.Width))
        {
            var (top, bottom) = a.Y <= b.Y ? (a, b) : (b, a);
            double gapStart = top.Y + top.Height;
            double gapEnd = bottom.Y;
            if (gapEnd > gapStart)
            {
                targetsY.Add((gapStart + gapEnd) / 2);
            }
        }
    }

    private static bool RangesOverlap(double aStart, double aEnd, double bStart, double bEnd) =>
        aStart < bEnd && bStart < aEnd;

    private static (double Position, double? Guide) SnapAxis(double position, double size, List<double> targets, double thresholdMm)
    {
        double[] anchors = { 0, size / 2, size };
        double bestDistance = double.MaxValue;
        double snapped = position;
        double? guide = null;

        foreach (double target in targets)
        {
            foreach (double anchor in anchors)
            {
                double distance = Math.Abs(target - (position + anchor));
                if (distance <= thresholdMm && distance < bestDistance)
                {
                    bestDistance = distance;
                    snapped = target - anchor;
                    guide = target;
                }
            }
        }

        return (snapped, guide);
    }
}
