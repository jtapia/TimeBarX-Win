using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class TimerEngineRehydrationTests
{
    [Fact]
    public void StartAt_resumes_running_with_correct_remaining()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);

        var endTime = clock.UtcNow + TimeSpan.FromMinutes(7);
        engine.StartAt(endTime, TimeSpan.FromMinutes(25));

        Assert.Equal(TimerState.Running, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(7), engine.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(18), engine.Elapsed);
    }

    [Fact]
    public void StartAt_with_past_endtime_completes_immediately()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);

        engine.StartAt(clock.UtcNow - TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(25));

        Assert.Equal(TimerState.Completed, engine.State);
        Assert.Equal(1.0, engine.Progress);
        Assert.Equal(TimeSpan.Zero, engine.Remaining);
    }

    [Fact]
    public void RestorePaused_rehydrates_paused_state()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);

        engine.RestorePaused(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(25));

        Assert.Equal(TimerState.Paused, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(10), engine.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(15), engine.Remaining);
    }

    [Fact]
    public void RestorePaused_can_be_resumed_normally()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.RestorePaused(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(25));

        engine.Resume();
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(TimerState.Running, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(12), engine.Elapsed);
    }

    [Fact]
    public void Reconcile_completes_timer_after_long_gap()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(25));

        // Simulate sleep: clock jumps past end time without ticks firing.
        clock.Advance(TimeSpan.FromHours(1));
        engine.Reconcile();

        Assert.Equal(TimerState.Completed, engine.State);
    }

    [Fact]
    public void EndTime_is_set_while_running()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        var expected = clock.UtcNow + TimeSpan.FromMinutes(10);
        Assert.Equal(expected, engine.EndTime);
    }

    [Fact]
    public void EndTime_is_null_when_paused()
    {
        var engine = new TimerEngine(new FakeClock());
        engine.Start(TimeSpan.FromMinutes(10));
        engine.Pause();

        Assert.Null(engine.EndTime);
    }
}
