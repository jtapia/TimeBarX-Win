using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _path;

    public JsonSettingsStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"timebarx-settings-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Load_returns_defaults_when_missing()
    {
        var store = new JsonSettingsStore(_path);
        Assert.Equal(AppSettings.Default, store.Load());
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new JsonSettingsStore(_path);
        var settings = AppSettings.Default with { Color = BarColor.Green, GradientMode = true };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(BarColor.Green, loaded.Color);
        Assert.True(loaded.GradientMode);
        Assert.Equal(settings.Opacity, loaded.Opacity);
    }

    [Fact]
    public void Load_clamps_out_of_range_opacity()
    {
        File.WriteAllText(_path,
            "{\"Color\":\"Blue\",\"Height\":\"Normal\",\"Opacity\":42.0,\"GradientMode\":false," +
            "\"PlayCompletionSound\":false,\"DefaultDuration\":\"00:25:00\",\"Position\":\"Top\"}");
        var store = new JsonSettingsStore(_path);

        var loaded = store.Load();

        Assert.Equal(1.0, loaded.Opacity);
    }

    [Fact]
    public void Load_replaces_undefined_enum_with_default_without_discarding_other_fields()
    {
        // Height 999 is not a defined BarHeight; the rest of the file is valid.
        File.WriteAllText(_path,
            "{\"Color\":\"Green\",\"Height\":999,\"Opacity\":0.5,\"GradientMode\":true," +
            "\"PlayCompletionSound\":false,\"DefaultDuration\":\"00:25:00\",\"Position\":\"Bottom\"}");
        var store = new JsonSettingsStore(_path);

        var loaded = store.Load();

        Assert.Equal(AppSettings.Default.Height, loaded.Height); // reset
        Assert.Equal(BarColor.Green, loaded.Color);              // preserved
        Assert.True(loaded.GradientMode);                        // preserved
        Assert.Equal(BarPosition.Bottom, loaded.Position);       // preserved
    }

    [Fact]
    public void Load_returns_defaults_on_corrupt_file()
    {
        File.WriteAllText(_path, "{ not valid json");
        var store = new JsonSettingsStore(_path);

        Assert.Equal(AppSettings.Default, store.Load());
    }

    [Fact]
    public void Construction_sweeps_orphaned_temp_files()
    {
        // Simulate a temp file left by a crash between CreateNew and Move.
        var dir = Path.GetDirectoryName(_path)!;
        var orphan = $"{_path}.99999.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(orphan, "leftover");

        _ = new JsonSettingsStore(_path);

        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public void Load_replaces_unknown_enum_string_with_default_without_discarding_other_fields()
    {
        // "Orange" is not a defined BarColor name — it simulates a settings.json
        // written by a newer app version (or a hand-edit typo). The stock string
        // enum converter would throw here, collapsing the whole load to defaults
        // and letting the next Save wipe every other field. The tolerant
        // converter must reset only Color and preserve the rest.
        File.WriteAllText(_path,
            "{\"Color\":\"Orange\",\"Height\":\"Thick\",\"Opacity\":0.5,\"GradientMode\":true," +
            "\"PlayCompletionSound\":false,\"DefaultDuration\":\"00:25:00\",\"Position\":\"Bottom\"}");
        var store = new JsonSettingsStore(_path);

        var loaded = store.Load();

        Assert.Equal(AppSettings.Default.Color, loaded.Color); // reset from unknown name
        Assert.Equal(BarHeight.Thick, loaded.Height);          // preserved
        Assert.Equal(0.5, loaded.Opacity);                     // preserved
        Assert.True(loaded.GradientMode);                      // preserved
        Assert.Equal(BarPosition.Bottom, loaded.Position);     // preserved
    }

    [Fact]
    public void Load_survives_unknown_nested_enum_string_in_custom_preset()
    {
        // A per-preset CompletionSound value from a newer version must not nuke
        // the preset (or the whole file); the override falls back to null so the
        // app-level default applies.
        File.WriteAllText(_path,
            "{\"Color\":\"Blue\",\"Height\":\"Normal\",\"Opacity\":1.0,\"GradientMode\":false," +
            "\"PlayCompletionSound\":false,\"DefaultDuration\":\"00:25:00\",\"Position\":\"Top\"," +
            "\"CustomPresets\":[{\"Name\":\"Deep\",\"Duration\":\"00:50:00\",\"CompletionSound\":\"Sparkle\"}]}");
        var store = new JsonSettingsStore(_path);

        var loaded = store.Load();

        Assert.NotNull(loaded.CustomPresets);
        Assert.Single(loaded.CustomPresets!);
        Assert.Equal("Deep", loaded.CustomPresets![0].Name);
        Assert.Equal(TimeSpan.FromMinutes(50), loaded.CustomPresets[0].Duration);
    }
}
