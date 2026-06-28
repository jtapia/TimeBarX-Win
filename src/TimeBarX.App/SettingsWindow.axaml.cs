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

    // Radio groups — indexed in the order they appear in XAML so SyncFromSettings
    // can mark the active option without N near-identical field accesses.
    private RadioButton[]? _defaultDurationRadios;
    private TimeSpan[]? _defaultDurationValues;
    private RadioButton[]? _colorRadios;
    private BarColor[]? _colorValues;
    private RadioButton[]? _heightRadios;
    private BarHeight[]? _heightValues;

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

        _defaultDurationRadios = new[]
        {
            this.FindControl<RadioButton>("Default15")!,
            this.FindControl<RadioButton>("Default25")!,
            this.FindControl<RadioButton>("Default50")!,
            this.FindControl<RadioButton>("Default90")!,
        };
        _defaultDurationValues = new[]
        {
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(25),
            TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(90),
        };
        _colorRadios = new[]
        {
            this.FindControl<RadioButton>("ColorAccent")!,
            this.FindControl<RadioButton>("ColorBlue")!,
            this.FindControl<RadioButton>("ColorPurple")!,
            this.FindControl<RadioButton>("ColorGreen")!,
            this.FindControl<RadioButton>("ColorRed")!,
        };
        _colorValues = new[]
        {
            BarColor.Accent, BarColor.Blue, BarColor.Purple, BarColor.Green, BarColor.Red,
        };
        _heightRadios = new[]
        {
            this.FindControl<RadioButton>("HeightThin")!,
            this.FindControl<RadioButton>("HeightNormal")!,
            this.FindControl<RadioButton>("HeightThick")!,
        };
        _heightValues = new[] { BarHeight.Thin, BarHeight.Normal, BarHeight.Thick };

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
        }
        _controller = DataContext as TrayController;
        if (_controller is not null)
        {
            _controller.SettingsChanged += SyncFromSettings;
            _controller.PropertyChanged += OnControllerPropertyChanged;
        }
        SyncFromSettings();
        SyncUpdate();
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
            return;
        }
        _updateText.Text = $"Update available: {update.LatestVersion}";
        _updateText.IsVisible = true;
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

            SyncRadioGroup(_defaultDurationRadios, _defaultDurationValues, s.DefaultDuration);
            SyncRadioGroup(_colorRadios, _colorValues, s.Color);
            SyncRadioGroup(_heightRadios, _heightValues, s.Height);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void SyncRadioGroup<T>(RadioButton[]? radios, T[]? values, T current)
    {
        if (radios is null || values is null) return;
        var cmp = System.Collections.Generic.EqualityComparer<T>.Default;
        for (var i = 0; i < radios.Length; i++)
        {
            radios[i].IsChecked = cmp.Equals(values[i], current);
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
        => Update(x => x with { AlwaysAboveEverything = _alwaysAboveCheck?.IsChecked == true });

    // Appearance
    private void OnColorAccent(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Color = BarColor.Accent });
    private void OnColorBlue(object? s, Avalonia.Interactivity.RoutedEventArgs e)   => Update(x => x with { Color = BarColor.Blue });
    private void OnColorPurple(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Color = BarColor.Purple });
    private void OnColorGreen(object? s, Avalonia.Interactivity.RoutedEventArgs e)  => Update(x => x with { Color = BarColor.Green });
    private void OnColorRed(object? s, Avalonia.Interactivity.RoutedEventArgs e)    => Update(x => x with { Color = BarColor.Red });

    private void OnHeight2(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Height = BarHeight.Thin });
    private void OnHeight3(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Height = BarHeight.Normal });
    private void OnHeight4(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Update(x => x with { Height = BarHeight.Thick });

    private void OnOpacityChanged(object? s, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_syncing) return;
        Update(x => x.WithOpacity(e.NewValue));
    }

    private void OnGradientClicked(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        => Update(x => x with { GradientMode = _gradientCheck?.IsChecked == true });
}
