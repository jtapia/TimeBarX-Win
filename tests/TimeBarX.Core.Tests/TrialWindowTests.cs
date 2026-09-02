using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class TrialWindowTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TrialWindow Default() => TrialWindow.Starting(Start);

    // The full window length, referenced so boundary tests track DefaultLength
    // rather than hardcoding a day count that changes when the length changes.
    private static readonly TimeSpan Len = TrialWindow.DefaultLength;

    [Fact]
    public void DefaultLength_Is7Days()
    {
        Assert.Equal(TimeSpan.FromDays(7), TrialWindow.DefaultLength);
    }

    [Fact]
    public void IsActive_AtStart()
    {
        Assert.True(Default().IsActive(Start));
    }

    [Fact]
    public void IsActive_MidWindow()
    {
        Assert.True(Default().IsActive(Start + Len - TimeSpan.FromDays(1)));
    }

    [Fact]
    public void IsActive_JustBeforeExpiry()
    {
        Assert.True(Default().IsActive(Start + Len - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void NotActive_AtExpiryInstant_ExclusiveBoundary()
    {
        // The window is half-open [start, start+length): the expiry instant
        // itself is already expired.
        Assert.False(Default().IsActive(Start + Len));
    }

    [Fact]
    public void NotActive_AfterExpiry()
    {
        Assert.False(Default().IsActive(Start + Len + TimeSpan.FromDays(1)));
    }

    [Fact]
    public void FutureStart_TreatedAsActive_NotExpired()
    {
        // Clock was wrong at first run / rolled back before the stamp: never
        // burn the trial because of a bad clock.
        var now = Start - TimeSpan.FromDays(3);
        Assert.True(Default().IsActive(now));
    }

    [Fact]
    public void ZeroLength_NeverActive()
    {
        var w = new TrialWindow(Start, TimeSpan.Zero);
        Assert.False(w.IsActive(Start));
    }

    [Fact]
    public void ExpiresUtc_IsStartPlusLength()
    {
        Assert.Equal(Start + Len, Default().ExpiresUtc);
    }

    [Fact]
    public void Remaining_MidWindow_IsPositive()
    {
        // Halfway-ish through the window: a positive, sub-length remainder.
        var elapsed = TimeSpan.FromDays(1);
        Assert.Equal(Len - elapsed, Default().Remaining(Start + elapsed));
    }

    [Fact]
    public void Remaining_AfterExpiry_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, Default().Remaining(Start + Len + TimeSpan.FromDays(5)));
    }
}
