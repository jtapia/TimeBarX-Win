using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class PomodoroSettingsTests
{
    [Fact]
    public void Default_is_disabled_with_sane_values()
    {
        var d = PomodoroSettings.Default;
        Assert.False(d.Enabled);
        Assert.Equal(25, d.WorkMinutes);
        Assert.Equal(5, d.ShortBreakMinutes);
        Assert.Equal(15, d.LongBreakMinutes);
        Assert.Equal(4, d.LongBreakEvery);
        Assert.True(d.AutoAdvance);
    }

    [Fact]
    public void Sanitize_replaces_out_of_range_with_fallback()
    {
        var bad = new PomodoroSettings(
            Enabled: true,
            WorkMinutes: 9999,
            ShortBreakMinutes: 0,
            LongBreakMinutes: -1,
            LongBreakEvery: 1,
            AutoAdvance: true);
        var clean = bad.Sanitize();
        Assert.Equal(25, clean.WorkMinutes);
        Assert.Equal(5, clean.ShortBreakMinutes);
        Assert.Equal(15, clean.LongBreakMinutes);
        Assert.Equal(4, clean.LongBreakEvery);
    }

    [Fact]
    public void NextPhase_cycles_work_and_break()
    {
        var pomo = PomodoroSettings.Default with { LongBreakEvery = 4 };

        // Work #1 → ShortBreak; the caller passes completedWorkSessions
        // AFTER incrementing so 1..3 → ShortBreak, 4 → LongBreak.
        Assert.Equal(PomodoroPhase.ShortBreak, pomo.NextPhase(PomodoroPhase.Work, 1));
        Assert.Equal(PomodoroPhase.ShortBreak, pomo.NextPhase(PomodoroPhase.Work, 2));
        Assert.Equal(PomodoroPhase.ShortBreak, pomo.NextPhase(PomodoroPhase.Work, 3));
        Assert.Equal(PomodoroPhase.LongBreak, pomo.NextPhase(PomodoroPhase.Work, 4));
        Assert.Equal(PomodoroPhase.LongBreak, pomo.NextPhase(PomodoroPhase.Work, 8));

        // Any break → Work.
        Assert.Equal(PomodoroPhase.Work, pomo.NextPhase(PomodoroPhase.ShortBreak, 3));
        Assert.Equal(PomodoroPhase.Work, pomo.NextPhase(PomodoroPhase.LongBreak, 4));
    }

    [Fact]
    public void DurationFor_returns_correct_span_per_phase()
    {
        var pomo = new PomodoroSettings(true, 30, 6, 20, 4, true);
        Assert.Equal(TimeSpan.FromMinutes(30), pomo.DurationFor(PomodoroPhase.Work));
        Assert.Equal(TimeSpan.FromMinutes(6), pomo.DurationFor(PomodoroPhase.ShortBreak));
        Assert.Equal(TimeSpan.FromMinutes(20), pomo.DurationFor(PomodoroPhase.LongBreak));
    }
}
