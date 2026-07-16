using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class TimerEngineClockSkewTests
{
    [Fact]
    public void Backward_wall_clock_jump_does_not_rewind_progress()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        // 4 minutes of real time pass (monotonic advances with wall).
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(TimeSpan.FromMinutes(4), engine.Elapsed);

        // The system clock is moved back an hour (NTP correction / user change).
        clock.AdvanceWallOnly(TimeSpan.FromHours(-1));

        // Monotonic time is unaffected, so elapsed must not rewind.
        Assert.Equal(TimeSpan.FromMinutes(4), engine.Elapsed);
        Assert.True(engine.Progress > 0.0);
    }

    [Fact]
    public void Forward_wall_clock_jump_while_monotonic_frozen_counts_sleep()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        // Machine sleeps 6 minutes: wall advances, monotonic frozen.
        clock.AdvanceWallOnly(TimeSpan.FromMinutes(6));

        Assert.Equal(TimeSpan.FromMinutes(6), engine.Elapsed);
    }

    [Fact]
    public void Monotonic_progress_counts_even_if_wall_clock_stalls()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        clock.AdvanceMonotonicOnly(TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(3), engine.Elapsed);
    }

    [Fact]
    public void EndTime_stays_consistent_with_elapsed_after_backward_clock_jump()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        clock.Advance(TimeSpan.FromMinutes(4));
        // System clock is moved back an hour; monotonic elapsed stays at 4 min.
        clock.AdvanceWallOnly(TimeSpan.FromHours(-1));
        Assert.Equal(TimeSpan.FromMinutes(4), engine.Elapsed);

        // EndTime must project only the real 6 minutes remaining, not be pushed
        // ~66 minutes out by the rewound wall clock.
        var end = engine.EndTime;
        Assert.NotNull(end);
        Assert.Equal(TimeSpan.FromMinutes(6), end!.Value - clock.UtcNow);
    }

    [Fact]
    public void Rehydrating_from_saved_endtime_after_skew_preserves_progress()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));
        clock.Advance(TimeSpan.FromMinutes(4));
        clock.AdvanceWallOnly(TimeSpan.FromHours(-1));

        // Persist EndTime, then rehydrate into a fresh engine sharing the (now
        // rewound) wall clock — simulating an app restart right after the jump.
        var savedEnd = engine.EndTime!.Value;
        var restored = new TimerEngine(clock);
        restored.StartAt(savedEnd, TimeSpan.FromMinutes(10));

        Assert.Equal(TimerState.Running, restored.State);
        // The 4 minutes of progress must survive the round-trip, not reset to 0.
        Assert.Equal(TimeSpan.FromMinutes(4), restored.Elapsed);
    }

    [Fact]
    public void Pause_after_expiry_completes_instead_of_stranding()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(5));

        // Timer runs out during sleep; Pause is called before Tick.
        clock.Advance(TimeSpan.FromMinutes(6));
        engine.Pause();

        Assert.Equal(TimerState.Completed, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.Elapsed);
    }

    [Fact]
    public void Pause_before_expiry_still_pauses()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(5));

        clock.Advance(TimeSpan.FromMinutes(2));
        engine.Pause();

        Assert.Equal(TimerState.Paused, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(2), engine.Elapsed);
    }
}
