using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using TimeBarX.Core;

namespace TimeBarX.App;

/// <summary>
/// Drives experimental "always above everything" behavior on Windows:
///   - reasserts HWND_TOPMOST periodically while enabled
///   - hides the overlay when the foreground window belongs to a process in
///     the configured exclusion list (e.g. video players, full-screen games)
///   - skips top-most poking while an exclusive-fullscreen window covers the
///     overlay's monitor, so games don't lose focus
/// All cross-platform no-ops outside Windows.
/// </summary>
public sealed class OverlayPolicy : IDisposable
{
    private static readonly TimeSpan Cadence = TimeSpan.FromSeconds(1);

    private readonly Window _overlay;
    private readonly TrayController _controller;
    private readonly DispatcherTimer _timer;

    public OverlayPolicy(Window overlay, TrayController controller)
    {
        _overlay = overlay;
        _controller = controller;
        _timer = new DispatcherTimer { Interval = Cadence };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    private void Tick()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var handle = _overlay.TryGetPlatformHandle()?.Handle;
        if (handle is null || handle == IntPtr.Zero) return;

        var settings = _controller.Settings;
        var foreground = GetForegroundWindow();
        var foregroundProcess = ProcessNameForWindow(foreground);

        var hideForProcess = !string.IsNullOrEmpty(foregroundProcess)
            && settings.HideForProcesses is { } list
            && list.Any(p => string.Equals(p, foregroundProcess, StringComparison.OrdinalIgnoreCase));

        var foregroundIsFullscreen = IsExclusiveFullscreen(foreground, _overlay);

        // Default behavior (PLAN.md Mode 1): hide for known full-screen / video apps.
        // Experimental mode (PLAN.md Mode 2): also push above everything else when possible.
        var shouldHide = hideForProcess || (!settings.AlwaysAboveEverything && foregroundIsFullscreen);

        // Only toggle when the value actually changes — assigning IsVisible every
        // tick re-runs Avalonia's Show() path on Windows, which churns the Z-order
        // and (on Win11) interferes with other apps' minimize/close caption buttons.
        var wantVisible = !shouldHide;
        if (_overlay.IsVisible != wantVisible) _overlay.IsVisible = wantVisible;

        if (settings.AlwaysAboveEverything && !shouldHide && !foregroundIsFullscreen)
        {
            ReassertTopmost(handle.Value);
        }
    }

    private static string? ProcessNameForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsExclusiveFullscreen(IntPtr foreground, Window overlay)
    {
        if (foreground == IntPtr.Zero) return false;

        // The desktop shell (Progman / WorkerW) and the shell tray also span the
        // whole screen, so the bounds check below would treat "clicking the
        // desktop" as exclusive-fullscreen and hide the bar. Exclude them: only
        // genuine app windows should ever suppress the overlay.
        if (IsShellWindow(foreground)) return false;

        if (!GetWindowRect(foreground, out var rect)) return false;

        // Compare against the overlay's screen bounds.
        var screen = overlay.Screens?.ScreenFromPoint(new PixelPoint(rect.left, rect.top));
        if (screen is null) return false;
        var b = screen.Bounds;
        return rect.left <= b.X
            && rect.top <= b.Y
            && rect.right >= b.X + b.Width
            && rect.bottom >= b.Y + b.Height;
    }

    private static bool IsShellWindow(IntPtr hwnd)
    {
        var buffer = new char[256];
        var len = GetClassName(hwnd, buffer, buffer.Length);
        if (len <= 0) return false;
        var className = new string(buffer, 0, len);
        return className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    // ---- Win32 ----

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static void ReassertTopmost(IntPtr hwnd)
    {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
