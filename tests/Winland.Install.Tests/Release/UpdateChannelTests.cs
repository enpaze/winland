namespace Winland.Install.Tests;

[TestClass]
public class UpdateChannelTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  \n")]
    [DataRow("stable")]
    [DataRow("STABLE\n")]
    [DataRow("\uFEFFstable\n")]
    public void AMissingOrStableFileUsesStable(string? text)
    {
        Assert.AreEqual(UpdateChannel.Stable, UpdateChannelFile.Read(text));
        Assert.AreEqual(UpdateChannel.Stable, UpdateChannelFile.Choose(null, text));
    }

    [TestMethod]
    public void ASavedDevChannelIsUsedWhenTheCommandDoesNotChoose()
    {
        Assert.AreEqual(UpdateChannel.Dev, UpdateChannelFile.Read("dev\n"));
        Assert.AreEqual(UpdateChannel.Dev, UpdateChannelFile.Choose(null, " DEV "));
    }

    [TestMethod]
    public void AnExplicitChannelWinsOverTheSavedOne()
    {
        Assert.AreEqual(UpdateChannel.Stable, UpdateChannelFile.Choose(UpdateChannel.Stable, "dev\n"));
        Assert.AreEqual(UpdateChannel.Dev, UpdateChannelFile.Choose(UpdateChannel.Dev, "stable\n"));
    }

    [TestMethod]
    public void TheSavedWordRoundTripsAndLivesUnderTheWinlandFolder()
    {
        Assert.AreEqual(UpdateChannel.Dev, UpdateChannelFile.Read(UpdateChannelFile.Text(UpdateChannel.Dev)));
        Assert.AreEqual(UpdateChannel.Stable, UpdateChannelFile.Read(UpdateChannelFile.Text(UpdateChannel.Stable)));
        Assert.AreEqual("dev\n", UpdateChannelFile.Text(UpdateChannel.Dev));
        Assert.AreEqual(
            Path.Combine(@"C:\Users\me", ".winland", "channel"),
            UpdateChannelFile.PathFor(@"C:\Users\me"));
    }

    [TestMethod]
    public void AnythingOtherThanTheTwoChannelsIsRefused()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => UpdateChannelFile.Read("nightly"));

        Assert.Contains("not stable or dev", error.Message, StringComparison.Ordinal);
    }
}
