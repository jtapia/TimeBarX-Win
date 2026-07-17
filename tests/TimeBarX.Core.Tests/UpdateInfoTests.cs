using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class UpdateInfoTests
{
    [Theory]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("1.0.0", "0.9.9", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.1", "0.1.0", false)]
    [InlineData("0.1.0", "0.1", false)]
    [InlineData("garbage", "0.1.0", false)]
    // v-prefixed GitHub tags must still compare numerically.
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("1.1.0", "v1.0.0", true)]
    // Per-segment pre-release suffixes read as their numeric prefix.
    [InlineData("1.0.5-beta", "1.0.1", true)]
    [InlineData("1.0.0-rc1", "1.0.0", false)]
    public void IsNewer_compares_versions(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateInfo.IsNewer(latest, current));
    }
}
