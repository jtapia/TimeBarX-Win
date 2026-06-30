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

    // Cache the last bar color so the ~30 FPS refresh reuses the same brush
    // instead of allocating a new SolidColorBrush every frame.
    private Rgb? _lastBarRgb;
    private SolidColorBrush? _barBrush;

    // Authoritative bar width (DIPs) from the last layout. We compute progress
    // width from this rather than the window's Width property, which can read
    // NaN/stale mid-relayout (e.g. when switching position) — NaN width makes
    // Avalonia stretch the Rectangle to fill, colorizing the whole bar.
    private double _barWidth;

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
        // EffectiveSettings clamps Pro-only fields (GradientMode, Color,
        // AlwaysAboveEverything) to free behavior when not entitled — the stored
        // values are preserved for re-purchase / Restore. Position is free, so
        // Top/Bottom always applies regardless of entitlement.
        var effective = _boundController?.EffectiveSettings;
        var position = effective?.Position ?? BarPosition.Top;
        var configHeightDip = (int)(effective?.Height ?? BarHeight.Normal);

        _barWidth = bounds.Width / scaling;
        Width = _barWidth;

        int yPx;       // window top in physical pixels
        int heightDip; // window/bar height in DIPs

        if (position == BarPosition.Bottom)
        {
            // Bottom mode: overlay the taskbar so the progress color fills it,
            // sized to the taskbar's height. The window sits ON TOP of the
            // (top-most) taskbar; OverlayPolicy reasserts top-most aggressively
            // to keep it from being covered. Click-through (WS_EX_TRANSPARENT)
            // keeps the taskbar buttons working underneath. Falls back to the
            // configured height (above the taskbar) if no bottom taskbar is found.
            //
            // Derive yPx from the SAME physical height we round the DIP height
            // from, so on fractional scaling (125/150%) the bar lines up flush
            // with the taskbar instead of leaving a 1px seam.
            var taskbarPx = TaskbarHeightOnScreen(bounds);
            var heightPx = taskbarPx > 0 ? taskbarPx : (int)Math.Round(configHeightDip * scaling);
            heightDip = (int)Math.Round(heightPx / scaling);
            yPx = bounds.Y + bounds.Height - heightPx;
        }
        else
        {
            heightDip = configHeightDip;
            yPx = bounds.Y;
        }

        Height = heightDip;
        Position = new PixelPoint(bounds.X, yPx);

        if (_progressBar is not null) _progressBar.Height = heightDip;
        UpdateProgressBarWidth();

        // Sitting over the top-most taskbar — nudge above it now; the policy
        // keeps re-asserting on its fast cadence while in Bottom mode.
        // SetTopmost is platform/handle-guarded, so a null handle is a no-op.
        if (position == BarPosition.Bottom)
        {
            NativeMethods.SetTopmost(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        }
    }

    /// <summary>
    /// Height (physical px) of the Windows taskbar on the monitor that contains
    /// <paramref name="screenBounds"/>, or 0 if not on Windows / not found / the
    /// taskbar isn't a visible bottom taskbar on this monitor (incl. auto-hidden).
    /// Callers fall back to the configured bar height when this returns 0.
    /// </summary>
    private static int TaskbarHeightOnScreen(PixelRect screenBounds)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return 0;

        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return 0;
        if (!NativeMethods.GetWindowRect(tray, out var r)) return 0;

        var screenBottom = screenBounds.Y + screenBounds.Height;
        var overlapsX = r.right > screenBounds.X && r.left < screenBounds.X + screenBounds.Width;
        var atBottom = r.bottom >= screenBottom - 4;
        if (!overlapsX || !atBottom) return 0;

        // Auto-hidden taskbars slide off-screen: measure only the visible overlap
        // and treat a negligible sliver as "absent" so we fall back to the bar height.
        var visibleTop = Math.Max(r.top, screenBounds.Y);
        var visibleHeight = screenBottom - visibleTop;
        return visibleHeight > 2 ? visibleHeight : 0;
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
        // Called from both OnDataContextChanged (fires when DataContext is set,
        // before Show) and OnOpened. The first caller to find a bound controller
        // wins; the null/already-set guard makes the duplicate call a no-op. The
        // policy tolerates being created pre-open — its tick early-returns until
        // the platform handle exists.
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
        // Read EffectiveSettings everywhere the bar is *rendered* — clamps Pro
        // values (gradient, custom color, position, always-above) to free
        // behavior without losing the stored values.
        var settings = _boundController.EffectiveSettings;
        Opacity = settings.Opacity;
        ApplyScreenLayout();
        ApplyBarColor();
    }

    private void ApplyBarColor()
    {
        if (_progressBar is null || _boundController is null) return;
        if (_animator is { IsActive: true }) return; // animator owns the brush mid-sequence
        var rgb = BarColorPalette.ForProgress(_boundController.EffectiveSettings, _boundController.Progress);
        if (_barBrush is null || _lastBarRgb != rgb)
        {
            _barBrush = ToBrush(rgb);
            _lastBarRgb = rgb;
        }
        _progressBar.Fill = _barBrush;
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
            Opacity = _boundController.EffectiveSettings.Opacity;
            ApplyBarColor();
        }
    }

    private void OnTimerCompleted()
    {
        if (_progressBar is null || _boundController is null) return;
        _animator?.Cancel();
        var effective = _boundController.EffectiveSettings;
        var resting = ToBrush(BarColorPalette.ForProgress(effective, 1.0));
        _animator = new CompletionAnimator(this, _progressBar, resting, FlashBrush, effective.Opacity);
        _animator.Run();
    }

    private void UpdateProgressBarWidth()
    {
        if (_progressBar is null) return;
        if (_barWidth <= 0 || double.IsNaN(_barWidth)) return; // not laid out yet
        var progress = _boundController?.Progress ?? 0.0;
        if (progress < 0) progress = 0;
        if (progress > 1) progress = 1;
        _progressBar.Width = _barWidth * progress;
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
}
