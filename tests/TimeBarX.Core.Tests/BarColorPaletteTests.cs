using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class BarColorPaletteTests
{
    [Fact]
    public void Gradient_starts_green()
    {
        var c = BarColorPalette.Gradient(0.0);
        Assert.True(c.G > c.R && c.G > c.B, $"Expected green-dominant at progress 0, got {c}");
    }

    [Fact]
    public void Gradient_middle_is_yellow_orange()
    {
        var c = BarColorPalette.Gradient(0.5);
        Assert.True(c.R > 200, $"Expected high R at midpoint, got {c}");
        Assert.True(c.G > 100, $"Expected significant G at midpoint, got {c}");
    }

    [Fact]
    public void Gradient_ends_red()
    {
        var c = BarColorPalette.Gradient(1.0);
        Assert.True(c.R > c.G && c.R > c.B, $"Expected red-dominant at progress 1, got {c}");
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Gradient_clamps_out_of_range(double progress)
    {
        var c = BarColorPalette.Gradient(progress); // should not throw
        Assert.InRange(c.R, (byte)0, (byte)255);
    }

    [Fact]
    public void Solid_returns_palette_color()
    {
        Assert.NotEqual(BarColorPalette.Solid(BarColor.Red), BarColorPalette.Solid(BarColor.Green));
    }

    [Fact]
    public void ForProgress_uses_solid_when_gradient_off()
    {
        var settings = AppSettings.Default with { Color = BarColor.Red, GradientMode = false };
        Assert.Equal(BarColorPalette.Solid(BarColor.Red), BarColorPalette.ForProgress(settings, 0.3));
    }

    [Fact]
    public void ForProgress_uses_gradient_when_on()
    {
        var settings = AppSettings.Default with { GradientMode = true };
        Assert.Equal(BarColorPalette.Gradient(0.7), BarColorPalette.ForProgress(settings, 0.7));
    }
}
