namespace TimeBarX.Core;

/// <summary>
/// How long the user has been idle at the OS level (no keyboard / mouse input).
/// Split out so <see cref="TimerEngine"/> and its host can be unit-tested with
/// a fake, and so the Windows-only <c>GetLastInputInfo</c> P/Invoke lives on the
/// App side rather than leaking into Core.
/// </summary>
public interface IIdleProbe
{
    TimeSpan GetIdleTime();
}

/// <summary>Default: reports zero idle time, i.e. auto-pause never fires.</summary>
public sealed class NullIdleProbe : IIdleProbe
{
    public TimeSpan GetIdleTime() => TimeSpan.Zero;
}
