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
}
