using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using BadgeForge.App.ViewModels;

namespace BadgeForge.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            // Last line of defence: report an unexpected UI error instead of closing the app and losing unsaved work
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Trace.TraceError($"[BadgeForge] Unhandled UI exception: {e.Exception}");
                if (desktop.MainWindow?.DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.StatusText = $"Something went wrong: {e.Exception.Message}";
                }

                e.Handled = true;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
