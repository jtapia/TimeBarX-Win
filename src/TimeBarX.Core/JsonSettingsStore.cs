using System.Text.Json;

namespace TimeBarX.Core;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Tolerant so an unknown enum value (a setting written by a newer version,
        // or a hand-edit typo) doesn't abort the whole load and let the next Save
        // wipe every unrelated user setting. See TolerantEnumConverter.
        Converters = { new TolerantEnumConverter() },
    };

    private readonly string _path;

    public JsonSettingsStore(string? path = null)
    {
        _path = path ?? DefaultPath();
        AtomicFile.SweepOrphanedTemps(_path);
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
            // Share Delete so a concurrent atomic-rename save (AtomicFile replaces
            // the target via File.Move) can't fail because this read holds the
            // file open. Read too, in case another reader is active.
            using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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
