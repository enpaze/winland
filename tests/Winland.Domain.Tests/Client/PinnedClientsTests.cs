namespace Winland.Domain.Tests;

[TestClass]
public class PinnedClientsTests
{
    private static readonly WindowId Window = new(1);
    private static readonly WindowId Child = new(2);

    [TestMethod]
    public void APinnedClientIsPinnedUntilItIsUnpinned()
    {
        var pins = new PinnedClients();

        pins.Pin(Window);
        Assert.IsTrue(pins.IsPinned(Window));

        pins.Unpin(Window);
        Assert.IsFalse(pins.IsPinned(Window));
    }

    [TestMethod]
    [DataRow(true, true, false, true)]
    [DataRow(true, false, true, true)]
    [DataRow(true, false, false, false)]
    [DataRow(false, true, true, false)]
    public void AFloatingClientIsPinnedAtOpenWhenARulePinsItOrItsOwnerIsPinned(bool floating, bool ruled, bool ownerPinned, bool expected)
    {
        var pins = new PinnedClients();
        if (ownerPinned)
            pins.Pin(Window);

        pins.PinAtOpen(Child, floating, ruled, Window);

        Assert.AreEqual(expected, pins.IsPinned(Child));
    }

    [TestMethod]
    [DataRow(true, false, true, true)]
    [DataRow(true, true, true, false)]
    [DataRow(true, false, false, false)]
    [DataRow(false, false, true, false)]
    public void APinnedClientStaysVisibleWhenItIsNotMinimizedAndItsMonitorShowsAWorkspace(bool pinned, bool minimized, bool monitorShows, bool expected)
    {
        var pins = new PinnedClients();
        if (pinned)
            pins.Pin(Window);

        Assert.AreEqual(expected, pins.StaysVisible(Window, minimized, monitorShows));
    }
}
