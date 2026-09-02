namespace TimeBarX.Core;

/// <summary>
/// Pure trial-clock logic: given when the trial started and how long it lasts,
/// decides whether the trial is currently active. Kept free of any I/O or
/// platform types so it is fully unit-testable; the App layer owns persistence
/// of <see cref="StartedUtc"/> and wraps this in an <c>IEntitlements</c>.
///
/// <para>
/// The window is <c>[StartedUtc, StartedUtc + Length)</c> — active from the
/// first launch up to (but not including) the expiry instant. A
/// <see cref="StartedUtc"/> in the future (the machine clock was wrong at first
/// run, or was later rolled back past the stamp) is treated as "just started"
/// rather than expired, so a bad clock never silently burns the trial.
/// </para>
/// </summary>
public readonly record struct TrialWindow(DateTimeOffset StartedUtc, TimeSpan Length)
{
    /// <summary>The default trial length: 7 days (matches the Store free-trial period).</summary>
    public static readonly TimeSpan DefaultLength = TimeSpan.FromDays(7);

    /// <summary>A trial of the default length starting at <paramref name="startedUtc"/>.</summary>
    public static TrialWindow Starting(DateTimeOffset startedUtc)
        => new(startedUtc, DefaultLength);

    /// <summary>The instant the trial expires (exclusive).</summary>
    public DateTimeOffset ExpiresUtc => StartedUtc + Length;

    /// <summary>
    /// True while the trial is running. Before <see cref="StartedUtc"/> (future
    /// start / clock rollback) counts as active — never expired-by-bad-clock.
    /// </summary>
    public bool IsActive(DateTimeOffset now)
    {
        if (Length <= TimeSpan.Zero) return false;
        if (now < StartedUtc) return true; // future start: treat as just begun
        return now < ExpiresUtc;
    }

    /// <summary>How long is left, floored at zero. Zero once expired.</summary>
    public TimeSpan Remaining(DateTimeOffset now)
    {
        var left = ExpiresUtc - now;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }
}
