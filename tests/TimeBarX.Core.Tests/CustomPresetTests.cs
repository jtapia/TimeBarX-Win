using System.Text.Json;
using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class CustomPresetTests
{
    [Fact]
    public void IsValid_RequiresName()
    {
        Assert.False(new CustomPreset("", TimeSpan.FromMinutes(5)).IsValid);
        Assert.False(new CustomPreset("   ", TimeSpan.FromMinutes(5)).IsValid);
    }

    [Fact]
    public void IsValid_RequiresPositiveDuration()
    {
        Assert.False(new CustomPreset("Standup", TimeSpan.Zero).IsValid);
        Assert.False(new CustomPreset("Standup", TimeSpan.FromSeconds(-1)).IsValid);
    }

    [Fact]
    public void IsValid_AcceptsWellFormed()
    {
        Assert.True(new CustomPreset("Standup", TimeSpan.FromMinutes(15)).IsValid);
        Assert.True(new CustomPreset("Lunch", TimeSpan.FromMinutes(45), "lunch break").IsValid);
    }

    [Fact]
    public void RoundTrips_Through_AppSettings_Json()
    {
        // The store uses System.Text.Json; verify the new IReadOnlyList<CustomPreset>
        // property serializes and rehydrates without losing data.
        var settings = AppSettings.Default with
        {
            CustomPresets = new[]
            {
                new CustomPreset("Standup", TimeSpan.FromMinutes(15)),
                new CustomPreset("Pomodoro", TimeSpan.FromMinutes(25), "focus"),
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.CustomPresets);
        Assert.Equal(2, loaded.CustomPresets!.Count);
        Assert.Equal("Standup", loaded.CustomPresets[0].Name);
        Assert.Equal(TimeSpan.FromMinutes(15), loaded.CustomPresets[0].Duration);
        Assert.Null(loaded.CustomPresets[0].Label);
        Assert.Equal("focus", loaded.CustomPresets[1].Label);
    }

    [Fact]
    public void Default_HasEmptyPresets()
    {
        Assert.NotNull(AppSettings.Default.CustomPresets);
        Assert.Empty(AppSettings.Default.CustomPresets!);
    }
}
