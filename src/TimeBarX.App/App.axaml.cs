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
    // Pick the entitlement sources per target. Two channels grant Pro and they
    // compose: a user can buy either through the Store or via a direct
    // (Gumroad) license key, and switching install channels keeps their unlock.
    //   - Store build (net10.0-windows10.0.19041.0):  Store OR License-key
    //   - Cross-platform / dev build (plain net10.0): Mock OR License-key
    //     (Mock honors TIMEBARX_PRO=1 for macOS dev / CI.)
    public TimeBarX.App.Store.LicenseKeyEntitlements LicenseKey { get; } = new();

    /// <summary>
    /// The store/dev purchase channel (the concrete StoreEntitlements or
    /// MockEntitlements). The UpgradeProDialog's Buy/Restore buttons need this
    /// concrete instance — NOT the composed <see cref="TrayController.Entitlements"/>,
    /// whose runtime type is OrEntitlements and would fail the dialog's
    /// `is StoreEntitlements` / `is MockEntitlements` checks.
    /// </summary>
    public TimeBarX.Core.IEntitlements PurchaseChannel { get; }

    public TrayController Controller { get; }

    public App()
    {
#if WINDOWS
        PurchaseChannel = new TimeBarX.App.Store.StoreEntitlements();
#else
        PurchaseChannel = new TimeBarX.App.Store.MockEntitlements();
#endif
        var composed = new TimeBarX.App.Store.OrEntitlements(PurchaseChannel, LicenseKey);
        Controller = new TrayController(
            new TimeBarX.Core.JsonTimerStore(),
            new TimeBarX.Core.JsonSettingsStore(),
            composed,
            new Win32IdleProbe());
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

            // Custom presets are appended to the "Start timer" submenu at runtime;
            // refresh on any settings change (Manage presets dialog edits) and on
            // entitlement flip (Pro → custom presets show; Free → "Add preset…" hint).
            Controller.SettingsChanged += RebuildStartTimerMenu;
            RebuildStartTimerMenu();

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
            // Accept both host-form (timebarx://…) and path-form (timebarx:/…);
            // UriCommand normalizes both, this just has to pick up either arg.
            var startupUri = desktop.Args?.FirstOrDefault(a => a.StartsWith("timebarx:", StringComparison.OrdinalIgnoreCase));
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
        // URI automation is a Pro feature: non-Pro users see all timebarx://
        // commands silently no-op. Silent (no toast/popup) is deliberate —
        // automation runs unattended and shouldn't surface upgrade nags.
        if (!Controller.Entitlements.IsPro) return;
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
        CompletionSound.Play(Controller.Settings.EffectiveCompletionSound);
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

    private NativeMenuItemBase[]? _builtinStartItems;

    /// <summary>
    /// Rebuilds the "Start timer" submenu so the user's custom presets (Pro)
    /// appear above the built-in 1/5/10/... entries. Snapshotting the built-in
    /// items on first call means we can re-construct the menu deterministically
    /// without losing their click handlers across rebuilds.
    /// </summary>
    private void RebuildStartTimerMenu()
    {
        var startMenu = FindStartTimerMenu();
        if (startMenu is null) return;

        // First call: snapshot the XAML-declared built-in items so we can
        // re-insert them on every rebuild without losing the Click handlers.
        // Snapshot the full item list (NativeMenuItemBase) — not just
        // NativeMenuItem — so the separator before "Custom…" survives rebuilds.
        _builtinStartItems ??= startMenu.Items.ToArray();

        startMenu.Items.Clear();

        var customPresets = Controller.Settings.CustomPresets ?? Array.Empty<TimeBarX.Core.CustomPreset>();
        if (Controller.Entitlements.IsPro && customPresets.Count > 0)
        {
            foreach (var p in customPresets)
            {
                var item = new NativeMenuItem(p.Name);
                var captured = p; // capture local to avoid closure-over-loop-variable
                item.Click += (_, _) => Controller.StartCustom(captured.Duration, FormatPresetTag(captured.Duration), captured.Label);
                startMenu.Items.Add(item);
            }
            startMenu.Items.Add(new NativeMenuItemSeparator());
        }
        else if (!Controller.Entitlements.IsPro)
        {
            // Free-tier hook: a single entry that opens the upgrade dialog.
            var add = new NativeMenuItem("Add custom preset… (Pro)");
            add.Click += (_, _) => ShowUpgradeFromTray();
            startMenu.Items.Add(add);
            startMenu.Items.Add(new NativeMenuItemSeparator());
        }

        foreach (var item in _builtinStartItems!) startMenu.Items.Add(item);
    }

    private NativeMenu? FindStartTimerMenu()
    {
        var icons = TrayIcon.GetIcons(this);
        if (icons is null) return null;
        foreach (var icon in icons)
        {
            if (icon.Menu is null) continue;
            foreach (var entry in icon.Menu.Items.OfType<NativeMenuItem>())
            {
                if (entry.Header == "Start timer") return entry.Menu;
            }
        }
        return null;
    }

    private static string FormatPresetTag(TimeSpan d)
    {
        if (d.TotalHours >= 1 && d.Minutes == 0 && d.Seconds == 0) return $"{(int)d.TotalHours}h";
        if (d.TotalMinutes >= 1 && d.Seconds == 0) return $"{(int)d.TotalMinutes}m";
        return d.ToString();
    }

    private void ShowUpgradeFromTray() => ShowUpgradeDialog();

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

    private void OnStartPomodoroClicked(object? sender, System.EventArgs e)
    {
        // Pomodoro requires the settings block to be present and enabled; if
        // the user hasn't enabled it, kicking off with defaults would surprise
        // them — instead open the Settings window so they can configure it.
        var pomo = Controller.Settings.Pomodoro;
        if (pomo is null || !pomo.Enabled)
        {
            OpenSettings();
            return;
        }
        Controller.StartPomodoro();
    }

    private void OnPauseClicked(object? sender, System.EventArgs e) => Controller.Pause();
    private void OnResumeClicked(object? sender, System.EventArgs e) => Controller.Resume();
    private void OnStopClicked(object? sender, System.EventArgs e) => Controller.Stop();

    // Left-clicking the tray icon opens Settings — the primary place to adjust
    // appearance and behavior (the context menu keeps only quick timer actions).
    private void OnTrayIconClicked(object? sender, System.EventArgs e) => OpenSettings();

    private void OnSettingsClicked(object? sender, System.EventArgs e) => OpenSettings();

    // Tray "Buy Pro…" opens the same upgrade dialog the SettingsWindow lock
    // chips do, rather than calling BuyAsync directly. Two reasons:
    //   1. Consistent UX everywhere the user can start the purchase.
    //   2. The dialog surfaces status (network error, unsigned-package error,
    //      "already purchased") and exposes Restore + license-key entry —
    //      previously the tray path called store.BuyAsync() fire-and-forget
    //      and swallowed the StorePurchaseStatus, so any failure looked like
    //      the click did nothing.
    private void OnBuyProClicked(object? sender, System.EventArgs e) => ShowUpgradeDialog();

    private UpgradeProDialog? _upgradeDialog;

    /// <summary>
    /// Opens the Pro upgrade dialog from a tray/menu context (no owner window).
    /// A bare Show() with no owner can land off-screen or behind everything —
    /// which is why "Buy Pro" looked like it did nothing — so we center it and
    /// pull it to the foreground. Reused across the "Buy Pro…" and
    /// "Add custom preset…" tray entries; a single instance is kept so repeated
    /// clicks re-focus rather than stack windows.
    /// </summary>
    private void ShowUpgradeDialog()
    {
        if (_upgradeDialog is not null)
        {
            _upgradeDialog.Activate();
            return;
        }

        // PurchaseChannel is the concrete StoreEntitlements/MockEntitlements —
        // NOT the composed Controller.Entitlements — so the dialog's Buy/Restore
        // type checks match.
        var dialog = new UpgradeProDialog(PurchaseChannel);
        _upgradeDialog = dialog;
        dialog.Closed += (_, _) => _upgradeDialog = null;

        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.Topmost = true;
        dialog.Show();
        dialog.Activate();
        dialog.Topmost = false;
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
