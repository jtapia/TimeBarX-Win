using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TimeBarX.App;

public partial class App : Application
{
    public TrayController Controller { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public OverlayWindow? Overlay { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        DataContext = Controller;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ShowPrimaryOverlay();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowPrimaryOverlay()
    {
        var window = new OverlayWindow { DataContext = Controller };
        window.Show();

        var primary = window.Screens?.Primary;
        if (primary is not null)
        {
            window.PositionOnScreen(primary);
        }

        Overlay = window;
    }

    private void OnStartClicked(object? sender, System.EventArgs e) => Controller.Start();
    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    private void OnQuitClicked(object? sender, System.EventArgs e)
    {
        Overlay?.Close();
        Overlay = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
