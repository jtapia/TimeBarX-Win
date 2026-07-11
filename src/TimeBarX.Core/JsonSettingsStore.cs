using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeBarX.Core;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public JsonSettingsStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "TimeBarX", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return AppSettings.Default;
            using var stream = File.OpenRead(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(stream, Options);
            return loaded?.Sanitize() ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        AtomicFile.WriteJson(_path, settings, Options);
    }
}
