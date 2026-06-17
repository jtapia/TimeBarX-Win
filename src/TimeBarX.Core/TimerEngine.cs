namespace TimeBarX.Core;

public sealed class TimerEngine
{
    private readonly IClock _clock;

    private DateTimeOffset? _startedAt;
    private TimeSpan _total;
    private TimeSpan _accumulatedElapsed;
    private DateTimeOffset? _pausedAt;

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
                TimerState.Running => _accumulatedElapsed + (_clock.UtcNow - _startedAt!.Value),
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
        _pausedAt = null;
        _startedAt = _clock.UtcNow;
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
        _pausedAt = null;

        var now = _clock.UtcNow;
        if (endTime <= now)
        {
            _accumulatedElapsed = total;
            _startedAt = null;
            State = TimerState.Completed;
            return;
        }

        var remaining = endTime - now;
        if (remaining > total) remaining = total;
        _accumulatedElapsed = total - remaining;
        _startedAt = now;
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
        _pausedAt = _clock.UtcNow;
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

        _accumulatedElapsed += _clock.UtcNow - _startedAt!.Value;
        _pausedAt = _clock.UtcNow;
        _startedAt = null;
        State = TimerState.Paused;
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;

        _startedAt = _clock.UtcNow;
        _pausedAt = null;
        State = TimerState.Running;
    }

    public void Stop()
    {
        _startedAt = null;
        _pausedAt = null;
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
            State = TimerState.Completed;
        }
    }
}
