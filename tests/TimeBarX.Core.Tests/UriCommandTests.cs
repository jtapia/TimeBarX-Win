using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class UriCommandTests
{
    [Fact]
    public void Parses_start_with_duration()
    {
        Assert.True(UriCommand.TryParse("timebarx://start?duration=25m", out var cmd));
        Assert.Equal(UriCommandKind.Start, cmd.Kind);
        Assert.Equal(TimeSpan.FromMinutes(25), cmd.Duration);
    }

    [Fact]
    public void Parses_start_with_colon_duration()
    {
        Assert.True(UriCommand.TryParse("timebarx://start?duration=1:30", out var cmd));
        Assert.Equal(TimeSpan.FromMinutes(90), cmd.Duration);
    }

    [Fact]
    public void Parses_start_with_label()
    {
        Assert.True(UriCommand.TryParse("timebarx://start?duration=25m&label=review%20PR", out var cmd));
        Assert.Equal("review PR", cmd.Label);
    }

    [Fact]
    public void Embedded_label_in_duration_is_preserved()
    {
        Assert.True(UriCommand.TryParse("timebarx://start?duration=25%20min%20review%20PR", out var cmd));
        Assert.Equal(TimeSpan.FromMinutes(25), cmd.Duration);
        Assert.Equal("review PR", cmd.Label);
    }

    [Theory]
    [InlineData("timebarx://pause", UriCommandKind.Pause)]
    [InlineData("timebarx://resume", UriCommandKind.Resume)]
    [InlineData("timebarx://stop", UriCommandKind.Stop)]
    public void Parses_simple_commands(string input, UriCommandKind kind)
    {
        Assert.True(UriCommand.TryParse(input, out var cmd));
        Assert.Equal(kind, cmd.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notauri")]
    [InlineData("https://example.com/start")]
    [InlineData("timebarx://unknown")]
    [InlineData("timebarx://start")] // no duration
    [InlineData("timebarx://start?duration=abc")]
    public void Rejects_invalid(string input)
    {
        Assert.False(UriCommand.TryParse(input, out _));
    }
}
