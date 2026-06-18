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
        Controller.Completed += OnTimerCompleted;

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

    private void OnTimerCompleted()
    {
        if (Controller.PlayCompletionSound) CompletionSound.Play();
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
                Controller.StartCustom(parsed.Duration, parsed.Preset, parsed.Label);
            }
            _quickInput = null;
        };
        window.Show();
    }

    private void OnStartClicked(object? sender, System.EventArgs e) => Controller.Start();
    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    private void OnColorBlueClicked(object? sender, System.EventArgs e)   => Controller.UpdateSettings(s => s with { Color = TimeBarX.Core.BarColor.Blue });
    private void OnColorPurpleClicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s with { Color = TimeBarX.Core.BarColor.Purple });
    private void OnColorGreenClicked(object? sender, System.EventArgs e)  => Controller.UpdateSettings(s => s with { Color = TimeBarX.Core.BarColor.Green });
    private void OnColorRedClicked(object? sender, System.EventArgs e)    => Controller.UpdateSettings(s => s with { Color = TimeBarX.Core.BarColor.Red });
    private void OnColorAccentClicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s with { Color = TimeBarX.Core.BarColor.Accent });

    private void OnHeight2Clicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s with { Height = TimeBarX.Core.BarHeight.Thin });
    private void OnHeight3Clicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s with { Height = TimeBarX.Core.BarHeight.Normal });
    private void OnHeight4Clicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s with { Height = TimeBarX.Core.BarHeight.Thick });

    private void OnOpacity100Clicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s.WithOpacity(1.0));
    private void OnOpacity80Clicked(object? sender, System.EventArgs e)  => Controller.UpdateSettings(s => s.WithOpacity(0.8));
    private void OnOpacity60Clicked(object? sender, System.EventArgs e)  => Controller.UpdateSettings(s => s.WithOpacity(0.6));
    private void OnOpacity40Clicked(object? sender, System.EventArgs e)  => Controller.UpdateSettings(s => s.WithOpacity(0.4));

    private void OnGradientToggleClicked(object? sender, System.EventArgs e) => Controller.UpdateSettings(s => s with { GradientMode = !s.GradientMode });
    private void OnSoundToggleClicked(object? sender, System.EventArgs e)    => Controller.UpdateSettings(s => s with { PlayCompletionSound = !s.PlayCompletionSound });

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
