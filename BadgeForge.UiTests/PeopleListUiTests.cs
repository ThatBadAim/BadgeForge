using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BadgeForge.App;
using BadgeForge.App.ViewModels;
using SkiaSharp;
using Xunit;

namespace BadgeForge.UiTests;

/// <summary>
/// Drives the real People page in a headless session: keyboard entry across the list, and dropping photo files.
/// </summary>
public class PeopleListUiTests
{
    private sealed record Harness(MainWindow Window, MainWindowViewModel Vm, ListBox List);

    private static Harness Open(int people)
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        var vm = (MainWindowViewModel)window.DataContext!;
        for (int i = 0; i < people; i++)
        {
            vm.AddPerson();
        }

        vm.IsPeoplePageActive = true;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Harness(window, vm, window.FindControl<ListBox>("PeopleList")!);
    }

    private static void Close(Harness h) => h.Window.Close();

    private static T Wait<T>(Task<T> task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        return task.GetAwaiter().GetResult();
    }

    private static TextBox FieldBox(Harness h, PersonFieldCell cell) =>
        h.List.ContainerFromItem(cell.Owner)!.GetVisualDescendants().OfType<TextBox>().Single(b => b.DataContext == cell);

    private static PersonFieldCell? FocusedCell(Harness h) =>
        (h.Window.FocusManager?.GetFocusedElement() as TextBox)?.DataContext as PersonFieldCell;

    private static void Press(Harness h, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        h.Window.KeyPressQwerty(key, modifiers);
        h.Window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Tab_StepsThroughTheFields_ThenOntoTheNextPerson() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open(people: 2);
        try
        {
            var first = h.Vm.People[0];
            var second = h.Vm.People[1];
            Assert.True(first.Cells.Count >= 2);

            FieldBox(h, first.Cells[0]).Focus();
            Dispatcher.UIThread.RunJobs();

            Press(h, PhysicalKey.Tab);
            Assert.Same(first.Cells[1], FocusedCell(h));

            for (int i = 2; i <= first.Cells.Count; i++)
            {
                Press(h, PhysicalKey.Tab);
            }

            Assert.Same(second.Cells[0], FocusedCell(h));
            Assert.Same(second, h.Vm.SelectedPerson);

            Press(h, PhysicalKey.Tab, RawInputModifiers.Shift);
            Assert.Same(first.Cells[^1], FocusedCell(h));
        }
        finally
        {
            Close(h);
        }
    });

    [Fact]
    public Task Enter_MovesToTheNextPersonsFirstField_AddingOneAtTheEnd() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var h = Open(people: 1);
        try
        {
            var first = h.Vm.People[0];
            FieldBox(h, first.Cells[0]).Focus();
            h.Window.KeyTextInput("Ada");
            Press(h, PhysicalKey.Tab);
            h.Window.KeyTextInput("Lovelace");
            Press(h, PhysicalKey.Enter);

            Assert.Equal("Ada Lovelace", first.Name);
            Assert.Equal(2, h.Vm.People.Count);
            var added = h.Vm.People[1];
            Assert.Same(added.Cells[0], FocusedCell(h));

            // Enter on a blank last person doesn't keep adding empty ones
            Press(h, PhysicalKey.Enter);
            Assert.Equal(2, h.Vm.People.Count);
            Assert.Same(added.Cells[0], FocusedCell(h));

            h.Window.KeyTextInput("Grace");
            Press(h, PhysicalKey.Enter, RawInputModifiers.Shift);
            Assert.Same(first.Cells[0], FocusedCell(h));
            Assert.Equal("Grace", added.Cells[0].Value);
        }
        finally
        {
            Close(h);
        }
    });

    [Fact]
    public Task DraggingAnImageOntoAPhotoBox_HighlightsIt_AndDroppingSetsThatPersonsPhoto() => HeadlessTestApp.RunOnUiThread(() =>
    {
        string dir = Directory.CreateTempSubdirectory("badgeforge-drop-").FullName;
        var h = Open(people: 2);
        try
        {
            string photo = Path.Combine(dir, "jane_smith.png");
            using (var bitmap = new SKBitmap(40, 50))
            using (var stream = File.Create(photo))
            {
                bitmap.Erase(SKColors.Teal);
                SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
            }

            var target = h.Vm.People[1];
            var photoBox = h.List.ContainerFromItem(target)!.GetVisualDescendants().OfType<Button>()
                .Single(b => b.Classes.Contains("personPhoto"));
            var point = photoBox.TranslatePoint(new Point(photoBox.Bounds.Width / 2, photoBox.Bounds.Height / 2), h.Window)!.Value;

            // Two files dropped on a photo box: the first becomes the photo, nobody is added
            var file = Wait(h.Window.StorageProvider.TryGetFileFromPathAsync(photo));
            Assert.NotNull(file);
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(file));
            data.Add(DataTransferItem.CreateFile(file));

            h.Window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
            h.Window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("dropTarget", photoBox.Classes);

            h.Window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!target.HasPhoto && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            Assert.DoesNotContain("dropTarget", photoBox.Classes);
            Assert.True(target.HasPhoto);
            Assert.Equal(photo, target.PhotoPath);
            Assert.False(h.Vm.People[0].HasPhoto);
            Assert.Equal(2, h.Vm.People.Count);
            Assert.Same(target, h.Vm.SelectedPerson);
        }
        finally
        {
            Close(h);
            Directory.Delete(dir, recursive: true);
        }
    });
}
