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
        // Use a fully-specified record so equality isn't sensitive to null lists
        // being normalized on load (see Sanitize).
        var settings = AppSettings.Default with
        {
            Color = BarColor.Purple,
            Height = BarHeight.Thick,
            Opacity = 0.6,
            GradientMode = true,
            PlayCompletionSound = true,
            DefaultDuration = TimeSpan.FromMinutes(50),
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings.Color, loaded.Color);
        Assert.Equal(settings.Height, loaded.Height);
        Assert.Equal(settings.Opacity, loaded.Opacity);
        Assert.Equal(settings.GradientMode, loaded.GradientMode);
        Assert.Equal(settings.PlayCompletionSound, loaded.PlayCompletionSound);
        Assert.Equal(settings.DefaultDuration, loaded.DefaultDuration);
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

    [Fact]
    public void ShowCompletionToast_defaults_on()
    {
        Assert.True(AppSettings.Default.ShowCompletionToast);
    }

    [Fact]
    public void ShowCompletionToast_round_trips_when_disabled()
    {
        var store = new JsonSettingsStore(_path);
        store.Save(AppSettings.Default with { ShowCompletionToast = false });

        Assert.False(store.Load().ShowCompletionToast);
    }

    [Fact]
    public void Legacy_settings_without_toast_field_default_on()
    {
        // A settings.json written before ShowCompletionToast existed must not
        // silently disable toasts — the missing field takes the record default.
        File.WriteAllText(_path,
            "{\"Color\":\"Blue\",\"Height\":\"Normal\",\"Opacity\":1.0,\"GradientMode\":false," +
            "\"PlayCompletionSound\":false,\"DefaultDuration\":\"00:25:00\",\"Position\":\"Top\"}");
        var store = new JsonSettingsStore(_path);

        Assert.True(store.Load().ShowCompletionToast);
    }
}
