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
    private readonly ISettingsStore _settingsStore;
    private readonly DispatcherTimer _ticker;

    private string _currentPreset = DefaultPreset;
    private string? _currentLabel;
    private TimerState _lastState = TimerState.Idle;
    private AppSettings _settings;

    /// <summary>Raised once when the timer transitions into Completed.</summary>
    public event Action? Completed;

    /// <summary>Raised when settings change so views can re-render.</summary>
    public event Action? SettingsChanged;

    public AppSettings Settings => _settings;

    private UpdateInfo? _availableUpdate;
    public UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        set
        {
            if (_availableUpdate == value) return;
            _availableUpdate = value;
            Raise(nameof(AvailableUpdate));
        }
    }

    /// <summary>If true, play a short sound on completion. Default off per PLAN.md.</summary>
    public bool PlayCompletionSound => _settings.PlayCompletionSound;

    public TrayController()
        : this(new JsonTimerStore(), new JsonSettingsStore())
    {
    }

    public TrayController(ITimerStore store, ISettingsStore settingsStore)
    {
        _store = store;
        _settingsStore = settingsStore;
        _settings = _settingsStore.Load();
        _ticker = new DispatcherTimer { Interval = RefreshInterval };
        _ticker.Tick += (_, _) => RefreshFromEngine();
    }

    public void UpdateSettings(Func<AppSettings, AppSettings> mutator)
    {
        var next = mutator(_settings);
        if (next.Equals(_settings)) return;
        _settings = next;
        try { _settingsStore.Save(_settings); }
        catch { /* best-effort */ }
        SettingsChanged?.Invoke();
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
        _currentLabel = string.IsNullOrWhiteSpace(snapshot.Label) ? null : snapshot.Label;

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
        var duration = _settings.DefaultDuration > TimeSpan.Zero ? _settings.DefaultDuration : DefaultDuration;
        _currentPreset = FormatPreset(duration);
        _currentLabel = null;
        _engine.Start(duration);
        _ticker.Start();
        _lastState = _engine.State;
        Persist();
        RefreshFromEngine();
    }

    private static string FormatPreset(TimeSpan d)
    {
        if (d.TotalHours >= 1 && d.Minutes == 0 && d.Seconds == 0) return $"{(int)d.TotalHours}h";
        if (d.TotalMinutes >= 1 && d.Seconds == 0) return $"{(int)d.TotalMinutes}m";
        return d.ToString();
    }

    /// <summary>
    /// Start a timer with a custom duration (e.g. from the quick input window).
    /// Replaces any active timer. <paramref name="label"/> is shown in the tray UI.
    /// </summary>
    public void StartCustom(TimeSpan duration, string preset, string? label = null)
    {
        if (duration <= TimeSpan.Zero) return;
        _engine.Stop();
        _currentPreset = string.IsNullOrEmpty(preset) ? DefaultPreset : preset;
        _currentLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
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
        _currentLabel = null;
        RefreshFromEngine();
    }

    private void Persist()
    {
        var snapshot = new TimerSnapshot(
            State: _engine.State,
            EndTime: _engine.EndTime,
            Total: _engine.Total,
            ElapsedAtPause: _engine.State == TimerState.Paused ? _engine.Elapsed : TimeSpan.Zero,
            Preset: _currentPreset,
            Label: _currentLabel
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

    private string BuildTooltip()
    {
        var suffix = _currentLabel is null ? string.Empty : $" · {_currentLabel}";
        return _engine.State switch
        {
            TimerState.Idle => "TimeBarX — idle",
            TimerState.Running => $"TimeBarX — {Format(_engine.Remaining)} remaining{suffix}",
            TimerState.Paused => $"TimeBarX — paused at {Format(_engine.Remaining)}{suffix}",
            TimerState.Completed => _currentLabel is null ? "TimeBarX — done" : $"TimeBarX — {_currentLabel} done",
            _ => "TimeBarX",
        };
    }

    private string BuildStatusLabel()
    {
        var suffix = _currentLabel is null ? string.Empty : $" · {_currentLabel}";
        return _engine.State switch
        {
            TimerState.Idle => "No timer running",
            TimerState.Running => $"{Format(_engine.Remaining)} remaining{suffix}",
            TimerState.Paused => $"Paused — {Format(_engine.Remaining)} left{suffix}",
            TimerState.Completed => _currentLabel is null ? "Timer complete" : $"{_currentLabel} complete",
            _ => "",
        };
    }

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
