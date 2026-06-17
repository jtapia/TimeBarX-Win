using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TimeBarX.App;

public partial class App : Application
{
    public TrayController Controller { get; } = new();

    public DisplayManager? Displays { get; private set; }

    private PowerEventBridge? _power;

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

            Controller.RestoreFromStore();

            _power = new PowerEventBridge(OnSystemResume);
            _power.Attach();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSystemResume()
    {
        // SystemEvents fires on a non-UI thread; marshal back.
        Dispatcher.UIThread.Post(() => Controller.ReconcileClock());
    }

    private void OnStartClicked(object? sender, System.EventArgs e) => Controller.Start();
    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    private void OnQuitClicked(object? sender, System.EventArgs e)
    {
        _power?.Dispose();
        _power = null;

        Displays?.Dispose();
        Displays = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
