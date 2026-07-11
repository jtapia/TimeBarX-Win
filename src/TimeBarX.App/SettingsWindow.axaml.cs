using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TimeBarX.Core;

namespace TimeBarX.App;

public partial class SettingsWindow : Window
{
    private TrayController? _controller;
    private bool _syncing;

    private CheckBox? _soundCheck;
    private CheckBox? _gradientCheck;
    private CheckBox? _alwaysAboveCheck;
    private Control? _alwaysAboveWarning;
    private Slider? _opacitySlider;
    private TextBlock? _versionText;
    private TextBlock? _updateText;
    private Button? _updateDownloadButton;

    // "Pro" chips on locked feature groups — visible only when !IsPro.
    private Control? _colorProChip;
    private Control? _alwaysAboveProChip;
    private Control? _gradientProChip;
    private Control? _presetsProChip;

    // Radio groups paired with their setting value, so SyncFromSettings can mark
    // the active option. Pairing radio+value at one site (vs. parallel arrays)
    // keeps them from silently drifting if the XAML order ever changes.
    private (RadioButton Radio, TimeSpan Value)[]? _defaultDurationRadios;
    private (RadioButton Radio, BarColor Value)[]? _colorRadios;
    private (RadioButton Radio, BarHeight Value)[]? _heightRadios;
    private (RadioButton Radio, BarPosition Value)[]? _positionRadios;

    public SettingsWindow()
    {
        InitializeComponent();
        _soundCheck = this.FindControl<CheckBox>("SoundCheck");
        _gradientCheck = this.FindControl<CheckBox>("GradientCheck");
        _alwaysAboveCheck = this.FindControl<CheckBox>("AlwaysAboveCheck");
        _alwaysAboveWarning = this.FindControl<Control>("AlwaysAboveWarning");
        _opacitySlider = this.FindControl<Slider>("OpacitySlider");
        _versionText = this.FindControl<TextBlock>("VersionText");
        _updateText = this.FindControl<TextBlock>("UpdateText");
        _updateDownloadButton = this.FindControl<Button>("UpdateDownloadButton");
        _colorProChip = this.FindControl<Control>("ColorProChip");
        _alwaysAboveProChip = this.FindControl<Control>("AlwaysAboveProChip");
        _gradientProChip = this.FindControl<Control>("GradientProChip");
        _presetsProChip = this.FindControl<Control>("PresetsProChip");

        _defaultDurationRadios = new[]
        {
            (this.FindControl<RadioButton>("Default15")!, TimeSpan.FromMinutes(15)),
            (this.FindControl<RadioButton>("Default25")!, TimeSpan.FromMinutes(25)),
            (this.FindControl<RadioButton>("Default50")!, TimeSpan.FromMinutes(50)),
            (this.FindControl<RadioButton>("Default90")!, TimeSpan.FromMinutes(90)),
        };
        _colorRadios = new[]
        {
            (this.FindControl<RadioButton>("ColorAccent")!, BarColor.Accent),
            (this.FindControl<RadioButton>("ColorBlue")!,   BarColor.Blue),
            (this.FindControl<RadioButton>("ColorPurple")!, BarColor.Purple),
            (this.FindControl<RadioButton>("ColorGreen")!,  BarColor.Green),
            (this.FindControl<RadioButton>("ColorRed")!,    BarColor.Red),
        };
        _heightRadios = new[]
        {
            (this.FindControl<RadioButton>("HeightThin")!,   BarHeight.Thin),
            (this.FindControl<RadioButton>("HeightNormal")!, BarHeight.Normal),
            (this.FindControl<RadioButton>("HeightThick")!,  BarHeight.Thick),
        };
        _positionRadios = new[]
        {
            (this.FindControl<RadioButton>("PositionTop")!,    BarPosition.Top),
            (this.FindControl<RadioButton>("PositionBottom")!, BarPosition.Bottom),
        };

        if (_versionText is not null)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";
            _versionText.Text = $"Version {version}";
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_controller is not null)
        {
            _controller.SettingsChanged -= SyncFromSettings;
            _controller.PropertyChanged -= OnControllerPropertyChanged;
            _controller.Entitlements.Changed -= SyncProChips;
        }
        _controller = DataContext as TrayController;
        if (_controller is not null)
        {
            _controller.SettingsChanged += SyncFromSettings;
            _controller.PropertyChanged += OnControllerPropertyChanged;
            // Re-render lock chips when entitlement flips (purchase / refund /
            // dev SetPro). SettingsChanged also fires on entitlement transitions
            // (wired in TrayController) for the rendered overlay; the chips are
            // a separate UI concern that doesn't follow stored settings.
            _controller.Entitlements.Changed += SyncProChips;
        }
        SyncFromSettings();
        SyncProChips();
        SyncUpdate();
    }

    protected override void OnClosed(EventArgs e)
    {
        // The controller and entitlements outlive this window, so unhook on close;
        // otherwise every opened-then-closed Settings window stays rooted and keeps
        // running SyncFromSettings/SyncProChips on each settings or entitlement change.
        if (_controller is not null)
        {
            _controller.SettingsChanged -= SyncFromSettings;
            _controller.PropertyChanged -= OnControllerPropertyChanged;
            _controller.Entitlements.Changed -= SyncProChips;
            _controller = null;
        }
        DataContextChanged -= OnDataContextChanged;
        base.OnClosed(e);
    }

    private void SyncProChips()
    {
        var isPro = _controller?.Entitlements.IsPro ?? false;
        var locked = !isPro;
        if (_colorProChip is not null) _colorProChip.IsVisible = locked;
        if (_alwaysAboveProChip is not null) _alwaysAboveProChip.IsVisible = locked;
        if (_gradientProChip is not null) _gradientProChip.IsVisible = locked;
        if (_presetsProChip is not null) _presetsProChip.IsVisible = locked;
    }

    private void OnManagePresetsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RequireProOrPrompt()) return;
        if (_controller is null) return;
        var dialog = new ManagePresetsDialog(_controller);
        dialog.ShowDialog(this);
    }

    /// <summary>
    /// Pro-gate guard. Call at the top of any handler that mutates a Pro-only
    /// setting. If the user isn't entitled, opens the upgrade dialog and
    /// returns false so the caller skips the mutation, then re-syncs the UI to
    /// undo the click's optimistic radio/checkbox toggle.
    /// </summary>
    private bool RequireProOrPrompt()
    {
        if (_controller is null) return false;
        if (_controller.Entitlements.IsPro) return true;
        OpenUpgradeDialog();
        SyncFromSettings();
        return false;
    }

    private void OpenUpgradeDialog()
    {
        if (_controller is null) return;
        // Pass the concrete purchase channel (Store/Mock), not the composed
        // OrEntitlements on the controller — the dialog's Buy/Restore branches
        // type-check against StoreEntitlements/MockEntitlements.
        var channel = (Avalonia.Application.Current as App)?.PurchaseChannel
                      ?? _controller.Entitlements;
        var dialog = new UpgradeProDialog(channel);
        dialog.ShowDialog(this);
    }

    private void OnControllerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrayController.AvailableUpdate)) SyncUpdate();
    }

    private void SyncUpdate()
    {
        if (_updateText is null) return;
        var update = _controller?.AvailableUpdate;
        if (update is null)
        {
            _updateText.IsVisible = false;
            if (_updateDownloadButton is not null) _updateDownloadButton.IsVisible = false;
            return;
        }
        _updateText.Text = $"Update available: {update.LatestVersion}";
        _updateText.IsVisible = true;
        if (_updateDownloadButton is not null)
            _updateDownloadButton.IsVisible = !string.IsNullOrWhiteSpace(update.DownloadUrl);
    }

    private void OnDownloadUpdateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var url = _controller?.AvailableUpdate?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        // Only launch well-formed absolute http(s) URLs; never hand an arbitrary
        // string to the shell.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // best-effort; nothing actionable if the shell can't open a browser.
        }
    }

    private void SyncFromSettings()
    {
        if (_controller is null) return;
        _syncing = true;
        try
        {
            var s = _controller.Settings;
            if (_soundCheck is not null) _soundCheck.IsChecked = s.PlayCompletionSound;
            if (_gradientCheck is not null) _gradientCheck.IsChecked = s.GradientMode;
            if (_alwaysAboveCheck is not null) _alwaysAboveCheck.IsChecked = s.AlwaysAboveEverything;
            if (_alwaysAboveWarning is not null) _alwaysAboveWarning.IsVisible = s.AlwaysAboveEverything;
            if (_opacitySlider is not null) _opacitySlider.Value = s.Opacity;

            SyncRadioGroup(_defaultDurationRadios, s.DefaultDuration);
            SyncRadioGroup(_colorRadios, s.Color);
            SyncRadioGroup(_heightRadios, s.Height);
            SyncRadioGroup(_positionRadios, s.Position);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void SyncRadioGroup<T>((RadioButton Radio, T Value)[]? group, T current)
    {
        if (group is null) return;
        var cmp = System.Collections.Generic.EqualityComparer<T>.Default;
        foreach (var (radio, value) in group)
        {
            radio.IsChecked = cmp.Equals(value, current);
        }
    }

    private void Update(Func<AppSettings, AppSettings> mut)
    {
        if (_syncing || _controller is null) return;
        _controller.UpdateSettings(mut);
    }

    // General
    private void OnDefault15Clicked(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { DefaultDuration = TimeSpan.FromMinutes(15) });
    private void OnDefault25Clicked(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { DefaultDuration = TimeSpan.FromMinutes(25) });
    private void OnDefault50Clicked(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { DefaultDuration = TimeSpan.FromMinutes(50) });
    private void OnDefault90Clicked(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { DefaultDuration = TimeSpan.FromMinutes(90) });
    private void OnSoundCheckClicked(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        => Update(x => x with { PlayCompletionSound = _soundCheck?.IsChecked == true });

    private void OnAlwaysAboveClicked(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RequireProOrPrompt()) return;
        Update(x => x with { AlwaysAboveEverything = _alwaysAboveCheck?.IsChecked == true });
    }

    // Appearance — Color: Blue is the free default; the rest are Pro.
    private void OnColorAccent(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    { if (!RequireProOrPrompt()) return; Update(x => x with { Color = BarColor.Accent }); }
    private void OnColorBlue(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        => Update(x => x with { Color = BarColor.Blue });
    private void OnColorPurple(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    { if (!RequireProOrPrompt()) return; Update(x => x with { Color = BarColor.Purple }); }
    private void OnColorGreen(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    { if (!RequireProOrPrompt()) return; Update(x => x with { Color = BarColor.Green }); }
    private void OnColorRed(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    { if (!RequireProOrPrompt()) return; Update(x => x with { Color = BarColor.Red }); }

    private void OnHeight2(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Height = BarHeight.Thin });
    private void OnHeight3(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Height = BarHeight.Normal });
    private void OnHeight4(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Height = BarHeight.Thick });

    // Position (Top/Bottom) is free — no Pro gate.
    private void OnPositionTop(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        => Update(x => x with { Position = BarPosition.Top });
    private void OnPositionBottom(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        => Update(x => x with { Position = BarPosition.Bottom });

    private void OnOpacityChanged(object? s, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_syncing) return;
        Update(x => x.WithOpacity(e.NewValue));
    }

    private void OnGradientClicked(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!RequireProOrPrompt()) return;
        Update(x => x with { GradientMode = _gradientCheck?.IsChecked == true });
    }
}
