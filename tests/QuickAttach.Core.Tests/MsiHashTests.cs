using QuickAttach.Core.Update;

namespace QuickAttach.Core.Tests;

public class MsiHashTests
{
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Theory]
    [InlineData(AbcSha256, true)]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", true)]
    [InlineData(" " + AbcSha256 + " ", true)]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public void IsAllowed_ComparesPinnedHash(string? expected, bool allowed)
    {
        Assert.Equal(allowed, MsiHash.IsAllowed(expected, AbcSha256));
    }

    [Fact]
    public void Compute_ReturnsLowercaseSha256()
    {
        using var dir = new TempDir();
        var path = dir.Combine("file.msi");
        File.WriteAllText(path, "abc");

        Assert.Equal(AbcSha256, MsiHash.Compute(path));
    }
}
