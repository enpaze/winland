namespace Winland.Infrastructure.Tests;

[TestClass]
public class TrayMessageTests
{
    [TestMethod]
    public void OnlyTheCallbackIsAllowedWhenTheBroadcastIsNotRegistered()
    {
        Assert.AreSequenceEqual([TrayMessages.Callback], TrayMessages.Allowed(0));
    }

    [TestMethod]
    public void TheCallbackAndTaskbarCreatedAreAllowedTogether()
    {
        const uint taskbarCreated = 0xC000;

        var allowed = TrayMessages.Allowed(taskbarCreated);

        Assert.Contains(TrayMessages.Callback, allowed);
        Assert.Contains(taskbarCreated, allowed);
        Assert.AreEqual(2, allowed.Length);
    }
}
