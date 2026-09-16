using System;
using BadgeForge.App.Controls;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Templates.Layers;
using Xunit;

namespace BadgeForge.Tests;

public class LayerSnapperTests
{
    private const double CardW = 85.6;
    private const double CardH = 53.98;
    private const double SafeMargin = 3.0;

    [Fact]
    public void Snap_NearCardCentre_LocksLayerCentreToCardCentre()
    {
        var result = LayerSnapper.Snap(22.0, 10.0, 40.0, 8.0, CardW, CardH, SafeMargin, Array.Empty<TemplateLayer>(), 1.5);

        Assert.Equal(22.8, result.X, 6);
        Assert.Equal(CardW / 2, result.GuideXmm!.Value, 6);
        Assert.Equal(10.0, result.Y);
        Assert.Null(result.GuideYmm);
    }

    [Fact]
    public void Snap_BeyondThreshold_LeavesPositionAlone()
    {
        var result = LayerSnapper.Snap(10.0, 20.0, 12.0, 8.0, CardW, CardH, SafeMargin, Array.Empty<TemplateLayer>(), 1.0);

        Assert.Equal(10.0, result.X);
        Assert.Equal(20.0, result.Y);
        Assert.Null(result.GuideXmm);
        Assert.Null(result.GuideYmm);
    }

    [Fact]
    public void Snap_NearSafeAreaAndOtherLayer_LocksToBoth()
    {
        var other = new TextLayer { X = 50.0, Y = 30.0, Width = 20.0, Height = 8.0 };

        var result = LayerSnapper.Snap(49.6, 3.4, 12.0, 6.0, CardW, CardH, SafeMargin, new TemplateLayer[] { other }, 1.0);

        Assert.Equal(50.0, result.X, 6);
        Assert.Equal(50.0, result.GuideXmm!.Value, 6);
        Assert.Equal(SafeMargin, result.Y, 6);
        Assert.Equal(SafeMargin, result.GuideYmm!.Value, 6);
    }

    [Fact]
    public void Snap_BottomEdgeNearCardBottom_LocksToCardEdge()
    {
        var result = LayerSnapper.Snap(10.0, 45.5, 12.0, 8.0, CardW, CardH, SafeMargin, Array.Empty<TemplateLayer>(), 1.0);

        Assert.Equal(CardH - 8.0, result.Y, 6);
        Assert.Equal(CardH, result.GuideYmm!.Value, 6);
    }

    [Fact]
    public void Snap_BetweenTwoLayers_LocksIntoMiddleOfTheGap()
    {
        // Two layers side by side at the same height, with a 10mm gap between them (15..25),
        // chosen well away from the card centre/safe-area targets so it's the only thing that can match.
        var left = new TextLayer { X = 5.0, Y = 5.0, Width = 10.0, Height = 4.0 };
        var right = new TextLayer { X = 25.0, Y = 5.0, Width = 10.0, Height = 4.0 };

        // A 4mm-wide layer dragged so its centre lands near x=19.6 (gap midpoint is 20.0)
        var result = LayerSnapper.Snap(17.6, 5.0, 4.0, 4.0, CardW, CardH, SafeMargin,
            new TemplateLayer[] { left, right }, 1.0);

        Assert.Equal(18.0, result.X, 6);
        Assert.Equal(20.0, result.GuideXmm!.Value, 6);
    }

    [Fact]
    public void Snap_BetweenTwoLayers_IgnoresGapWhenLayersDontOverlapPerpendicularAxis()
    {
        // Same horizontal gap as above, but the two layers no longer share any vertical overlap
        var top = new TextLayer { X = 5.0, Y = 0.0, Width = 10.0, Height = 4.0 };
        var bottom = new TextLayer { X = 25.0, Y = 45.0, Width = 10.0, Height = 4.0 };

        var result = LayerSnapper.Snap(17.6, 25.0, 4.0, 4.0, CardW, CardH, SafeMargin,
            new TemplateLayer[] { top, bottom }, 1.0);

        Assert.Equal(17.6, result.X);
        Assert.Null(result.GuideXmm);
    }

    [Fact]
    public void SetSelectedLayerPosition_PlacesLayerExactly_AndClampsToCard()
    {
        var vm = new MainWindowViewModel();
        vm.AddTextLayer();

        vm.SetSelectedLayerPosition(22.8, 23.0);
        Assert.Equal(22.8, vm.SelectedLayer!.X);
        Assert.Equal(23.0, vm.SelectedLayer.Y);

        vm.SetSelectedLayerPosition(-4.0, -1.0);
        Assert.Equal(0.0, vm.SelectedLayer!.X);
        Assert.Equal(0.0, vm.SelectedLayer.Y);
    }
}
