namespace TimeBarX.Core;

public sealed record TimerSnapshot(
    TimerState State,
    DateTimeOffset? EndTime,
    TimeSpan Total,
    TimeSpan ElapsedAtPause,
    string? Preset,
    string? Label = null
);
