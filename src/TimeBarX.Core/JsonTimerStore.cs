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
            return JsonSerializer.Deserialize<TimerSnapshot>(stream, Options);
        }
        catch
        {
            return null;
        }
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
