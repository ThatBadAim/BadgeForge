using Avalonia.Controls;
using Avalonia.Threading;
using BadgeForge.App;
using BadgeForge.App.ViewModels;
using Xunit;

namespace BadgeForge.UiTests;

/// <summary>
/// Drives the real Design page in a headless session.
/// </summary>
public class DesignPageUiTests
{
    [Fact]
    public Task SelectingALayer_BringsTheLayerTabBackFromTheDataTab() => HeadlessTestApp.RunOnUiThread(() =>
    {
        var window = new MainWindow { Width = 1400, Height = 850 };
        var vm = (MainWindowViewModel)window.DataContext!;
        vm.IsDesignPageActive = true;
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            var tabs = window.FindControl<TabControl>("InspectorTabs")!;
            var first = vm.Layers[0];
            var second = vm.Layers[1];

            vm.SelectedLayer = first;
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            vm.SelectedLayer = second;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, tabs.SelectedIndex);

            // Clearing the selection leaves the tab alone
            tabs.SelectedIndex = 1;
            vm.SelectedLayer = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    });
}
