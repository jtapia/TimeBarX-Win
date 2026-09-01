using System;
using System.IO;
using System.Text.Json;
using TimeBarX.Core;

namespace TimeBarX.App.Store;

/// <summary>
/// Time-limited Pro trial backed by a stamp file next to the other app state in
/// %APPDATA%\TimeBarX\. On first launch it records the start instant; Pro is
/// granted while the <see cref="TrialWindow"/> is active and revoked once it
/// expires. Composed as a third source alongside the Store and license-key
/// channels so an active trial unlocks Pro everywhere.
///
/// <para>
/// The start instant lives in a plain <c>trial.json</c>. Deleting it restarts
/// the trial — an accepted reset for a $5 app (matches the refund-abuse stance
/// in PRO_PLAN §4), not a DRM boundary. The clock is injectable for tests.
/// </para>
///
/// <para>
/// Expiry is driven by the passage of time, not an external event, so the state
/// is evaluated at construction and re-evaluated on <see cref="Refresh"/> (call
/// it at launch and whenever the user opens a Pro surface). <see cref="Changed"/>
/// fires only on the active→expired transition.
/// </para>
/// </summary>
public sealed class TrialEntitlements : IEntitlements
{
    private readonly string _path;
    private readonly Func<DateTimeOffset> _now;
    private readonly TrialWindow _window;
    private bool _isActive;

    public TrialEntitlements(string? path = null, Func<DateTimeOffset>? now = null)
    {
        _path = path ?? DefaultPath();
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _window = LoadOrStart();
        _isActive = _window.IsActive(_now());
    }

    public bool IsPro => _isActive;

    public event Action? Changed;

    /// <summary>The instant the trial expires (exclusive).</summary>
    public DateTimeOffset ExpiresUtc => _window.ExpiresUtc;

    /// <summary>Default path: %APPDATA%\TimeBarX\trial.json</summary>
    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "TimeBarX", "trial.json");
    }

    /// <summary>
    /// Re-evaluate the window against the current clock. Fires <see cref="Changed"/>
    /// only if the active state flipped (active→expired). Cheap; safe to call on
    /// every launch and every Pro-surface open.
    /// </summary>
    public void Refresh()
    {
        var active = _window.IsActive(_now());
        if (active == _isActive) return;
        _isActive = active;
        Changed?.Invoke();
    }

    private TrialWindow LoadOrStart()
    {
        // Existing stamp: honor it.
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var state = JsonSerializer.Deserialize<TrialState>(json);
                if (state is { StartedUtc: { } started })
                {
                    return TrialWindow.Starting(started);
                }
            }
        }
        catch
        {
            // Corrupt/unreadable stamp: fall through and re-stamp so a mangled
            // file doesn't permanently deny the trial.
        }

        // First run (or unrecoverable stamp): start the trial now and persist.
        var window = TrialWindow.Starting(_now());
        Persist(window.StartedUtc);
        return window;
    }

    private void Persist(DateTimeOffset startedUtc)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new TrialState { StartedUtc = startedUtc });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Disk full / permission denied: the trial still runs in-memory for
            // this session; it just won't survive a restart. Better than
            // stranding the user with no trial at all.
        }
    }

    private sealed class TrialState
    {
        public DateTimeOffset? StartedUtc { get; set; }
    }
}
