using System.Diagnostics;

namespace TimeBarX.Core;

public interface IClock
{
    /// <summary>Wall-clock time. Used for absolute end times that must survive a process restart.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// A monotonic timestamp that never runs backwards, unaffected by NTP corrections or the
    /// user changing the system clock. Used to measure elapsed time within a single running
    /// session so the bar can't rewind. May freeze while the machine sleeps.
    /// </summary>
    TimeSpan MonotonicNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeSpan MonotonicNow => Stopwatch.GetElapsedTime(0);
}
