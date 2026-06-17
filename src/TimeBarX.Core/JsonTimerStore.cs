using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeBarX.Core;

public sealed class JsonTimerStore : ITimerStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, snapshot, Options);
        }
        File.Move(tmp, _path, overwrite: true);
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
