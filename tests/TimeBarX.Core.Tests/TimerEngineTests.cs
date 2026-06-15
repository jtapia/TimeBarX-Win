using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class TimerEngineTests
{
    [Fact]
    public void New_engine_is_idle()
    {
        var engine = new TimerEngine(new FakeClock());
        Assert.Equal(TimerState.Idle, engine.State);
        Assert.Equal(0.0, engine.Progress);
    }

    [Fact]
    public void Start_transitions_to_running_and_records_total()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);

        engine.Start(TimeSpan.FromMinutes(25));

        Assert.Equal(TimerState.Running, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Total);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.Remaining);
    }

    [Fact]
    public void Start_with_zero_or_negative_duration_throws()
    {
        var engine = new TimerEngine(new FakeClock());
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Start(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Start(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Elapsed_advances_with_clock()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        clock.Advance(TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(3), engine.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(7), engine.Remaining);
    }

    [Fact]
    public void Progress_is_elapsed_over_total()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(0.2, engine.Progress, 3);
    }

    [Fact]
    public void Progress_always_between_zero_and_one()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromMinutes(1));
        engine.Tick();

        Assert.InRange(engine.Progress, 0.0, 1.0);
        Assert.Equal(1.0, engine.Progress);
    }

    [Fact]
    public void Pause_freezes_elapsed_then_resume_continues()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));

        clock.Advance(TimeSpan.FromMinutes(2));
        engine.Pause();
        Assert.Equal(TimerState.Paused, engine.State);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(2), engine.Elapsed);

        engine.Resume();
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(TimerState.Running, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(3), engine.Elapsed);
    }

    [Fact]
    public void Tick_completes_timer_when_time_elapsed()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromSeconds(30));

        clock.Advance(TimeSpan.FromSeconds(30));
        engine.Tick();

        Assert.Equal(TimerState.Completed, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.Remaining);
        Assert.Equal(1.0, engine.Progress);
    }

    [Fact]
    public void Stop_resets_to_idle()
    {
        var clock = new FakeClock();
        var engine = new TimerEngine(clock);
        engine.Start(TimeSpan.FromMinutes(10));
        clock.Advance(TimeSpan.FromMinutes(2));

        engine.Stop();

        Assert.Equal(TimerState.Idle, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.Elapsed);
        Assert.Equal(0.0, engine.Progress);
    }

    [Fact]
    public void Pause_is_noop_when_not_running()
    {
        var engine = new TimerEngine(new FakeClock());
        engine.Pause();
        Assert.Equal(TimerState.Idle, engine.State);
    }

    [Fact]
    public void Resume_is_noop_when_not_paused()
    {
        var engine = new TimerEngine(new FakeClock());
        engine.Resume();
        Assert.Equal(TimerState.Idle, engine.State);
    }
}
