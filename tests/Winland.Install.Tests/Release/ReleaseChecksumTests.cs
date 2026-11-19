using System.Security.Cryptography;

namespace Winland.Install.Tests;

[TestClass]
public class ReleaseChecksumTests
{
    private static readonly byte[] Archive = "zip-bytes"u8.ToArray();

    [TestMethod]
    public void ThePublishedLineMatchesTheZip()
    {
        var sums = Hash(Archive).ToLowerInvariant() + "  Winland-win-x64.zip\n"
            + new string('b', 64) + "  version.json\n";

        Assert.IsTrue(ReleaseChecksum.Matches(Archive, sums));
    }

    [TestMethod]
    public void AnUppercaseHashAndACarriageReturnStillMatch()
    {
        Assert.IsTrue(ReleaseChecksum.Matches(Archive, Hash(Archive) + "  Winland-win-x64.zip\r\n"));
    }

    [TestMethod]
    [DataRow("  ")]
    [DataRow(" ")]
    [DataRow(" *")]
    [DataRow("  *")]
    public void ASha256SumSeparatorIsAccepted(string separator)
    {
        Assert.IsTrue(ReleaseChecksum.Matches(Archive, Hash(Archive).ToLowerInvariant() + separator + "Winland-win-x64.zip\n"));
    }

    [TestMethod]
    public void ADifferentHashIsRefused()
    {
        Assert.IsFalse(ReleaseChecksum.Matches(Archive, new string('a', 64) + "  Winland-win-x64.zip\n"));
    }

    [TestMethod]
    public void ALineForAnotherFileIsRefused()
    {
        Assert.IsFalse(ReleaseChecksum.Matches(Archive, Hash(Archive) + "  version.json\n"));
        Assert.IsFalse(ReleaseChecksum.Matches(Archive, Hash(Archive) + "  Winland-win-x64.zip.extra\n"));
    }

    [TestMethod]
    public void TheSameVersionTextIsCurrent()
    {
        Assert.IsTrue(ReleaseVersion.IsCurrent("0.1.0", "0.1.0"));
        Assert.IsFalse(ReleaseVersion.IsCurrent("0.1.0", "0.1.1"));
        Assert.IsFalse(ReleaseVersion.IsCurrent("0.0.0-dev", "0.1.0"));
        Assert.IsFalse(ReleaseVersion.IsCurrent("0.1.0", "0.1.0 "));
    }

    [TestMethod]
    public void ADevBuildIsCurrentOnlyWhenTheCommitMatches()
    {
        Assert.IsTrue(ReleaseVersion.IsSameCommit("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));
        Assert.IsFalse(ReleaseVersion.IsSameCommit("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        Assert.IsFalse(ReleaseVersion.IsSameCommit("", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        Assert.IsFalse(ReleaseVersion.IsSameCommit("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ""));
        Assert.IsFalse(ReleaseVersion.IsSameCommit("  ", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
