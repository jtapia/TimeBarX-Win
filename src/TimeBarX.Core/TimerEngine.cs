namespace TimeBarX.Core;

public sealed class TimerEngine
{
    private readonly IClock _clock;

    private DateTimeOffset? _startedAt;
    private TimeSpan? _monotonicStartedAt;
    private TimeSpan _total;
    private TimeSpan _accumulatedElapsed;

    public TimerEngine(IClock? clock = null)
    {
        _clock = clock ?? new SystemClock();
    }

    public TimerState State { get; private set; } = TimerState.Idle;

    public TimeSpan Total => _total;

    public TimeSpan Elapsed
    {
        get
        {
            var raw = State switch
            {
                TimerState.Idle => TimeSpan.Zero,
                TimerState.Paused => _accumulatedElapsed,
                TimerState.Completed => _total,
                TimerState.Running => _accumulatedElapsed + RunningDelta(),
                _ => TimeSpan.Zero,
            };

            if (raw < TimeSpan.Zero) return TimeSpan.Zero;
            if (raw >= _total)
            {
                return _total;
            }
            return raw;
        }
    }

    /// <summary>
    /// Elapsed time within the current running segment. Uses the larger of the monotonic
    /// delta and the wall-clock delta: the monotonic clock keeps the bar from rewinding when
    /// the system clock is moved backwards (NTP/manual), while the wall clock still counts
    /// time the machine spent asleep (when the monotonic clock is frozen). Never negative.
    /// </summary>
    private TimeSpan RunningDelta()
    {
        var wall = _clock.UtcNow - _startedAt!.Value;
        if (wall < TimeSpan.Zero) wall = TimeSpan.Zero;

        if (_monotonicStartedAt is not TimeSpan monoStart) return wall;
        var mono = _clock.MonotonicNow - monoStart;
        if (mono < TimeSpan.Zero) mono = TimeSpan.Zero;

        return mono > wall ? mono : wall;
    }

    public TimeSpan Remaining => _total - Elapsed;

    public double Progress
    {
        get
        {
            if (_total <= TimeSpan.Zero) return 0.0;
            var p = Elapsed.TotalSeconds / _total.TotalSeconds;
            if (p < 0.0) return 0.0;
            if (p > 1.0) return 1.0;
            return p;
        }
    }

    public void Start(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");

        _total = duration;
        _accumulatedElapsed = TimeSpan.Zero;
        _startedAt = _clock.UtcNow;
        _monotonicStartedAt = _clock.MonotonicNow;
        State = TimerState.Running;
    }

    /// <summary>
    /// Resumes a running timer that was previously persisted, given its absolute end time
    /// and original total duration. If <paramref name="endTime"/> is already in the past,
    /// the engine transitions to Completed.
    /// </summary>
    public void StartAt(DateTimeOffset endTime, TimeSpan total)
    {
        if (total <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(total), "Total must be positive.");

        _total = total;

        var now = _clock.UtcNow;
        if (endTime <= now)
        {
            _accumulatedElapsed = total;
            _startedAt = null;
            _monotonicStartedAt = null;
            State = TimerState.Completed;
            return;
        }

        var remaining = endTime - now;
        if (remaining > total) remaining = total;
        _accumulatedElapsed = total - remaining;
        _startedAt = now;
        // No monotonic anchor across a process restart: the absolute wall-clock end
        // time is authoritative for a rehydrated timer, so RunningDelta falls back to it.
        _monotonicStartedAt = null;
        State = TimerState.Running;
    }

    public DateTimeOffset? EndTime => State switch
    {
        TimerState.Running => _startedAt!.Value + (_total - _accumulatedElapsed),
        TimerState.Paused => null,
        _ => null,
    };

    /// <summary>
    /// Rehydrates a paused timer with its captured elapsed and total.
    /// </summary>
    public void RestorePaused(TimeSpan elapsed, TimeSpan total)
    {
        if (total <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(total), "Total must be positive.");
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed > total) elapsed = total;

        _total = total;
        _accumulatedElapsed = elapsed;
        _startedAt = null;
        _monotonicStartedAt = null;
        State = TimerState.Paused;
    }

    /// <summary>
    /// Re-evaluates the running clock — used after sleep/wake or other long gaps
    /// where Tick may not have fired in time.
    /// </summary>
    public void Reconcile() => Tick();

    public void Pause()
    {
        if (State != TimerState.Running) return;

        _accumulatedElapsed += RunningDelta();
        _startedAt = null;
        _monotonicStartedAt = null;

        // If the timer already ran out (e.g. it expired while the machine slept and
        // Pause was called before Tick), complete it rather than stranding it at
        // Paused-100%, which would never fire completion.
        if (_accumulatedElapsed >= _total)
        {
            _accumulatedElapsed = _total;
            State = TimerState.Completed;
            return;
        }

        State = TimerState.Paused;
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;

        _startedAt = _clock.UtcNow;
        _monotonicStartedAt = _clock.MonotonicNow;
        State = TimerState.Running;
    }

    public void Stop()
    {
        _startedAt = null;
        _monotonicStartedAt = null;
        _accumulatedElapsed = TimeSpan.Zero;
        _total = TimeSpan.Zero;
        State = TimerState.Idle;
    }

    /// <summary>
    /// Refreshes derived state. Call from a UI refresh loop; transitions to Completed when time is up.
    /// </summary>
    public void Tick()
    {
        if (State == TimerState.Running && Elapsed >= _total)
        {
            _accumulatedElapsed = _total;
            _startedAt = null;
            _monotonicStartedAt = null;
            State = TimerState.Completed;
        }
    }
}
