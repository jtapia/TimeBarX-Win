using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TimeBarX.App;

/// <summary>
/// Plays a short system notification sound on completion. Windows-only;
/// silently no-ops on other platforms.
/// </summary>
public static class CompletionSound
{
    public static void Play()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        PlayWindows();
    }

    [SupportedOSPlatform("windows")]
    private static void PlayWindows()
    {
        System.Media.SystemSounds.Asterisk.Play();
    }
}
