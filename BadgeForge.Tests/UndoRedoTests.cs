using System;
using System.Linq;
using BadgeForge.App.ViewModels;
using BadgeForge.Core.Printing.Models;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using Xunit;

namespace BadgeForge.Tests;

/// <summary>
/// Undo and redo in the badge designer: every edit that reaches the template can be stepped back, continuous
/// gestures collapse into one step, and a new or opened design starts a fresh history.
/// </summary>
public class UndoRedoTests
{
    private static TemplateLayer LayerNamed(MainWindowViewModel vm, string name) =>
        vm.Template.Layers.Single(l => l.Name == name);

    [Fact]
    public void NewViewModel_HasNothingToUndoOrRedo()
    {
        var vm = new MainWindowViewModel();

        Assert.False(vm.CanUndo);
        Assert.False(vm.CanRedo);
        Assert.False(vm.Undo());
        Assert.False(vm.Redo());
        Assert.Contains("Nothing to undo", vm.UndoTooltip);
    }

    [Fact]
    public void Undo_AfterMovingALayer_PutsItBack_AndRedoMovesItAgain()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");
        double startX = vm.SelectedLayer!.X;
        double startY = vm.SelectedLayer.Y;

        vm.SetSelectedLayerPosition(startX + 12, startY + 7);
        Assert.Equal(startX + 12, LayerNamed(vm, "Full Name").X);

        Assert.True(vm.Undo());
        Assert.Equal(startX, LayerNamed(vm, "Full Name").X);
        Assert.Equal(startY, LayerNamed(vm, "Full Name").Y);

        Assert.True(vm.Redo());
        Assert.Equal(startX + 12, LayerNamed(vm, "Full Name").X);
        Assert.Equal(startY + 7, LayerNamed(vm, "Full Name").Y);
    }

    [Fact]
    public void Undo_AfterDeletingALayer_BringsItBackWithTheSameIdAndSelection()
    {
        var vm = new MainWindowViewModel();
        var target = LayerNamed(vm, "QR Code");
        vm.SelectedLayer = target;
        int countBefore = vm.Template.Layers.Count;

        vm.DeleteSelectedLayer();
        Assert.Equal(countBefore - 1, vm.Template.Layers.Count);

        Assert.True(vm.Undo());
        var restored = LayerNamed(vm, "QR Code");
        Assert.Equal(countBefore, vm.Template.Layers.Count);
        Assert.Equal(target.Id, restored.Id);
        Assert.Equal(target, restored);

        // The user is put back on the layer they were working on
        Assert.Equal(target.Id, vm.SelectedLayer?.Id);
    }

    [Fact]
    public void Undo_AfterAddingALayer_RemovesItAgain()
    {
        var vm = new MainWindowViewModel();
        int countBefore = vm.Template.Layers.Count;

        vm.AddTextLayer(5, 5, "Hello", "Greeting");
        Assert.Equal(countBefore + 1, vm.Template.Layers.Count);

        Assert.True(vm.Undo());
        Assert.Equal(countBefore, vm.Template.Layers.Count);
        Assert.DoesNotContain(vm.Template.Layers, l => l.Name == "Greeting");
        Assert.DoesNotContain(vm.Layers, l => l.Name == "Greeting");
    }

    [Fact]
    public void Undo_AfterEditingText_RestoresTheOldText()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");
        string original = ((TextLayer)vm.SelectedLayer!).Text;

        vm.TextContent = "{{FullName}} — {{JobTitle}}";
        Assert.Equal("{{FullName}} — {{JobTitle}}", ((TextLayer)LayerNamed(vm, "Full Name")).Text);
        Assert.NotEqual("{{FullName}} — {{JobTitle}}", original);

        Assert.True(vm.Undo());
        Assert.Equal(original, ((TextLayer)LayerNamed(vm, "Full Name")).Text);
    }

    [Fact]
    public void Undo_AfterChangingTheCardSize_RestoresTheOldFormat()
    {
        var vm = new MainWindowViewModel();
        var original = vm.ActiveFormat;

        vm.ActiveFormat = CardFormat.CR79;
        Assert.Equal(CardFormat.CR79, vm.Template.TargetFormat);

        Assert.True(vm.Undo());
        Assert.Equal(original, vm.Template.TargetFormat);
        Assert.Equal(original, vm.ActiveFormat);
    }

    [Fact]
    public void ADragIsOneUndoStep_AndTheNextDragIsAnother()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");
        double startX = vm.SelectedLayer!.X;

        // A drag arrives as a stream of small moves
        for (int i = 1; i <= 20; i++)
        {
            vm.SetSelectedLayerPosition(startX + i * 0.4, vm.SelectedLayer!.Y);
        }

        vm.EndHistoryGesture();
        vm.SetSelectedLayerPosition(startX + 30, vm.SelectedLayer!.Y);

        Assert.True(vm.Undo());
        Assert.Equal(startX + 8.0, LayerNamed(vm, "Full Name").X, 3);   // back to where the second drag started

        Assert.True(vm.Undo());
        Assert.Equal(startX, LayerNamed(vm, "Full Name").X, 3);          // back to before the first drag

        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void EditingAfterAnUndo_DropsWhatCouldHaveBeenRedone()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");

        vm.SetSelectedLayerPosition(20, 20);
        Assert.True(vm.Undo());
        Assert.True(vm.CanRedo);

        vm.EndHistoryGesture();
        vm.SetSelectedLayerPosition(30, 30);

        Assert.False(vm.CanRedo);
    }

    [Fact]
    public void StartingANewTemplate_ClearsTheHistory()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");
        vm.SetSelectedLayerPosition(20, 20);
        Assert.True(vm.CanUndo);

        vm.NewBlankTemplate(CardFormat.CR80);

        Assert.False(vm.CanUndo);
        Assert.False(vm.CanRedo);
        Assert.Empty(vm.Template.Layers);
    }

    [Fact]
    public void Undo_DoesNotTouchThePeopleList()
    {
        var vm = new MainWindowViewModel();
        var person = vm.AddPerson();
        person.Cells[0].Value = "Ada Lovelace";

        vm.SelectedLayer = LayerNamed(vm, "Full Name");
        vm.SetSelectedLayerPosition(20, 20);
        Assert.True(vm.Undo());

        Assert.Contains(vm.People, p => ReferenceEquals(p, person));
        Assert.Equal("Ada Lovelace", person.Cells[0].Value);
    }

    [Fact]
    public void UndoTooltip_NamesTheEditThatWouldBeReversed()
    {
        var vm = new MainWindowViewModel();
        vm.SelectedLayer = LayerNamed(vm, "Full Name");

        vm.SetSelectedLayerPosition(20, 20);
        Assert.Contains("layer position", vm.UndoTooltip);

        vm.DeleteSelectedLayer();
        Assert.Contains("deleting the layer", vm.UndoTooltip);

        vm.Undo();
        Assert.Contains("deleting the layer", vm.RedoTooltip);
    }

    [Fact]
    public void History_KeepsAtMostItsCapacity_AndTheOldestStepsFallOff()
    {
        var history = new TemplateHistory();
        var template = new TemplateDefinition { Name = "T" };

        for (int i = 0; i < TemplateHistory.Capacity + 25; i++)
        {
            history.Record(new TemplateSnapshot(template with { Name = $"step {i}" }, null, $"step {i}"));
        }

        Assert.Equal(TemplateHistory.Capacity, history.UndoDepth);

        var restored = history.Undo(new TemplateSnapshot(template, null, "now"));
        Assert.Equal($"step {TemplateHistory.Capacity + 24}", restored!.Template.Name);
    }

    [Fact]
    public void History_MergesOnlyWhileTheSameGestureKeepsArriving()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var history = new TemplateHistory(() => now);
        var template = new TemplateDefinition();

        history.Record(new TemplateSnapshot(template, null, "move"), "move:a");
        history.Record(new TemplateSnapshot(template, null, "move"), "move:a");
        Assert.Equal(1, history.UndoDepth);

        // A different key always starts a new step, even in the same instant
        history.Record(new TemplateSnapshot(template, null, "resize"), "resize:a");
        Assert.Equal(2, history.UndoDepth);

        // And so does the same key after a pause
        now = now.Add(TemplateHistory.MergeWindow + TimeSpan.FromMilliseconds(1));
        history.Record(new TemplateSnapshot(template, null, "resize"), "resize:a");
        Assert.Equal(3, history.UndoDepth);
    }
}
