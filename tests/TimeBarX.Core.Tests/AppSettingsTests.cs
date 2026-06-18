using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _path;

    public AppSettingsTests()
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
        var settings = new AppSettings(BarColor.Purple, BarHeight.Thick, 0.6, GradientMode: true, PlayCompletionSound: true);

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Load_returns_defaults_on_corrupt_file()
    {
        File.WriteAllText(_path, "{ not valid");
        var store = new JsonSettingsStore(_path);

        Assert.Equal(AppSettings.Default, store.Load());
    }

    [Fact]
    public void WithOpacity_clamps_to_unit_range()
    {
        var settings = AppSettings.Default;
        Assert.Equal(0.0, settings.WithOpacity(-1).Opacity);
        Assert.Equal(1.0, settings.WithOpacity(5).Opacity);
        Assert.Equal(0.42, settings.WithOpacity(0.42).Opacity);
    }
}
