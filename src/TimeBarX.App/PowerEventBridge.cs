using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TimeBarX.App;

/// <summary>
/// Hooks Windows power-mode change notifications and invokes the supplied
/// callback on Resume. No-op on non-Windows platforms.
/// </summary>
public sealed class PowerEventBridge : IDisposable
{
    private readonly Action _onResume;
    private bool _hooked;

    public PowerEventBridge(Action onResume)
    {
        _onResume = onResume;
    }

    public void Attach()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        AttachWindows();
    }

    public void Dispose()
    {
        if (!_hooked) return;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        DetachWindows();
    }

    [SupportedOSPlatform("windows")]
    private void AttachWindows()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _hooked = true;
    }

    [SupportedOSPlatform("windows")]
    private void DetachWindows()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _hooked = false;
    }

    [SupportedOSPlatform("windows")]
    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            _onResume();
        }
    }
}
