using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeBarX.Core;

public sealed class JsonTimerStore : ITimerStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Persist the state as a string so reordering the TimerState enum can't
        // silently reinterpret an old file (e.g. Running becoming Paused).
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public JsonTimerStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "TimeBarX", "state.json");
    }

    public TimerSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            using var stream = File.OpenRead(_path);
            var snapshot = JsonSerializer.Deserialize<TimerSnapshot>(stream, Options);
            return IsUsable(snapshot) ? snapshot : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Rejects structurally invalid snapshots (corrupt-but-valid-JSON, hand-edited,
    /// or from a reordered enum) so rehydration can't throw at startup. Both
    /// TimerEngine.StartAt and RestorePaused require a positive Total; an undefined
    /// State or an out-of-range ElapsedAtPause would rehydrate into an unreachable
    /// engine state. A rejected snapshot is treated as "no saved timer".
    /// </summary>
    private static bool IsUsable(TimerSnapshot? s)
    {
        if (s is null) return false;
        if (!Enum.IsDefined(s.State)) return false;
        if (s.Total <= TimeSpan.Zero) return false;
        if (s.ElapsedAtPause < TimeSpan.Zero || s.ElapsedAtPause > s.Total) return false;
        return true;
    }

    public void Save(TimerSnapshot snapshot)
    {
        try
        {
            AtomicFile.WriteJson(_path, snapshot, Options);
        }
        catch
        {
            // Best-effort persistence of transient timer state; a failed save just
            // means the timer won't survive a restart, matching Clear's semantics.
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch
        {
            // best-effort
        }
    }
}
