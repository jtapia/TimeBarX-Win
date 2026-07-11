using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class DurationParserTests
{
    [Theory]
    [InlineData("25 min", 0, 25, 0)]
    [InlineData("25min", 0, 25, 0)]
    [InlineData("25m", 0, 25, 0)]
    [InlineData("25 minutes", 0, 25, 0)]
    [InlineData("1h", 1, 0, 0)]
    [InlineData("1 hour", 1, 0, 0)]
    [InlineData("2h 15m", 2, 15, 0)]
    [InlineData("2h15m", 2, 15, 0)]
    [InlineData("2h 15m 30s", 2, 15, 30)]
    [InlineData("90s", 0, 1, 30)]
    public void Parses_unit_form(string input, int h, int m, int s)
    {
        Assert.True(DurationParser.TryParse(input, out var parsed));
        Assert.Equal(new TimeSpan(h, m, s), parsed.Duration);
        Assert.Null(parsed.Label);
    }

    [Theory]
    [InlineData("1:30", 1, 30, 0)]
    [InlineData("0:45", 0, 45, 0)]
    [InlineData("1:23:45", 1, 23, 45)]
    public void Parses_colon_form(string input, int h, int m, int s)
    {
        Assert.True(DurationParser.TryParse(input, out var parsed));
        Assert.Equal(new TimeSpan(h, m, s), parsed.Duration);
    }

    [Theory]
    [InlineData("25 min review PR", 25, "review PR")]
    [InlineData("2h review PR", 120, "review PR")]
    [InlineData("1:30 focus block", 90, "focus block")]
    public void Captures_trailing_label(string input, int totalMinutes, string label)
    {
        Assert.True(DurationParser.TryParse(input, out var parsed));
        Assert.Equal(TimeSpan.FromMinutes(totalMinutes), parsed.Duration);
        Assert.Equal(label, parsed.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("0 min")]
    [InlineData("0:00")]
    [InlineData("abc 25")]
    public void Rejects_invalid(string input)
    {
        Assert.False(DurationParser.TryParse(input, out _));
    }

    [Fact]
    public void Preset_round_trips_for_unit_form()
    {
        DurationParser.TryParse("2h 15m", out var parsed);
        Assert.Equal("2h 15m", parsed.Preset);
    }

    [Fact]
    public void Preset_round_trips_for_colon_form()
    {
        DurationParser.TryParse("1:30", out var parsed);
        Assert.Equal("1:30", parsed.Preset);
    }

    [Theory]
    [InlineData("half hour", 30)]
    [InlineData("quarter hour", 15)]
    [InlineData("an hour", 60)]
    [InlineData("a minute", 1)]
    public void Parses_phrase_form(string input, int totalMinutes)
    {
        Assert.True(DurationParser.TryParse(input, out var parsed));
        Assert.Equal(TimeSpan.FromMinutes(totalMinutes), parsed.Duration);
    }

    [Fact]
    public void Phrase_form_captures_trailing_label()
    {
        Assert.True(DurationParser.TryParse("half hour standup", out var parsed));
        Assert.Equal(TimeSpan.FromMinutes(30), parsed.Duration);
        Assert.Equal("standup", parsed.Label);
    }

    [Theory]
    [InlineData("99999999999h")]
    [InlineData("600000000h")]
    [InlineData("3000000:00")]
    [InlineData("999999999m")]
    [InlineData("1000h")]
    public void Rejects_overflowing_input_without_throwing(string input)
    {
        Assert.False(DurationParser.TryParse(input, out _));
    }

    [Theory]
    [InlineData("1:99")]
    [InlineData("0:99")]
    [InlineData("1:23:99")]
    [InlineData("1:60")]
    public void Rejects_out_of_range_colon_components(string input)
    {
        Assert.False(DurationParser.TryParse(input, out _));
    }

    [Theory]
    [InlineData("half minute", 30)]
    [InlineData("quarter minute", 15)]
    public void Sub_minute_phrase_preset_round_trips(string input, int seconds)
    {
        Assert.True(DurationParser.TryParse(input, out var parsed));
        Assert.Equal(TimeSpan.FromSeconds(seconds), parsed.Duration);
        Assert.Equal($"{seconds}s", parsed.Preset);
        // The preset must itself parse back to the same duration.
        Assert.True(DurationParser.TryParse(parsed.Preset, out var round));
        Assert.Equal(parsed.Duration, round.Duration);
    }
}
