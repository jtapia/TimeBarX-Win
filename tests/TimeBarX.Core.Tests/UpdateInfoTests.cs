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
    public void IsNewer_compares_versions(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateInfo.IsNewer(latest, current));
    }
}
