using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using TimeBarX.Core;

namespace TimeBarX.App;

public partial class OverlayWindow : Window
{
    private static readonly IBrush FlashBrush = Brushes.White;

    private Rectangle? _progressBar;
    private TrayController? _boundController;
    private CompletionAnimator? _animator;
    private OverlayPolicy? _policy;
    private Screen? _screen;

    public OverlayWindow()
    {
        InitializeComponent();
        _progressBar = this.FindControl<Rectangle>("ProgressBar");
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void PositionOnScreen(Screen screen)
    {
        _screen = screen;
        ApplyScreenLayout();
    }

    private void ApplyScreenLayout()
    {
        if (_screen is null) return;
        var bounds = _screen.Bounds;
        var scaling = _screen.Scaling > 0 ? _screen.Scaling : 1.0;
        var barHeight = (int)(_boundController?.Settings.Height ?? BarHeight.Normal);

        Width = bounds.Width / scaling;
        Height = barHeight;
        Position = new PixelPoint(bounds.X, bounds.Y);

        if (_progressBar is not null) _progressBar.Height = barHeight;
        UpdateProgressBarWidth();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyClickThrough();
        UpdateProgressBarWidth();
        StartPolicyIfReady();
    }

    private void StartPolicyIfReady()
    {
        if (_policy is not null || _boundController is null) return;
        _policy = new OverlayPolicy(this, _boundController);
    }

    protected override void OnClosed(EventArgs e)
    {
        _policy?.Dispose();
        _policy = null;
        base.OnClosed(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundController is not null)
        {
            _boundController.PropertyChanged -= OnControllerPropertyChanged;
            _boundController.Completed -= OnTimerCompleted;
            _boundController.SettingsChanged -= ApplySettings;
        }

        _boundController = DataContext as TrayController;

        if (_boundController is not null)
        {
            _boundController.PropertyChanged += OnControllerPropertyChanged;
            _boundController.Completed += OnTimerCompleted;
            _boundController.SettingsChanged += ApplySettings;
        }

        ApplySettings();
        UpdateProgressBarWidth();
        StartPolicyIfReady();
    }

    private void ApplySettings()
    {
        if (_boundController is null) return;
        var settings = _boundController.Settings;
        Opacity = settings.Opacity;
        ApplyScreenLayout();
        ApplyBarColor();
    }

    private void ApplyBarColor()
    {
        if (_progressBar is null || _boundController is null) return;
        if (_animator is { IsActive: true }) return; // animator owns the brush mid-sequence
        var rgb = BarColorPalette.ForProgress(_boundController.Settings, _boundController.Progress);
        _progressBar.Fill = ToBrush(rgb);
    }

    private static SolidColorBrush ToBrush(Rgb rgb) => new(Color.FromRgb(rgb.R, rgb.G, rgb.B));

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrayController.Progress) || e.PropertyName == nameof(TrayController.IsBarVisible))
        {
            UpdateProgressBarWidth();
            ApplyBarColor();
        }

        if (e.PropertyName == nameof(TrayController.IsBarVisible) && _boundController is { IsBarVisible: true })
        {
            // A new timer started — cancel any in-flight completion animation
            // and restore opacity/color from settings.
            _animator?.Cancel();
            _animator = null;
            Opacity = _boundController.Settings.Opacity;
            ApplyBarColor();
        }
    }

    private void OnTimerCompleted()
    {
        if (_progressBar is null || _boundController is null) return;
        _animator?.Cancel();
        var resting = ToBrush(BarColorPalette.ForProgress(_boundController.Settings, 1.0));
        _animator = new CompletionAnimator(this, _progressBar, resting, FlashBrush, _boundController.Settings.Opacity);
        _animator.Run();
    }

    private void UpdateProgressBarWidth()
    {
        if (_progressBar is null) return;
        var progress = _boundController?.Progress ?? 0.0;
        if (progress < 0) progress = 0;
        if (progress > 1) progress = 1;
        _progressBar.Width = Width * progress;
    }

    private void ApplyClickThrough()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var handle = TryGetPlatformHandle()?.Handle;
        if (handle is null || handle == IntPtr.Zero) return;

        // Click-through requires BOTH WS_EX_LAYERED and WS_EX_TRANSPARENT — with
        // WS_EX_TRANSPARENT alone, Windows still hit-tests the window normally and
        // the overlay swallows clicks on caption buttons of maximized apps sitting
        // under it. We then immediately call SetLayeredWindowAttributes(LWA_ALPHA)
        // so the window stays in the DWM-composed path (no UpdateLayeredWindow
        // fallback, which would break Avalonia's GPU rendering).
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const uint LWA_ALPHA = 0x00000002;

        var current = GetWindowLongPtr(handle.Value, GWL_EXSTYLE).ToInt64();
        var updated = current | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(handle.Value, GWL_EXSTYLE, new IntPtr(updated));
        SetLayeredWindowAttributes(handle.Value, 0, 255, LWA_ALPHA);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
}
