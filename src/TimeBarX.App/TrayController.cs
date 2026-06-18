using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using TimeBarX.Core;

namespace TimeBarX.App;

public sealed class TrayController : INotifyPropertyChanged
{
    private const string DefaultPreset = "25m";
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(25);

    // ~30 FPS — smooth enough for a thin progress bar without burning CPU.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(33);

    private readonly TimerEngine _engine = new();
    private readonly ITimerStore _store;
    private readonly DispatcherTimer _ticker;

    private string _currentPreset = DefaultPreset;
    private TimerState _lastState = TimerState.Idle;

    /// <summary>Raised once when the timer transitions into Completed.</summary>
    public event Action? Completed;

    /// <summary>If true, play a short sound on completion. Default off per PLAN.md.</summary>
    public bool PlayCompletionSound { get; set; }

    public TrayController()
        : this(new JsonTimerStore())
    {
    }

    public TrayController(ITimerStore store)
    {
        _store = store;
        _ticker = new DispatcherTimer { Interval = RefreshInterval };
        _ticker.Tick += (_, _) => RefreshFromEngine();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TooltipText { get; private set; } = "TimeBarX — idle";
    public string StatusLabel { get; private set; } = "No timer running";
    public double Progress => _engine.Progress;
    public bool IsBarVisible => _engine.State is TimerState.Running or TimerState.Paused or TimerState.Completed;
    public bool CanStart => _engine.State is TimerState.Idle or TimerState.Completed;
    public bool CanPause => _engine.State == TimerState.Running;
    public bool CanResume => _engine.State == TimerState.Paused;
    public bool CanStop => _engine.State is TimerState.Running or TimerState.Paused;

    /// <summary>
    /// Rehydrates a previously-saved timer from the store. Stale (already-completed)
    /// state is discarded silently.
    /// </summary>
    public void RestoreFromStore()
    {
        var snapshot = _store.Load();
        if (snapshot is null) return;

        if (!string.IsNullOrEmpty(snapshot.Preset)) _currentPreset = snapshot.Preset;

        switch (snapshot.State)
        {
            case TimerState.Running when snapshot.EndTime is not null:
                _engine.StartAt(snapshot.EndTime.Value, snapshot.Total);
                if (_engine.State == TimerState.Running) _ticker.Start();
                else _store.Clear();
                break;

            case TimerState.Paused:
                _engine.RestorePaused(snapshot.ElapsedAtPause, snapshot.Total);
                break;

            default:
                _store.Clear();
                break;
        }

        RefreshFromEngine();
    }

    /// <summary>
    /// Reconciles the engine clock — call after sleep/wake or whenever real time
    /// may have jumped beyond what the dispatcher tick has caught up to.
    /// </summary>
    public void ReconcileClock()
    {
        _engine.Reconcile();
        RefreshFromEngine();
    }

    public void Start()
    {
        if (!CanStart) return;
        _currentPreset = DefaultPreset;
        _engine.Start(DefaultDuration);
        _ticker.Start();
        _lastState = _engine.State;
        Persist();
        RefreshFromEngine();
    }

    /// <summary>
    /// Start a timer with a custom duration (e.g. from the quick input window).
    /// Replaces any active timer.
    /// </summary>
    public void StartCustom(TimeSpan duration, string preset)
    {
        if (duration <= TimeSpan.Zero) return;
        _engine.Stop();
        _currentPreset = string.IsNullOrEmpty(preset) ? DefaultPreset : preset;
        _engine.Start(duration);
        _ticker.Start();
        _lastState = _engine.State;
        Persist();
        RefreshFromEngine();
    }

    public void Pause()
    {
        if (!CanPause) return;
        _engine.Pause();
        Persist();
        RefreshFromEngine();
    }

    public void Resume()
    {
        if (!CanResume) return;
        _engine.Resume();
        _ticker.Start();
        _lastState = _engine.State;
        Persist();
        RefreshFromEngine();
    }

    public void Stop()
    {
        _engine.Stop();
        _ticker.Stop();
        _store.Clear();
        RefreshFromEngine();
    }

    private void Persist()
    {
        var snapshot = new TimerSnapshot(
            State: _engine.State,
            EndTime: _engine.EndTime,
            Total: _engine.Total,
            ElapsedAtPause: _engine.State == TimerState.Paused ? _engine.Elapsed : TimeSpan.Zero,
            Preset: _currentPreset
        );

        try
        {
            _store.Save(snapshot);
        }
        catch
        {
            // Persistence is best-effort; never let it crash the UI.
        }
    }

    private void RefreshFromEngine()
    {
        _engine.Tick();

        var current = _engine.State;
        var justCompleted = current == TimerState.Completed && _lastState != TimerState.Completed;
        _lastState = current;

        if (current == TimerState.Completed)
        {
            _ticker.Stop();
            _store.Clear();
        }

        TooltipText = BuildTooltip();
        StatusLabel = BuildStatusLabel();

        Raise(nameof(TooltipText));
        Raise(nameof(StatusLabel));
        Raise(nameof(Progress));
        Raise(nameof(IsBarVisible));
        Raise(nameof(CanStart));
        Raise(nameof(CanPause));
        Raise(nameof(CanResume));
        Raise(nameof(CanStop));

        if (justCompleted)
        {
            Completed?.Invoke();
        }
    }

    private string BuildTooltip() => _engine.State switch
    {
        TimerState.Idle => "TimeBarX — idle",
        TimerState.Running => $"TimeBarX — {Format(_engine.Remaining)} remaining",
        TimerState.Paused => $"TimeBarX — paused at {Format(_engine.Remaining)}",
        TimerState.Completed => "TimeBarX — done",
        _ => "TimeBarX",
    };

    private string BuildStatusLabel() => _engine.State switch
    {
        TimerState.Idle => "No timer running",
        TimerState.Running => $"{Format(_engine.Remaining)} remaining",
        TimerState.Paused => $"Paused — {Format(_engine.Remaining)} left",
        TimerState.Completed => "Timer complete",
        _ => "",
    };

    private static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes:D2}:{t.Seconds:D2}";
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
