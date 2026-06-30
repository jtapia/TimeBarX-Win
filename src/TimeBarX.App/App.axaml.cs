using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TimeBarX.Core;

namespace TimeBarX.App;

public partial class App : Application
{
    // Pick the entitlement source per target:
    //   - Store build (net10.0-windows10.0.19041.0): real StoreContext via
    //     StoreEntitlements. Pro state is queried from the Microsoft Store on
    //     launch and refreshed on Settings open.
    //   - Cross-platform / dev build (plain net10.0): MockEntitlements that
    //     reads TIMEBARX_PRO=1 from the env. Lets macOS dev box keep building.
    public TrayController Controller { get; } = new(
        new TimeBarX.Core.JsonTimerStore(),
        new TimeBarX.Core.JsonSettingsStore(),
        CreateEntitlements());

    private static TimeBarX.Core.IEntitlements CreateEntitlements()
    {
#if WINDOWS
        return new TimeBarX.App.Store.StoreEntitlements();
#else
        return new TimeBarX.App.Store.MockEntitlements();
#endif
    }

    public DisplayManager? Displays { get; private set; }

    private PowerEventBridge? _power;
    private HotkeyService? _hotkey;
    private QuickInputWindow? _quickInput;
    private SettingsWindow? _settings;

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

            // Forwarded URIs from secondary instances.
            if (Program.Instance is { } singleton)
            {
                singleton.MessageReceived += OnSingletonMessage;
            }

            _ = CheckForUpdatesAsync();

            // URI from this process's own startup args.
            var startupUri = desktop.Args?.FirstOrDefault(a => a.StartsWith("timebarx://", StringComparison.OrdinalIgnoreCase));
            if (startupUri is not null) HandleUri(startupUri);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSingletonMessage(string message)
    {
        Dispatcher.UIThread.Post(() => HandleUri(message));
    }

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        var url = Environment.GetEnvironmentVariable("TIMEBARX_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var endpoint)) return;

        var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var checker = new UpdateChecker(endpoint);
        var info = await checker.CheckAsync(current).ConfigureAwait(false);
        if (info is null) return;
        Dispatcher.UIThread.Post(() => Controller.AvailableUpdate = info);
    }

    private void HandleUri(string uri)
    {
        if (!UriCommand.TryParse(uri, out var cmd)) return;
        switch (cmd.Kind)
        {
            case UriCommandKind.Start:
                if (cmd.Duration is { } d)
                    Controller.StartCustom(d, cmd.Preset ?? string.Empty, cmd.Label);
                else
                    // Bare `timebarx://start` honors the configured default duration.
                    Controller.Start();
                break;
            case UriCommandKind.Pause:
                Controller.Pause();
                break;
            case UriCommandKind.Resume:
                Controller.Resume();
                break;
            case UriCommandKind.Stop:
                Controller.Stop();
                break;
        }
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

    private void StartPreset(int minutes)
        => Controller.StartCustom(System.TimeSpan.FromMinutes(minutes), $"{minutes}m");

    private void OnStart1Clicked(object? sender, System.EventArgs e)  => StartPreset(1);
    private void OnStart5Clicked(object? sender, System.EventArgs e)  => StartPreset(5);
    private void OnStart10Clicked(object? sender, System.EventArgs e) => StartPreset(10);
    private void OnStart15Clicked(object? sender, System.EventArgs e) => StartPreset(15);
    private void OnStart20Clicked(object? sender, System.EventArgs e) => StartPreset(20);
    private void OnStart25Clicked(object? sender, System.EventArgs e) => StartPreset(25);
    private void OnStart45Clicked(object? sender, System.EventArgs e) => StartPreset(45);
    private void OnStart60Clicked(object? sender, System.EventArgs e) => StartPreset(60);
    private void OnStart90Clicked(object? sender, System.EventArgs e) => StartPreset(90);
    private void OnStartCustomClicked(object? sender, System.EventArgs e) => ShowQuickInput();
    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    // Left-clicking the tray icon opens Settings — the primary place to adjust
    // appearance and behavior (the context menu keeps only quick timer actions).
    private void OnTrayIconClicked(object? sender, System.EventArgs e) => OpenSettings();

    private void OnSettingsClicked(object? sender, System.EventArgs e) => OpenSettings();

    // Phase 2: prove the entitlement signal works end-to-end. No UI gates yet —
    // Phase 3 wires the lock chips and upgrade modal. On the Store target this
    // opens the real Microsoft Store purchase flow; on cross-platform/dev it
    // toggles MockEntitlements so we can exercise Changed-driven re-renders.
    private void OnBuyProClicked(object? sender, System.EventArgs e)
    {
#if WINDOWS
        if (Controller.Entitlements is TimeBarX.App.Store.StoreEntitlements store)
        {
            _ = store.BuyAsync();
        }
#else
        if (Controller.Entitlements is TimeBarX.App.Store.MockEntitlements mock)
        {
            mock.SetPro(!mock.IsPro);
        }
#endif
    }

    private void OpenSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }
        var window = new SettingsWindow { DataContext = Controller };
        _settings = window;
        window.Closed += (_, _) => _settings = null;
        window.Show();
    }

    private void OnQuitClicked(object? sender, System.EventArgs e)
    {
        _hotkey?.Dispose();
        _hotkey = null;

        _power?.Dispose();
        _power = null;

        _quickInput?.Close();
        _quickInput = null;

        _settings?.Close();
        _settings = null;

        Displays?.Dispose();
        Displays = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
