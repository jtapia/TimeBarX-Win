using System.Collections.Generic;

namespace TimeBarX.App;

/// <summary>
/// A single jump-list entry: appears under the app's taskbar/Start icon,
/// activates a timebarx:// URI when clicked.
/// </summary>
/// <param name="Title">Text shown in the jump list.</param>
/// <param name="Uri">timebarx:// command URI launched on click.</param>
public readonly record struct JumpListEntry(string Title, string Uri);

/// <summary>
/// Publishes the app's jump list — the menu that appears on right-click of the
/// taskbar/Start icon. Must degrade to a no-op (never throw) on non-Windows,
/// on unpackaged builds without a shortcut, or when the shell rejects the list.
/// </summary>
public interface IJumpList
{
    /// <summary>
    /// Replace the current jump list with the given entries. Called once at
    /// app startup — the list is static (Start 25m, Pause, Resume, Stop, …).
    /// </summary>
    void Publish(IReadOnlyList<JumpListEntry> entries);
}

/// <summary>No-op jump list for non-Windows builds and when publishing fails.</summary>
public sealed class NullJumpList : IJumpList
{
    public void Publish(IReadOnlyList<JumpListEntry> entries)
    {
    }
}

/// <summary>
/// Canonical set of jump-list entries. Kept as a single method so a future
/// edit (add "Start 50 min", change verbs) doesn't have to be mirrored between
/// the wiring site and any test that verifies the URIs are round-trippable.
///
/// Entries intentionally use the same timebarx:// commands the URL-scheme
/// handler already accepts, so activation from the jump list re-enters the
/// running app (or launches it) through <c>App.HandleUri</c> — no new
/// entrypoint to keep in sync.
/// </summary>
public static class JumpListEntries
{
    // No Pomodoro entry: UriCommandKind has no dedicated Pomodoro verb, and
    // "start?duration=25m&label=Pomodoro" would look right in the jump list
    // but silently start a bare labeled timer without the phase auto-advance
    // chain — that mismatch is worse than not offering the entry at all. Add
    // a real timebarx://pomodoro/start verb first, then wire the entry.
    //
    // trustQuery: the app's per-launch own-UI trust fragment (see
    // App.OwnUiTrustQuery). Jump-list clicks are the user clicking our own UI,
    // not Pro-gated automation, so without it HandleUri silently no-ops every
    // entry for free users. Stamped at publish (the list is republished each
    // startup), it matches the running process; a click that cold-starts the
    // app carries the previous launch's value and stays gated — same as before,
    // never worse. UriCommand.TryParse ignores unknown query params on all verbs.
    public static IReadOnlyList<JumpListEntry> Default(string? trustQuery = null) => new[]
    {
        new JumpListEntry("Start 25-minute timer", WithTrust("timebarx://start?duration=25m", trustQuery)),
        new JumpListEntry("Start 50-minute timer", WithTrust("timebarx://start?duration=50m", trustQuery)),
        new JumpListEntry("Pause current timer",   WithTrust("timebarx://pause", trustQuery)),
        new JumpListEntry("Resume current timer",  WithTrust("timebarx://resume", trustQuery)),
        new JumpListEntry("Stop current timer",    WithTrust("timebarx://stop", trustQuery)),
    };

    private static string WithTrust(string uri, string? trustQuery)
        => string.IsNullOrEmpty(trustQuery)
            ? uri
            : uri + (uri.Contains('?') ? "&" : "?") + trustQuery;
}
