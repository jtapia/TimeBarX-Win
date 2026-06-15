using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using TimeBarX.Core;

namespace TimeBarX.App;

public sealed class TrayController : INotifyPropertyChanged
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(25);

    private readonly TimerEngine _engine = new();
    private readonly DispatcherTimer _ticker;

    // ~30 FPS — smooth enough for a thin progress bar without burning CPU.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(33);

    public TrayController()
    {
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

    public void Start()
    {
        if (!CanStart) return;
        _engine.Start(DefaultDuration);
        _ticker.Start();
        RefreshFromEngine();
    }

    public void Pause()
    {
        if (!CanPause) return;
        _engine.Pause();
        RefreshFromEngine();
    }

    public void Resume()
    {
        if (!CanResume) return;
        _engine.Resume();
        RefreshFromEngine();
    }

    public void Stop()
    {
        _engine.Stop();
        _ticker.Stop();
        RefreshFromEngine();
    }

    private void RefreshFromEngine()
    {
        _engine.Tick();

        if (_engine.State == TimerState.Completed)
        {
            _ticker.Stop();
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
