using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TimeBarX.Core;

namespace TimeBarX.App;

/// <summary>
/// Plays the user's chosen system notification sound on completion.
/// Windows-only; silently no-ops on other platforms and for Off.
/// </summary>
public static class CompletionSound
{
    public static void Play(CompletionSoundChoice choice)
    {
        if (choice == CompletionSoundChoice.Off) return;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        PlayWindows(choice);
    }

    [SupportedOSPlatform("windows")]
    private static void PlayWindows(CompletionSoundChoice choice)
    {
        var sound = choice switch
        {
            CompletionSoundChoice.Beep => System.Media.SystemSounds.Beep,
            CompletionSoundChoice.Exclamation => System.Media.SystemSounds.Exclamation,
            CompletionSoundChoice.Hand => System.Media.SystemSounds.Hand,
            CompletionSoundChoice.Question => System.Media.SystemSounds.Question,
            _ => System.Media.SystemSounds.Asterisk,
        };
        sound.Play();
    }
}
