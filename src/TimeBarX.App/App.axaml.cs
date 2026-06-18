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
    private HotkeyService? _hotkey;
    private QuickInputWindow? _quickInput;

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

            _hotkey = new HotkeyService();
            _hotkey.Pressed += OnHotkeyPressed;
            _hotkey.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSystemResume()
    {
        // SystemEvents fires on a non-UI thread; marshal back.
        Dispatcher.UIThread.Post(() => Controller.ReconcileClock());
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.UIThread.Post(ShowQuickInput);
    }

    private void ShowQuickInput()
    {
        if (_quickInput is not null)
        {
            _quickInput.Activate();
            return;
        }

        var window = new QuickInputWindow();
        _quickInput = window;
        window.Closed += (_, _) =>
        {
            if (window.Result is { } parsed)
            {
                Controller.StartCustom(parsed.Duration, parsed.Preset);
            }
            _quickInput = null;
        };
        window.Show();
    }

    private void OnStartClicked(object? sender, System.EventArgs e) => Controller.Start();
    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    private void OnQuitClicked(object? sender, System.EventArgs e)
    {
        _hotkey?.Dispose();
        _hotkey = null;

        _power?.Dispose();
        _power = null;

        _quickInput?.Close();
        _quickInput = null;

        Displays?.Dispose();
        Displays = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
