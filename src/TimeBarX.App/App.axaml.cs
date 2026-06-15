using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TimeBarX.App;

public partial class App : Application
{
    public TrayController Controller { get; } = new();

    public DisplayManager? Displays { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DataContext = Controller;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Displays = new DisplayManager(Controller);
            Displays.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnStartClicked(object? sender, System.EventArgs e) => Controller.Start();
    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    private void OnQuitClicked(object? sender, System.EventArgs e)
    {
        Displays?.Dispose();
        Displays = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
