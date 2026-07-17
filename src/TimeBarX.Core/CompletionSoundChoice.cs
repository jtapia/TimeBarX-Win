namespace TimeBarX.Core;

/// <summary>
/// Which system sound plays on timer completion. Values map to
/// <c>System.Media.SystemSounds</c> on Windows; other platforms no-op.
/// <c>Default</c> is picked from <see cref="AppSettings.PlayCompletionSound"/>
/// for legacy settings files so upgrading in place doesn't silently mute users
/// who already had sound on.
/// </summary>
public enum CompletionSoundChoice
{
    Off,
    Asterisk,
    Beep,
    Exclamation,
    Hand,
    Question,
}
