namespace TimeBarX.Core;

public enum PomodoroPhase
{
    Work,
    ShortBreak,
    LongBreak,
}

/// <summary>
/// User-configurable durations and cadence for a Pomodoro cycle. Enabling
/// Pomodoro means new timers Start() runs the cycle: Work → ShortBreak →
/// Work → ShortBreak → … → Work → LongBreak → Work (rolls over). The cycle
/// is stored on <see cref="AppSettings"/> so it survives restarts, but the
/// current position (<see cref="PomodoroState"/>) lives in the timer store.
/// </summary>
/// <param name="Enabled">Master toggle. When false, Start() runs a single-shot timer as before.</param>
/// <param name="WorkMinutes">Length of a work phase (1..240).</param>
/// <param name="ShortBreakMinutes">Length of a short break (1..120).</param>
/// <param name="LongBreakMinutes">Length of a long break (1..240).</param>
/// <param name="LongBreakEvery">Work phases before a long break (2..12).</param>
/// <param name="AutoAdvance">When false, each phase must be started manually.</param>
public sealed record PomodoroSettings(
    bool Enabled = false,
    int WorkMinutes = 25,
    int ShortBreakMinutes = 5,
    int LongBreakMinutes = 15,
    int LongBreakEvery = 4,
    bool AutoAdvance = true)
{
    public static PomodoroSettings Default => new();

    public PomodoroSettings Sanitize() => new(
        Enabled: Enabled,
        WorkMinutes: Clamp(WorkMinutes, 1, 240, 25),
        ShortBreakMinutes: Clamp(ShortBreakMinutes, 1, 120, 5),
        LongBreakMinutes: Clamp(LongBreakMinutes, 1, 240, 15),
        LongBreakEvery: Clamp(LongBreakEvery, 2, 12, 4),
        AutoAdvance: AutoAdvance);

    /// <summary>Duration for a given phase.</summary>
    public TimeSpan DurationFor(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Work => TimeSpan.FromMinutes(WorkMinutes),
        PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(ShortBreakMinutes),
        PomodoroPhase.LongBreak => TimeSpan.FromMinutes(LongBreakMinutes),
        _ => TimeSpan.FromMinutes(WorkMinutes),
    };

    /// <summary>Given the current phase and how many work sessions have been completed, return the next phase.</summary>
    public PomodoroPhase NextPhase(PomodoroPhase current, int completedWorkSessions) => current switch
    {
        PomodoroPhase.Work => (completedWorkSessions % LongBreakEvery == 0 && completedWorkSessions > 0)
            ? PomodoroPhase.LongBreak
            : PomodoroPhase.ShortBreak,
        _ => PomodoroPhase.Work,
    };

    private static int Clamp(int value, int min, int max, int fallback)
    {
        if (value < min || value > max) return fallback;
        return value;
    }
}
