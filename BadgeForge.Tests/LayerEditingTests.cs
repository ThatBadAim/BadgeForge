using System.Linq;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Templates.Layers;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// Editing layers from the designer's property panel and layer list.
/// </summary>
public class LayerEditingTests
{
    private static TemplateLayer LayerNamed(MainWindowViewModel vm, string name) =>
        vm.Template.Layers.Single(l => l.Name == name);

    [Fact]
    public void Layers_ListShowsTopmostLayerFirst()
    {
        var vm = new MainWindowViewModel();

        Assert.Equal(vm.Template.Layers.OrderByDescending(l => l.ZIndex).Select(l => l.Id), vm.Layers.Select(l => l.Id));
    }

    [Fact]
    public void ToggleVisibility_OfAnotherLayer_KeepsTheSelection()
    {
        var vm = new MainWindowViewModel();
        var selected = LayerNamed(vm, "Full Name");
        vm.SelectedLayer = selected;

        vm.ToggleLayerVisibility(LayerNamed(vm, "QR Code"));
        vm.ToggleLayerLock(LayerNamed(vm, "Department"));

        Assert.Same(selected, vm.SelectedLayer);
        Assert.False(LayerNamed(vm, "QR Code").IsVisible);
        Assert.True(LayerNamed(vm, "Department").IsLocked);
    }

    [Fact]
    public void ToggleVisibility_OfSelectedLayer_SelectsTheUpdatedCopy()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");

        vm.ToggleLayerVisibility(vm.SelectedLayer);

        Assert.Same(LayerNamed(vm, "Full Name"), vm.SelectedLayer);
        Assert.False(vm.SelectedLayer!.IsVisible);
    }

    [Fact]
    public void DeleteLayer_AnotherLayer_KeepsTheSelection()
    {
        var vm = new MainWindowViewModel();
        var selected = LayerNamed(vm, "Full Name");
        vm.SelectedLayer = selected;

        vm.DeleteLayer(LayerNamed(vm, "QR Code"));

        Assert.Same(selected, vm.SelectedLayer);
        Assert.DoesNotContain(vm.Template.Layers, l => l.Name == "QR Code");
    }

    [Fact]
    public void DeleteLayer_TheSelectedLayer_SelectsTheLayerThatTookItsPlace()
    {
        var vm = new MainWindowViewModel();
        var selected = LayerNamed(vm, "Full Name");
        int listIndex = vm.Layers.IndexOf(selected);
        vm.SelectedLayer = selected;

        vm.DeleteSelectedLayer();

        Assert.Same(vm.Layers[listIndex], vm.SelectedLayer);
    }

    [Fact]
    public void MoveLayerStackOrder_LayersSharingAZIndex_CanStillBeReordered()
    {
        var vm = new MainWindowViewModel();
        vm.Template.Layers.Clear();
        vm.Template.Layers.Add(new TextLayer { Name = "A" });
        vm.Template.Layers.Add(new TextLayer { Name = "B" });
        vm.Template.Layers.Add(new TextLayer { Name = "C" });
        vm.RefreshLayersList();
        vm.SelectedLayer = LayerNamed(vm, "B");

        // Same ZIndex draws in list order, so A is at the bottom; raising it puts it above B
        vm.MoveLayerStackOrder(LayerNamed(vm, "A"), +1);

        Assert.Equal(new[] { "C", "A", "B" }, vm.Layers.Select(l => l.Name));
        Assert.Same(LayerNamed(vm, "B"), vm.SelectedLayer);
    }

    [Fact]
    public void TextEditors_ReplaceTheLayerInsteadOfChangingItInPlace()
    {
        var vm = new MainWindowViewModel();
        var original = (TextLayer)LayerNamed(vm, "Full Name");
        vm.SelectedLayer = original;

        vm.TextContent = "{FirstName} {MiddleName}";
        vm.LayerX = 12.5;
        vm.TextFontSize = 18;

        // The preview and print renderers read layers on other threads, so the old copy must stay as it was
        Assert.Equal("{{FirstName}} {{LastName}}", original.Text);
        var edited = Assert.IsType<TextLayer>(vm.SelectedLayer);
        Assert.Equal("{FirstName} {MiddleName}", edited.Text);
        Assert.Equal(12.5, edited.X);
        Assert.Equal(18, edited.FontSize);
        Assert.Contains(edited, vm.Template.Layers);
        Assert.Contains(edited, vm.Layers);
    }

    [Fact]
    public void TextContent_NewToken_AppearsInFieldMapping_AndTypedMappingSurvivesEditing()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Department");

        vm.TextContent = "{Department} {Site}";
        var site = Assert.Single(vm.ColumnMappings, m => m.TemplateToken == "Site");
        site.CsvColumn = "office_location";

        // The token disappears for a moment while it is retyped
        vm.TextContent = "{Department} {Sit";
        Assert.DoesNotContain(vm.ColumnMappings, m => m.TemplateToken == "Site");
        vm.TextContent = "{Department} {Site}";

        Assert.Equal("office_location", Assert.Single(vm.ColumnMappings, m => m.TemplateToken == "Site").CsvColumn);
    }

    [Fact]
    public void FontComboBoxes_NullFromAnUnmatchedItem_IsIgnored_AndCustomFamilyIsOffered()
    {
        var vm = new MainWindowViewModel();
        var custom = new TextLayer { Name = "Custom", FontFamily = "Helvetica", FontWeight = "700", ZIndex = 99 };
        vm.Template.Layers.Add(custom);
        vm.RefreshLayersList();
        vm.SelectedLayer = custom;

        vm.TextFontFamily = null;
        vm.TextFontWeight = null;

        Assert.Same(custom, vm.SelectedLayer);
        Assert.Contains("Helvetica", vm.TextFontFamilyOptions);
        Assert.Contains("700", vm.TextFontWeightOptions);
        Assert.Equal("Helvetica", vm.TextFontFamily);
    }

    [Fact]
    public void SetSelectedLayerBounds_ClampsToMinimumSize_AndLeavesLockedLayersAlone()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");

        vm.SetSelectedLayerBounds(-3, 4, 0.5, 10);
        Assert.Equal(0, vm.SelectedLayer!.X);
        Assert.Equal(MainWindowViewModel.MinLayerSizeMm, vm.SelectedLayer.Width);

        vm.ToggleLayerLock(vm.SelectedLayer);
        var locked = vm.SelectedLayer!;
        vm.SetSelectedLayerBounds(20, 20, 30, 30);
        Assert.Same(locked, vm.SelectedLayer);
    }

    [Fact]
    public void DuplicateLayer_OfALockedLayer_IsUnlocked()
    {
        var vm = new MainWindowViewModel();
        var layer = LayerNamed(vm, "Full Name");
        vm.ToggleLayerLock(layer);

        vm.DuplicateLayer(LayerNamed(vm, "Full Name"));

        Assert.False(vm.SelectedLayer!.IsLocked);
    }

    [Fact]
    public void TextColor_UnparseableStoredColor_ShowsBlack()
    {
        var vm = new MainWindowViewModel();
        var layer = new TextLayer { Name = "Odd", ColorHex = "#GG0000", IsPureBlackKResin = false, ZIndex = 99 };
        vm.Template.Layers.Add(layer);
        vm.RefreshLayersList();
        vm.SelectedLayer = layer;

        Assert.Equal("#000000", vm.TextColorHex);
        Assert.NotNull(vm.TextColorBrush);
    }
}
