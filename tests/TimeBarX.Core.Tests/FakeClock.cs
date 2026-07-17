using TimeBarX.Core;

namespace TimeBarX.Core.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TimeSpan MonotonicNow { get; set; } = TimeSpan.Zero;

    /// <summary>Advances both wall-clock and monotonic time — the normal case.</summary>
    public void Advance(TimeSpan delta)
    {
        UtcNow += delta;
        MonotonicNow += delta;
    }

    /// <summary>Advances only the monotonic clock (e.g. the wall clock was moved backwards).</summary>
    public void AdvanceMonotonicOnly(TimeSpan delta) => MonotonicNow += delta;

    /// <summary>Advances only the wall clock (e.g. machine slept, freezing the monotonic clock).</summary>
    public void AdvanceWallOnly(TimeSpan delta) => UtcNow += delta;
}
