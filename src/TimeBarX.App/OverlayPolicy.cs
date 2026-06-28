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
/// Polls once/sec while the bar is visible and adjusts overlay visibility/Z-order
/// on Windows:
///   - hides the overlay when the foreground window's process is in the configured
///     exclusion list (e.g. video players)
///   - in Top mode, hides for an exclusive-fullscreen app so games/video aren't
///     covered (shell surfaces like the Start menu/Search are excluded); this
///     check is skipped entirely in Bottom mode, where the bar is fused to the
///     taskbar that a real fullscreen app already hides
///   - reasserts HWND_TOPMOST when it must out-rank other top-most windows:
///     "always above everything" mode, or Bottom mode (sitting over the taskbar)
/// All cross-platform no-ops outside Windows.
/// </summary>
public sealed class OverlayPolicy : IDisposable
{
    // Slow cadence is enough for the hide/show heuristics. Bottom mode overlays
    // the top-most taskbar and must out-race Windows reclaiming top Z-order when
    // Start/Search open, so it polls fast.
    private static readonly TimeSpan SlowCadence = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FastCadence = TimeSpan.FromMilliseconds(100);

    private readonly Window _overlay;
    private readonly TrayController _controller;
    private readonly DispatcherTimer _timer;

    public OverlayPolicy(Window overlay, TrayController controller)
    {
        _overlay = overlay;
        _controller = controller;
        _timer = new DispatcherTimer { Interval = SlowCadence };
        _timer.Tick += (_, _) => Tick();

        // The policy only has work to do while a timer is active (the bar is
        // visible). Poll just then — otherwise we'd call GetForegroundWindow +
        // Process.GetProcessById once/sec/monitor forever, even when idle.
        // Re-evaluate on settings changes too, since Position drives the cadence.
        _controller.PropertyChanged += OnControllerPropertyChanged;
        _controller.SettingsChanged += SyncTimerToVisibility;
        SyncTimerToVisibility();
    }

    public void Dispose()
    {
        _controller.PropertyChanged -= OnControllerPropertyChanged;
        _controller.SettingsChanged -= SyncTimerToVisibility;
        _timer.Stop();
    }

    private void OnControllerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrayController.IsBarVisible)) SyncTimerToVisibility();
    }

    private void SyncTimerToVisibility()
    {
        if (!_controller.IsBarVisible)
        {
            _timer.Stop();
            return;
        }

        // Bottom mode needs the fast cadence to keep out-ranking the taskbar.
        var wanted = _controller.Settings.Position == BarPosition.Bottom ? FastCadence : SlowCadence;
        if (_timer.Interval != wanted) _timer.Interval = wanted;
        _timer.Start();
    }

    private void Tick()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var handle = _overlay.TryGetPlatformHandle()?.Handle;
        if (handle is null || handle == IntPtr.Zero) return;

        var settings = _controller.Settings;
        var foreground = GetForegroundWindow();
        var bottomMode = settings.Position == BarPosition.Bottom;

        // Resolve the foreground process name at most once per tick (an OpenProcess
        // handle). Both the hide-list match and Top mode's shell-window exclusion
        // need it; skip it only when neither consumer will run (empty hide-list and
        // Bottom mode, where the fullscreen check doesn't run at all).
        var hideList = settings.HideForProcesses;
        var hasHideList = hideList is { Count: > 0 };
        var foregroundProcess = (hasHideList || !bottomMode) ? ProcessNameForWindow(foreground) : null;

        var hideForProcess = !string.IsNullOrEmpty(foregroundProcess) && hasHideList
            && hideList!.Any(p => string.Equals(p, foregroundProcess, StringComparison.OrdinalIgnoreCase));

        // The fullscreen-hide only serves Top mode (get out of a game/video's way).
        // In Bottom mode the bar is fused to the taskbar — a true exclusive-fullscreen
        // app hides the taskbar itself, so the OS already covers us. Running the
        // heuristic there only produces false positives (Start menu, Search, maximized
        // windows, "app bars") that make the bar flicker away. So skip it in Bottom mode.
        var foregroundIsFullscreen = !bottomMode && IsExclusiveFullscreen(foreground, foregroundProcess, _overlay);

        // Default behavior (PLAN.md Mode 1): hide for known full-screen / video apps.
        // Experimental mode (PLAN.md Mode 2): also push above everything else when possible.
        var shouldHide = hideForProcess || (!settings.AlwaysAboveEverything && foregroundIsFullscreen);

        // Only toggle when the value actually changes — assigning IsVisible every
        // tick re-runs Avalonia's Show() path on Windows, which churns the Z-order
        // and (on Win11) interferes with other apps' minimize/close caption buttons.
        var wantVisible = !shouldHide;
        if (_overlay.IsVisible != wantVisible) _overlay.IsVisible = wantVisible;

        // Reassert top-most when we must out-rank other top-most windows:
        //  - experimental "always above everything" mode, or
        //  - Bottom mode, where we overlay the top-most taskbar and have to win
        //    the Z-order back each time Start/Search/etc. bring it forward.
        var needsTopmost = settings.AlwaysAboveEverything || bottomMode;
        if (needsTopmost && !shouldHide && !foregroundIsFullscreen)
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

    private static bool IsExclusiveFullscreen(IntPtr foreground, string? foregroundProcess, Window overlay)
    {
        if (foreground == IntPtr.Zero) return false;

        // Shell surfaces — desktop (Progman/WorkerW), taskbar (Shell_TrayWnd), and
        // the transient flyouts (Start menu, Search, Action Center) — can report a
        // near-fullscreen rect, which would wrongly trip the bounds check below and
        // hide the bar. None of them is a real "exclusive fullscreen app", so skip
        // them entirely. We match by class AND by owning shell process, because the
        // flyout class names vary across Windows builds. The process name is resolved
        // once by the caller and passed in.
        if (IsShellWindow(foreground) || IsShellProcessName(foregroundProcess)) return false;

        if (!NativeMethods.GetWindowRect(foreground, out var rect)) return false;

        // Compare against the overlay's screen bounds.
        var screen = overlay.Screens?.ScreenFromPoint(new PixelPoint(rect.left, rect.top));
        if (screen is null) return false;
        var b = screen.Bounds;
        return rect.left <= b.X
            && rect.top <= b.Y
            && rect.right >= b.X + b.Width
            && rect.bottom >= b.Y + b.Height;
    }

    // Reused across ticks to avoid allocating a buffer every second. The policy
    // tick always runs on the single UI dispatcher thread, so a plain static is safe.
    private static readonly char[] ClassNameBuffer = new char[256];

    private static bool IsShellWindow(IntPtr hwnd)
    {
        var len = GetClassName(hwnd, ClassNameBuffer, ClassNameBuffer.Length);
        if (len <= 0) return false;
        return new ReadOnlySpan<char>(ClassNameBuffer, 0, len)
            is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    // Process names that own the desktop shell + its transient flyouts (Start
    // menu, Search, Action Center, input/notification surfaces). A foreground
    // window from any of these is shell chrome, not a fullscreen app.
    private static readonly string[] ShellProcessNames =
    {
        "explorer",
        "StartMenuExperienceHost",
        "SearchHost",
        "SearchApp",
        "ShellExperienceHost",
        "TextInputHost",
    };

    private static bool IsShellProcessName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var shell in ShellProcessNames)
        {
            if (string.Equals(name, shell, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // ---- Win32 ----

    private static void ReassertTopmost(IntPtr hwnd) => NativeMethods.SetTopmost(hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);
}
