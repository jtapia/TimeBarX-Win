using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace TimeBarX.App;

/// <summary>
/// Plays the completion sequence on a single overlay: white flash, three opacity
/// pulses, then a 2s fade-out. Driven by a single DispatcherTimer so each frame
/// can interpolate against wall-clock time without animation-system overhead on
/// the thin progress bar.
/// </summary>
internal sealed class CompletionAnimator
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan PulseHalf = TimeSpan.FromMilliseconds(180);
    private const int PulseCount = 3;
    private const double PulseFloor = 0.4;
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(2000);

    private readonly Window _window;
    private readonly Rectangle _bar;
    private readonly IBrush _restingBrush;
    private readonly IBrush _flashBrush;
    private readonly DispatcherTimer _timer;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private bool _cancelled;

    public CompletionAnimator(Window window, Rectangle bar, IBrush restingBrush, IBrush flashBrush)
    {
        _window = window;
        _bar = bar;
        _restingBrush = restingBrush;
        _flashBrush = flashBrush;
        _timer = new DispatcherTimer { Interval = Frame };
        _timer.Tick += (_, _) => Step();
    }

    public void Run()
    {
        _timer.Start();
    }

    public void Cancel()
    {
        if (_cancelled) return;
        _cancelled = true;
        _timer.Stop();
        _window.Opacity = 1.0;
        _bar.Fill = _restingBrush;
    }

    private void Step()
    {
        if (_cancelled) return;

        var elapsed = DateTimeOffset.UtcNow - _startedAt;

        var totalPulse = TimeSpan.FromTicks(PulseHalf.Ticks * 2 * PulseCount);
        var phaseEndFlash = FlashDuration;
        var phaseEndPulse = phaseEndFlash + totalPulse;
        var phaseEndFade = phaseEndPulse + FadeDuration;

        if (elapsed < phaseEndFlash)
        {
            _bar.Fill = _flashBrush;
            _window.Opacity = 1.0;
            return;
        }

        if (elapsed < phaseEndPulse)
        {
            _bar.Fill = _restingBrush;
            var inPulse = elapsed - phaseEndFlash;
            var cyclePos = inPulse.Ticks % (PulseHalf.Ticks * 2);
            var t = cyclePos / (double)(PulseHalf.Ticks * 2); // 0..1 across one cycle
            // Triangle wave: 1 → floor → 1
            var v = t < 0.5 ? 1.0 - (1.0 - PulseFloor) * (t * 2) : PulseFloor + (1.0 - PulseFloor) * ((t - 0.5) * 2);
            _window.Opacity = v;
            return;
        }

        if (elapsed < phaseEndFade)
        {
            _bar.Fill = _restingBrush;
            var fadeT = (elapsed - phaseEndPulse).TotalMilliseconds / FadeDuration.TotalMilliseconds;
            if (fadeT < 0) fadeT = 0;
            if (fadeT > 1) fadeT = 1;
            _window.Opacity = 1.0 - fadeT;
            return;
        }

        _timer.Stop();
        _window.Opacity = 0.0;
    }
}
