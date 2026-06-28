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
}
