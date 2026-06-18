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

    public SettingsWindow()
    {
        InitializeComponent();
        _soundCheck = this.FindControl<CheckBox>("SoundCheck");
        _gradientCheck = this.FindControl<CheckBox>("GradientCheck");
        _alwaysAboveCheck = this.FindControl<CheckBox>("AlwaysAboveCheck");
        _alwaysAboveWarning = this.FindControl<Control>("AlwaysAboveWarning");
        _opacitySlider = this.FindControl<Slider>("OpacitySlider");
        _versionText = this.FindControl<TextBlock>("VersionText");

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
        if (_controller is not null) _controller.SettingsChanged -= SyncFromSettings;
        _controller = DataContext as TrayController;
        if (_controller is not null) _controller.SettingsChanged += SyncFromSettings;
        SyncFromSettings();
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
        }
        finally
        {
            _syncing = false;
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
