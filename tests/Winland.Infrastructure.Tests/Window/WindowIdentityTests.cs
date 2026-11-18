namespace Winland.Infrastructure.Tests;
[TestClass]
public class WindowIdentityTests
{
    [TestMethod]
    public void ProcessAndClassMakeTheSameWindowAndTheTitleTheSameClient()
    {
        var client = new RecoveredClient(new WindowId(5), WorkspaceId.Initial, MonitorId.Primary, false, "Notepad", "a.txt", 42);

        Assert.AreEqual(WindowIdentity.Recognition.SameClient, WindowIdentity.Recognize(client, 42, "Notepad", "a.txt"));
        Assert.AreEqual(WindowIdentity.Recognition.SameWindow, WindowIdentity.Recognize(client, 42, "Notepad", "b.txt"));
        Assert.AreEqual(WindowIdentity.Recognition.Other, WindowIdentity.Recognize(client, 43, "Notepad", "a.txt"));
        Assert.AreEqual(WindowIdentity.Recognition.Other, WindowIdentity.Recognize(client, 42, "Edit", "a.txt"));
    }

    [TestMethod]
    public void AHiddenClientIsTheSameClientWhateverItsTitle()
    {
        var client = new RecoveredClient(new WindowId(5), WorkspaceId.Initial, MonitorId.Primary, true, "Chrome_WidgetWin_1", "Inbox (3)", 42);

        Assert.AreEqual(WindowIdentity.Recognition.SameClient, WindowIdentity.Recognize(client, 42, "Chrome_WidgetWin_1", "Inbox (4)"));
        Assert.AreEqual(WindowIdentity.Recognition.Other, WindowIdentity.Recognize(client, 43, "Chrome_WidgetWin_1", "Inbox (3)"));
    }

    [DesktopFact]
    public void AClosedClientIsLeftOutAndTheOthersAreStillRecorded()
    {
        using var probe = new LimitProbe();
        probe.Open();
        try
        {
            var open = new WindowId(probe.Anchor.ToInt64());
            var closed = new WindowId(0x7007);
            var snapshot = new RecoverySnapshot(
            [
                new RecoveredClient(closed, new WorkspaceId("1"), MonitorId.Primary, true, "", "", 0),
                new RecoveredClient(open, new WorkspaceId("2"), MonitorId.Primary, true, "", "", 0),
            ]);

            var annotated = WindowIdentity.Annotate(snapshot);

            var client = Assert.ContainsSingle(annotated.Clients);
            Assert.AreEqual(open, client.Window);
            Assert.AreEqual(Environment.ProcessId, client.ProcessId);
            Assert.AreEqual("WinlandLimitProbe", client.Class);
        }
        finally
        {
            probe.Close();
        }
    }
}
