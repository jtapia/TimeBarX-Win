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

namespace TimeBarX.App;

public partial class OverlayWindow : Window
{
    private const int BarHeight = 3;

    private static readonly IBrush DefaultBarBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush FlashBrush = Brushes.White;

    private Rectangle? _progressBar;
    private TrayController? _boundController;
    private CompletionAnimator? _animator;

    public OverlayWindow()
    {
        InitializeComponent();
        _progressBar = this.FindControl<Rectangle>("ProgressBar");
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void PositionOnScreen(Screen screen)
    {
        var bounds = screen.Bounds;
        var scaling = screen.Scaling > 0 ? screen.Scaling : 1.0;

        Width = bounds.Width / scaling;
        Height = BarHeight;
        Position = new PixelPoint(bounds.X, bounds.Y);

        UpdateProgressBarWidth();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyClickThrough();
        UpdateProgressBarWidth();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundController is not null)
        {
            _boundController.PropertyChanged -= OnControllerPropertyChanged;
            _boundController.Completed -= OnTimerCompleted;
        }

        _boundController = DataContext as TrayController;

        if (_boundController is not null)
        {
            _boundController.PropertyChanged += OnControllerPropertyChanged;
            _boundController.Completed += OnTimerCompleted;
        }

        UpdateProgressBarWidth();
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrayController.Progress) || e.PropertyName == nameof(TrayController.IsBarVisible))
        {
            UpdateProgressBarWidth();
        }

        if (e.PropertyName == nameof(TrayController.IsBarVisible) && _boundController is { IsBarVisible: true })
        {
            // A new timer started — cancel any in-flight completion animation
            // and restore opacity/color.
            _animator?.Cancel();
            _animator = null;
            Opacity = 1.0;
            if (_progressBar is not null) _progressBar.Fill = DefaultBarBrush;
        }
    }

    private void OnTimerCompleted()
    {
        if (_progressBar is null) return;
        _animator?.Cancel();
        _animator = new CompletionAnimator(this, _progressBar, DefaultBarBrush, FlashBrush);
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

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;

        var current = GetWindowLongPtr(handle.Value, GWL_EXSTYLE).ToInt64();
        var updated = current | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(handle.Value, GWL_EXSTYLE, new IntPtr(updated));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
