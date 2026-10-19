using QuickAttach.Core.Update;

namespace QuickAttach.Core.Tests;

public class VersionPolicyTests
{
    [Theory]
    [InlineData("1.1.9", "1.2.0", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.2.0", "1.2", false)]
    [InlineData("1.2", "1.2.0", false)]
    [InlineData("1.3.0", "1.2.0", false)]
    [InlineData("1.2.0.0", "1.2.1", true)]
    [InlineData("0.9.0", "v1.0.0", true)]
    [InlineData("1.0.0", null, false)]
    [InlineData("1.0.0", "", false)]
    [InlineData("1.0.0", "latest", false)]
    public void IsBlocked_ComparesInstalledWithMinVersion(string installed, string? minVersion, bool expected)
    {
        Assert.Equal(expected, VersionPolicy.IsBlocked(Version.Parse(installed), minVersion));
    }

    [Theory]
    [InlineData("0.1.0", "0.2.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0.0", "0.1.0", false)]
    [InlineData("0.2.0", "0.1.9", false)]
    [InlineData("0.1.0", null, false)]
    [InlineData("0.1.0", "garbage", false)]
    [InlineData("0.1.0", "0.1.0.57", true)]
    [InlineData("0.1.0.57", "0.1.0.58", true)]
    [InlineData("0.1.0.57", "0.1.1", true)]
    [InlineData("0.1.1", "0.1.0.57", false)]
    public void IsUpdateAvailable_ComparesInstalledWithMsiVersion(string installed, string? msiVersion, bool expected)
    {
        Assert.Equal(expected, VersionPolicy.IsUpdateAvailable(Version.Parse(installed), msiVersion));
    }

    [Fact]
    public void TryParse_FillsMissingPartsWithZero()
    {
        Assert.Equal(new Version(1, 2, 0, 0), VersionPolicy.TryParse("1.2"));
        Assert.Null(VersionPolicy.TryParse("1"));
    }
}
