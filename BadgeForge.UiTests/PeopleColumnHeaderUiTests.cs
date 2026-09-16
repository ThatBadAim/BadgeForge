using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App;
using BadgeForge.App.ViewModels;
using Xunit;

namespace BadgeForge.UiTests;

/// <summary>
/// Drives the real People column headings: dragging one sideways reorders the grid, and the values in each row move
/// with their own column rather than staying put.
/// </summary>
public class PeopleColumnHeaderUiTests
{
    private sealed record Harness(MainWindow Window, MainWindowViewModel Vm, ItemsControl Headers, ListBox List);

    private static Harness Open()
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        var vm = (MainWindowViewModel)window.DataContext!;

        var person = vm.AddPerson();
        foreach (var cell in person.Cells)
        {
            cell.Value = cell.Token + "-value";
        }

        vm.IsPeoplePageActive = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Harness(
            window,
            vm,
            window.FindControl<ItemsControl>("PeopleColumnHeaders")!,
            window.FindControl<ListBox>("PeopleList")!);
    }

    /// <summary>The heading Border for a column, as the user sees and grabs it.</summary>
    private static Border Heading(Harness h, PeopleFieldColumn column) =>
        h.Headers.GetVisualDescendants().OfType<Border>()
            .Single(b => b.Classes.Contains("columnHeader") && ReferenceEquals(b.DataContext, column));

    /// <summary>The centre of the nth column's slot, in window coordinates.</summary>
    private static Point SlotCentre(Harness h, int index)
    {
        double slot = h.Headers.Bounds.Width / h.Vm.PeopleFieldColumns.Count;
        return h.Headers.TranslatePoint(new Point(slot * (index + 0.5), h.Headers.Bounds.Height / 2), h.Window)!.Value;
    }

    private static void Drag(Harness h, PeopleFieldColumn column, int toIndex)
    {
        var heading = Heading(h, column);
        var from = heading.TranslatePoint(new Point(heading.Bounds.Width / 2, heading.Bounds.Height / 2), h.Window)!.Value;

        h.Window.MouseDown(from, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        h.Window.MouseMove(SlotCentre(h, toIndex));
        Dispatcher.UIThread.RunJobs();
        h.Window.MouseUp(SlotCentre(h, toIndex), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task DraggingAHeading_MovesTheColumnAndItsValues() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            Assert.True(h.Vm.PeopleFieldColumns.Count >= 3);
            var person = h.Vm.People[0];
            var moved = h.Vm.PeopleFieldColumns[^1];
            var wasFirst = h.Vm.PeopleFieldColumns[0];

            Drag(h, moved, toIndex: 0);

            Assert.Same(moved, h.Vm.PeopleFieldColumns[0]);
            Assert.Same(wasFirst, h.Vm.PeopleFieldColumns[1]);

            // The row's editors follow their own heading: each box still holds its own field's value
            var boxes = h.List.ContainerFromItem(person)!.GetVisualDescendants().OfType<TextBox>().ToList();
            Assert.Equal(
                h.Vm.PeopleFieldColumns.Select(c => c.Token + "-value"),
                boxes.Select(b => b.Text));
            Assert.Equal(h.Vm.PeopleFieldColumns.Select(c => c.Token), person.Cells.Select(c => c.Token));
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task DraggingAHeading_HighlightsItWhileItMovesAndStopsOnRelease() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            var moved = h.Vm.PeopleFieldColumns[^1];
            var heading = Heading(h, moved);
            var from = heading.TranslatePoint(new Point(heading.Bounds.Width / 2, heading.Bounds.Height / 2), h.Window)!.Value;

            h.Window.MouseDown(from, MouseButton.Left);
            h.Window.MouseMove(SlotCentre(h, 0));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("dragging", Container(h, 0).Classes);
            Assert.DoesNotContain("dragging", Container(h, 1).Classes);

            h.Window.MouseUp(SlotCentre(h, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.All(h.Vm.PeopleFieldColumns.Select((_, i) => Container(h, i)),
                c => Assert.DoesNotContain("dragging", c.Classes));
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task ClickingAHeadingWithoutMoving_ChangesNothing() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            var before = h.Vm.PeopleFieldColumns.Select(c => c.Token).ToList();
            var heading = Heading(h, h.Vm.PeopleFieldColumns[^1]);
            var point = heading.TranslatePoint(new Point(heading.Bounds.Width / 2, heading.Bounds.Height / 2), h.Window)!.Value;

            h.Window.MouseDown(point, MouseButton.Left);
            h.Window.MouseMove(point);
            h.Window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(before, h.Vm.PeopleFieldColumns.Select(c => c.Token));
            Assert.False(h.Vm.IsPeopleColumnOrderCustomised);
        }
        finally
        {
            h.Window.Close();
        }
    });

    [Fact]
    public Task TheHeadingContextMenuMovesTheColumnItWasOpenedOn() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open();
        try
        {
            var last = h.Vm.PeopleFieldColumns[^1];
            var menu = Heading(h, last).ContextMenu!;

            // The menu items inherit the heading's column, which is what the click handlers act on
            var items = menu.Items.OfType<MenuItem>().ToList();
            Assert.Equal(3, items.Count);
            menu.Open(Heading(h, last));
            Dispatcher.UIThread.RunJobs();
            Assert.All(items, item => Assert.Same(last, item.DataContext));
            menu.Close();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            h.Window.Close();
        }
    });

    private static Control Container(Harness h, int index) => (Control)h.Headers.ContainerFromIndex(index)!;
}
