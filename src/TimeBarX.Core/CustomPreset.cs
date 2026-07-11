namespace TimeBarX.Core;

/// <summary>
/// A user-saved timer preset that appears in the tray's Start submenu and is
/// editable through Settings. Pro-only — surfaced in the tray menu only when
/// the user is entitled; the stored list survives entitlement flips so a
/// refunded-and-repurchased user gets their presets back.
/// </summary>
/// <param name="Name">Display label in the tray menu (e.g. "Daily standup").</param>
/// <param name="Duration">Timer duration. Must be positive; the menu rejects non-positive entries.</param>
/// <param name="Label">Optional tooltip/label shown in the tray while the timer runs. Distinct from <paramref name="Name"/>: name is what you click, label is what you see.</param>
/// <param name="CompletionSound">
///   Per-preset override for the completion sound. When null, the app-level
///   <c>AppSettings.EffectiveCompletionSound</c> is used, so existing presets
///   pick up the user's global preference automatically.
/// </param>
/// <param name="AlertMessage">
///   Optional message to display in the completion overlay/tray for this
///   preset. Currently surfaced via the tooltip; a future release will render
///   it in the completion overlay.
/// </param>
public sealed record CustomPreset(
    string Name,
    TimeSpan Duration,
    string? Label = null,
    CompletionSoundChoice? CompletionSound = null,
    string? AlertMessage = null)
{
    /// <summary>True iff this preset is well-formed enough to be saved/run.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name)
        && Duration > TimeSpan.Zero;
}
