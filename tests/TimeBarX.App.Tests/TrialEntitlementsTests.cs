using System;
using System.IO;
using TimeBarX.App.Store;
using Xunit;

namespace TimeBarX.App.Tests;

/// <summary>
/// File-behavior tests for the App-side trial wrapper. The clock arithmetic
/// itself is covered by TimeBarX.Core.Tests/TrialWindowTests; here we exercise
/// the stamp/read/persist paths that only exist in the App layer, using an
/// injected temp path and a fixed clock.
/// </summary>
public sealed class TrialEntitlementsTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public TrialEntitlementsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tbx-trial-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "trial.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    private static Func<DateTimeOffset> Clock(DateTimeOffset t) => () => t;

    [Fact]
    public void FirstRun_StampsFile_AndGrantsPro()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var trial = new TrialEntitlements(_path, Clock(now));

        Assert.True(trial.IsPro);
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void FirstRun_ThenReopen_HonorsSameStart_StillActive()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _ = new TrialEntitlements(_path, Clock(start));

        // Reopen 10 days later: still inside the 14-day window, and it must use
        // the persisted start, not re-stamp to "now".
        var later = start + TimeSpan.FromDays(10);
        var reopened = new TrialEntitlements(_path, Clock(later));

        Assert.True(reopened.IsPro);
    }

    [Fact]
    public void ExpiredStamp_IsNotPro()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _ = new TrialEntitlements(_path, Clock(start));

        var wayLater = start + TimeSpan.FromDays(15); // past the 14-day window
        var expired = new TrialEntitlements(_path, Clock(wayLater));

        Assert.False(expired.IsPro);
        Assert.True(expired.HasExpired);
    }

    [Fact]
    public void CorruptFile_ReStampsAndGrantsPro()
    {
        File.WriteAllText(_path, "{ this is not valid json");
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var trial = new TrialEntitlements(_path, Clock(now));

        // A mangled stamp must not permanently deny the trial: it re-stamps.
        Assert.True(trial.IsPro);
    }

    [Fact]
    public void Refresh_FiresChanged_OnActiveToExpiredTransition()
    {
        // Start active, then advance the (mutable) clock past expiry and Refresh.
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var now = start;
        var trial = new TrialEntitlements(_path, () => now);
        Assert.True(trial.IsPro);

        var fired = 0;
        trial.Changed += () => fired++;

        now = start + TimeSpan.FromDays(15);
        trial.Refresh();

        Assert.False(trial.IsPro);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Refresh_NoTransition_DoesNotFireChanged()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var now = start;
        var trial = new TrialEntitlements(_path, () => now);

        var fired = 0;
        trial.Changed += () => fired++;

        now = start + TimeSpan.FromDays(1); // still active
        trial.Refresh();

        Assert.Equal(0, fired);
    }

    [Fact]
    public void MarkExpiryPromptShown_Persists_AcrossReopen()
    {
        // Seed a stamp that starts in the past, then reopen past its expiry —
        // the ctor only stamps "now" on first run, so we must create the start
        // first and time-travel on reopen.
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _ = new TrialEntitlements(_path, Clock(start));

        var expired = start + TimeSpan.FromDays(15);
        var first = new TrialEntitlements(_path, Clock(expired));
        Assert.False(first.ExpiryPromptShown);
        first.MarkExpiryPromptShown();
        Assert.True(first.ExpiryPromptShown);

        // A fresh instance reading the same file sees the flag set.
        var reopened = new TrialEntitlements(_path, Clock(expired));
        Assert.True(reopened.ExpiryPromptShown);
    }

    [Fact]
    public void MarkExpiryPromptShown_DoesNotResetStart()
    {
        // Persisting the shown-flag must keep the original StartedUtc so the
        // window doesn't silently restart. Seed the past start first, then
        // reopen past expiry.
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _ = new TrialEntitlements(_path, Clock(start));

        var expired = start + TimeSpan.FromDays(15);
        var trial = new TrialEntitlements(_path, Clock(expired));
        Assert.True(trial.HasExpired); // sanity: reading the old stamp, it's expired
        trial.MarkExpiryPromptShown();

        var reopened = new TrialEntitlements(_path, Clock(expired));
        Assert.True(reopened.HasExpired); // still expired, not restarted to "now"
    }

    [Fact]
    public void DeletingStamp_RestartsTrial()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _ = new TrialEntitlements(_path, Clock(start));

        // Simulate the accepted reset: wipe the stamp, reopen far in the future.
        File.Delete(_path);
        var muchLater = start + TimeSpan.FromDays(400);
        var restarted = new TrialEntitlements(_path, Clock(muchLater));

        Assert.True(restarted.IsPro); // fresh trial from muchLater
    }
}
