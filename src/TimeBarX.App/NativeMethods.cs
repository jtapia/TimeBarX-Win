using System;
using System.Runtime.InteropServices;

namespace TimeBarX.App;

/// <summary>
/// Win32 interop shared by the overlay windows and the overlay policy. Keeping
/// the common P/Invoke signatures and constants here avoids the two files
/// drifting out of sync. All members are no-ops / unused outside Windows.
/// </summary>
internal static class NativeMethods
{
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>
    /// SetWindowPos with the no-move/size/activate flags used to (re)assert top-most.
    /// Safe to call from anywhere: no-ops outside Windows and for a zero handle, so
    /// callers don't need to repeat the platform/handle guards.
    /// </summary>
    public static void SetTopmost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    /// <summary>
    /// Seconds since the user last provided keyboard or mouse input, according
    /// to Win32. Returns <see cref="TimeSpan.Zero"/> outside Windows or on API
    /// failure so idle-based features cleanly no-op. The wraparound of the
    /// 32-bit tick counters (~49.7 days) is handled by unsigned subtraction.
    /// </summary>
    public static TimeSpan GetIdleTime()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return TimeSpan.Zero;

        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;

        var now = GetTickCount();
        var elapsedMs = now - info.dwTime; // unsigned wraparound math
        return TimeSpan.FromMilliseconds(elapsedMs);
    }

    private const uint SPI_GETSCREENSAVERRUNNING = 0x0072;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    /// <summary>
    /// True when Windows reports a screensaver is currently running on the user's
    /// desktop. Used by the overlay policy to reassert top-most Z-order so the
    /// progress bar stays visible above the saver window. Returns false outside
    /// Windows or on API failure. Note: when "require sign-in on resume" is on,
    /// the saver runs on the secure desktop and this still returns true, but no
    /// user-mode window can render there — the caller degrades gracefully.
    /// </summary>
    public static bool IsScreenSaverRunning()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        bool running = false;
        return SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref running, 0) && running;
    }
}
