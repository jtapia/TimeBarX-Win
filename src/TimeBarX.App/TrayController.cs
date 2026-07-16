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
    private readonly IIdleProbe _idleProbe;
    private readonly SessionHistoryStore _history;
    private readonly DispatcherTimer _ticker;

    private string _currentPreset = DefaultPreset;
    private string? _currentLabel;
    private TimerState _lastState = TimerState.Idle;
    private AppSettings _settings;

    // Set at Start/StartCustom, cleared at Stop; used to stamp the session
    // record when the timer completes. Null when there's no active timer.
    private DateTimeOffset? _startedAtUtc;

    // Pomodoro cycle state. When _pomodoroPhase is non-null, the currently
    // running (or last-completed) timer is one Pomodoro phase; on completion
    // with AutoAdvance the next phase kicks off automatically. Reset by Stop
    // or by starting a non-Pomodoro timer.
    private PomodoroPhase? _pomodoroPhase;
    private int _pomodoroWorkCount;

    // If the current timer was started from a CustomPreset with per-preset
    // overrides, that preset is kept here so completion effects (sound,
    // alert message) can consult it. Cleared by Start / StartCustom without
    // a preset / Stop / Pomodoro transitions.
    private CustomPreset? _activePreset;

    // Last values pushed to the UI, so the 30 FPS refresh only raises
    // PropertyChanged when something actually changed (see RefreshFromEngine).
    private string? _lastTooltip;
    private string? _lastStatusLabel;
    private double _lastProgress = double.NaN;

    /// <summary>Raised once when the timer transitions into Completed.</summary>
    public event Action? Completed;

    /// <summary>Raised when settings change so views can re-render.</summary>
    public event Action? SettingsChanged;

    /// <summary>
    /// The raw, stored settings. Includes Pro values verbatim — use this when
    /// you need to know what the user has *configured*, not what's currently
    /// applied (e.g. Settings UI sync still needs the stored value to persist
    /// across entitlement flips).
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Settings with Pro-only fields clamped to free values when not entitled.
    /// Use this everywhere a setting drives the *rendered* behavior (overlay
    /// layout, color, policy cadence). Re-purchase / Restore makes this equal
    /// to <see cref="Settings"/> again without the user re-entering anything.
    /// </summary>
    public AppSettings EffectiveSettings => _settings.ClampForEntitlement(Entitlements.IsPro);

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

    /// <summary>
    /// Single source of truth for Pro state. Consumed to clamp
    /// <see cref="Settings"/>, gate Pro-only UI, and drive lock-chip
    /// re-renders on transitions. Free until a platform impl replaces the
    /// default in <c>App.axaml.cs</c>.
    /// </summary>
    public IEntitlements Entitlements { get; }

    public TrayController()
        : this(new JsonTimerStore(), new JsonSettingsStore(), new FreeEntitlements(), new NullIdleProbe())
    {
    }

    public TrayController(ITimerStore store, ISettingsStore settingsStore)
        : this(store, settingsStore, new FreeEntitlements(), new NullIdleProbe())
    {
    }

    public TrayController(ITimerStore store, ISettingsStore settingsStore, IEntitlements entitlements)
        : this(store, settingsStore, entitlements, new NullIdleProbe())
    {
    }

    public TrayController(ITimerStore store, ISettingsStore settingsStore, IEntitlements entitlements, IIdleProbe idleProbe)
    {
        _store = store;
        _settingsStore = settingsStore;
        _idleProbe = idleProbe;
        _history = new SessionHistoryStore();
        _settings = _settingsStore.Load();
        Entitlements = entitlements;
        // Entitlement transitions (purchase / refund / restore) change what
        // EffectiveSettings produces from the same stored values, so reuse the
        // SettingsChanged event for live re-render across the overlays / policy
        // / Settings window. The Store impl marshals onto the UI thread itself.
        Entitlements.Changed += () =>
        {
            SettingsChanged?.Invoke();
            Raise(nameof(ShowBuyPro));
        };
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

    /// <summary>
    /// Whether the bar is actively counting (Running or Paused). Distinct from
    /// <see cref="IsBarVisible"/>, which stays true through Completed so the
    /// overlay can play its fade-out. The overlay policy gates its polling on
    /// this so it stops after completion instead of running the foreground/
    /// Z-order checks forever for an invisible, faded-out bar.
    /// </summary>
    public bool IsBarActive => _engine.State is TimerState.Running or TimerState.Paused;
    public bool CanStart => _engine.State is TimerState.Idle or TimerState.Completed;
    public bool CanPause => _engine.State == TimerState.Running;
    public bool CanResume => _engine.State == TimerState.Paused;
    public bool CanStop => _engine.State is TimerState.Running or TimerState.Paused;

    /// <summary>
    /// Whether the tray's "Buy Pro…" entry should be shown. Hidden once the user
    /// is already Pro (via Store IAP or a license key) so it doesn't nag forever.
    /// Re-raised on every entitlement transition (see <see cref="Entitlements.Changed"/>).
    /// </summary>
    public bool ShowBuyPro => !Entitlements.IsPro;

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
                if (_engine.State == TimerState.Running)
                {
                    // Reconstruct a plausible start so the eventual completion
                    // gets a StartedAt in the history log. Not perfectly
                    // accurate — Pause segments across restart aren't tracked —
                    // but Started = End − Total is close enough for a history
                    // entry.
                    _startedAtUtc = snapshot.EndTime.Value - snapshot.Total;
                    _ticker.Start();
                }
                else
                {
                    // End-time already passed while the app was closed: the timer
                    // finished off-screen. Treat it as idle rather than surfacing a
                    // frozen, full-width "Completed" bar with no completion effect —
                    // reopening the app should show no bar, not a stuck one.
                    _engine.Stop();
                    _store.Clear();
                }
                break;

            case TimerState.Paused:
                _engine.RestorePaused(snapshot.ElapsedAtPause, snapshot.Total);
                _startedAtUtc = DateTimeOffset.UtcNow - snapshot.ElapsedAtPause;
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
        _startedAtUtc = DateTimeOffset.UtcNow;
        _activePreset = null;
        _pomodoroPhase = null;
        _pomodoroWorkCount = 0;
        _engine.Start(duration);
        _ticker.Start();
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
        _startedAtUtc = DateTimeOffset.UtcNow;
        _activePreset = null;
        // Any manually-started custom timer breaks the Pomodoro cycle: the user
        // is intentionally running something else, so the auto-advance chain
        // shouldn't come back to bite them later.
        _pomodoroPhase = null;
        _pomodoroWorkCount = 0;
        _engine.Start(duration);
        _ticker.Start();
        Persist();
        RefreshFromEngine();
    }

    /// <summary>
    /// Start a timer from a user-defined <see cref="CustomPreset"/>, honoring
    /// any per-preset overrides (label, completion sound, alert message).
    /// </summary>
    public void StartFromPreset(CustomPreset preset)
    {
        if (!preset.IsValid) return;
        StartCustom(preset.Duration, FormatPreset(preset.Duration), preset.Label);
        _activePreset = preset;
    }

    /// <summary>Completion sound for the currently-active timer, honoring per-preset overrides.</summary>
    public CompletionSoundChoice EffectiveCompletionSoundForCurrent()
        => _activePreset?.CompletionSound ?? _settings.EffectiveCompletionSound;

    /// <summary>Alert message for the currently-active preset, if any.</summary>
    public string? ActiveAlertMessage => _activePreset?.AlertMessage;

    /// <summary>
    /// Starts (or restarts) the Pomodoro cycle: the current phase is set to
    /// Work and a timer of the configured Work length kicks off. On completion,
    /// with AutoAdvance enabled, subsequent phases follow automatically.
    /// </summary>
    public void StartPomodoro()
    {
        var pomo = _settings.Pomodoro ?? PomodoroSettings.Default;
        _engine.Stop();
        _pomodoroWorkCount = 0;
        StartPomodoroPhase(pomo, PomodoroPhase.Work);
    }

    private void StartPomodoroPhase(PomodoroSettings pomo, PomodoroPhase phase)
    {
        _pomodoroPhase = phase;
        // A Pomodoro phase is not a custom preset: clear any preset left over
        // from an earlier StartFromPreset (it isn't cleared on completion, only
        // on Stop) so phase completions don't play that preset's override sound
        // or report its stale alert message.
        _activePreset = null;
        var duration = pomo.DurationFor(phase);
        _currentPreset = FormatPreset(duration);
        _currentLabel = phase switch
        {
            PomodoroPhase.Work => $"Pomodoro · Work",
            PomodoroPhase.ShortBreak => $"Pomodoro · Break",
            PomodoroPhase.LongBreak => $"Pomodoro · Long break",
            _ => "Pomodoro",
        };
        _startedAtUtc = DateTimeOffset.UtcNow;
        _engine.Start(duration);
        _ticker.Start();
        Persist();
        RefreshFromEngine();
    }

    /// <summary>Current Pomodoro phase, or null when no cycle is active.</summary>
    public PomodoroPhase? CurrentPomodoroPhase => _pomodoroPhase;

    public void Pause()
    {
        if (!CanPause) return;
        _engine.Pause();
        // Nothing advances while paused, so stop the 30 FPS refresh loop rather
        // than burning CPU/battery on unchanging frames. Resume() restarts it.
        _ticker.Stop();
        Persist();
        RefreshFromEngine();
    }

    public void Resume()
    {
        if (!CanResume) return;
        _engine.Resume();
        _ticker.Start();
        Persist();
        RefreshFromEngine();
    }

    public void Stop()
    {
        _engine.Stop();
        _ticker.Stop();
        _store.Clear();
        _currentLabel = null;
        _startedAtUtc = null;
        _activePreset = null;
        _pomodoroPhase = null;
        _pomodoroWorkCount = 0;
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
        // Auto-pause on idle: check before the engine tick so the pause lands on
        // this frame rather than one frame late. Only fires while Running so a
        // manually-paused user doesn't get "auto-paused" over their own pause.
        if (_engine.State == TimerState.Running && _settings.AutoPauseOnIdleMinutes is { } minutes && minutes > 0)
        {
            var threshold = TimeSpan.FromMinutes(minutes);
            if (_idleProbe.GetIdleTime() >= threshold)
            {
                _engine.Pause();
                // Stop the refresh loop while auto-paused — this is exactly when
                // the machine should be quiescent. Resume() restarts it.
                _ticker.Stop();
                Persist();
            }
        }

        _engine.Tick();

        var current = _engine.State;
        var justCompleted = current == TimerState.Completed && _lastState != TimerState.Completed;
        var stateChanged = current != _lastState;
        _lastState = current;

        if (current == TimerState.Completed)
        {
            _ticker.Stop();
            _store.Clear();
        }

        TooltipText = BuildTooltip();
        StatusLabel = BuildStatusLabel();

        // The refresh loop ticks ~30x/sec; only raise PropertyChanged for values
        // that actually changed so views don't re-layout/re-brush every frame.
        if (TooltipText != _lastTooltip)
        {
            _lastTooltip = TooltipText;
            Raise(nameof(TooltipText));
        }

        if (StatusLabel != _lastStatusLabel)
        {
            _lastStatusLabel = StatusLabel;
            Raise(nameof(StatusLabel));
        }

        // On a state transition, force Progress to re-push this frame: a
        // Stop→Start could land on a Progress value equal to the stale cache and
        // skip the raise below, leaving the bar stuck.
        if (stateChanged) _lastProgress = double.NaN;

        var progress = Progress;
        if (progress != _lastProgress)
        {
            _lastProgress = progress;
            Raise(nameof(Progress));
        }

        // The command-availability flags only change on a state transition.
        if (stateChanged)
        {
            Raise(nameof(IsBarVisible));
            Raise(nameof(IsBarActive));
            Raise(nameof(CanStart));
            Raise(nameof(CanPause));
            Raise(nameof(CanResume));
            Raise(nameof(CanStop));
        }

        if (justCompleted)
        {
            RecordCompletion();
            var advanced = TryAdvancePomodoro();
            Completed?.Invoke();
            // If the cycle auto-advanced into the next phase, the completion
            // sound/animation still fires for the phase that just ended.
            _ = advanced;
        }
    }

    /// <summary>
    /// If a Pomodoro cycle is active and AutoAdvance is on, transition to the
    /// next phase and kick off a new timer. Returns true when an advance
    /// happened, so the caller can suppress overlay effects that shouldn't
    /// run twice (currently none — the completion event still fires once for
    /// the phase that ended).
    /// </summary>
    private bool TryAdvancePomodoro()
    {
        if (_pomodoroPhase is not { } phase) return false;
        var pomo = _settings.Pomodoro ?? PomodoroSettings.Default;
        if (!pomo.Enabled || !pomo.AutoAdvance) return false;

        if (phase == PomodoroPhase.Work) _pomodoroWorkCount++;
        var next = pomo.NextPhase(phase, _pomodoroWorkCount);
        // Delay the transition until the completion flash/pulse has played, so the
        // user actually sees the "phase ended" cue. Starting the next phase
        // immediately (next dispatcher iteration) raises IsBarVisible, which makes
        // the overlay cancel the just-started animation ~1 frame in. The delay
        // covers only the attention portion; the trailing fade is superseded by
        // the next timer, which is the desired effect.
        var advance = new DispatcherTimer { Interval = CompletionAnimator.AttentionDuration };
        advance.Tick += (_, _) =>
        {
            advance.Stop();
            // A Stop or manual Start during the delay cancels the advance.
            if (_pomodoroPhase != phase) return;
            StartPomodoroPhase(pomo, next);
        };
        advance.Start();
        return true;
    }

    private void RecordCompletion()
    {
        if (!_settings.RecordSessionHistory) return;
        if (_startedAtUtc is not { } startedAt) return;
        var record = new SessionRecord(
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow,
            Duration: _engine.Total,
            Preset: _currentPreset,
            Label: _currentLabel);
        _history.Append(record);
        _startedAtUtc = null;
    }

    /// <summary>Absolute path to the history log — surfaced in Settings so users can inspect it.</summary>
    public string HistoryPath => _history.Path;

    private string LabelSuffix => _currentLabel is null ? string.Empty : $" · {_currentLabel}";

    private string BuildTooltip()
    {
        var suffix = LabelSuffix;
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
        var suffix = LabelSuffix;
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
