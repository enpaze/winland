namespace Winland.Application.Tests;

[TestClass]
public class MonitorTests
{
    [TestMethod]
    public void AWindowOpensOnTheFallbackMonitorWhenEveryMonitorIsDisabled()
    {
        var core = new WindowManager(ConfigParser.Parse("""
            general:gaps_out = 0
            general:border_size = 0
            general:border_size = 0
            """));
        var display = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 2560, 1440, 1, enabled: false);
        core.Handle(new MonitorsChanged([display]));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), display.WorkArea));

        Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void EachMonitorOwnsItsOwnLayout()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));

        var first = core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        var second = core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        Assert.AreEqual(left.WorkArea, BoundsOf(first, 1));
        Assert.AreEqual(right.WorkArea, BoundsOf(second, 2));
        Assert.AreEqual(2, second.State.Clients.Count);
        Assert.DoesNotContain(placement => placement.Client == first.State.Clients[0].Id, second.Placements);
    }

    [TestMethod]
    public void AWindowThatOpensGoesToTheFocusedMonitorWhereverItsProgramPutIt()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focus-monitor", @"\\.\DISPLAY2"));

        var opened = core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea) { Bounds = new Rectangle(100, 100, 800, 600), Opened = true });

        Assert.AreEqual(right.WorkArea, BoundsOf(opened, 2));
    }

    [TestMethod]
    public void AFramelessWindowOpensWhereItsProgramPutIt()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("focus-monitor", @"\\.\DISPLAY2"));
        var flyout = new Rectangle(1480, 340, 440, 700);

        var opened = core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea)
        {
            Bounds = flyout,
            Opened = true,
            Foreground = true,
            Traits = WindowTraits.Normal with { Resizable = false, Frameless = true },
        });

        var client = Assert.ContainsSingle(client => client.Window == new WindowId(3), opened.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.IsFalse(client.Pinned);
        Assert.AreEqual(flyout, client.Bounds);
        Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
    }

    [TestMethod]
    public void AWindowWithAnOwnerOpensOnItsOwnersMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), right.WorkArea));
        core.Handle(new Dispatch("focus-monitor", @"\\.\DISPLAY1"));

        var opened = core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea) { Owner = new WindowId(1), Opened = true });

        Assert.IsTrue(BoundsOf(opened, 2).X >= right.WorkArea.X);
    }

    [TestMethod]
    public void TwoClientsOnOneMonitorStillSplit()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void HotplugReflowsTheMonitorThatChanged()
    {
        var core = new WindowManager();
        var display = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        core.Handle(new MonitorsChanged([display]));
        core.Handle(new WindowAppeared(new WindowId(1), display.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), display.WorkArea));

        var outcome = core.Handle(new MonitorsChanged([
            display with { Width = 2000, Height = 1000, WorkArea = new Rectangle(0, 0, 2000, 1000) },
        ]));

        Assert.AreEqual(new Rectangle(0, 0, 1000, 1000), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(1000, 0, 1000, 1000), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void UnpluggingAMonitorMovesItsClients()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new MonitorsChanged([left]));

        Assert.ContainsSingle(outcome.State.Monitors);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
        Assert.Contains(client => client.Window == new WindowId(2) && client.Workspace == new WorkspaceId("2"), outcome.State.Hidden);
        Assert.Contains(workspace => workspace.Id == new WorkspaceId("2") && workspace.Monitor == new MonitorId(0), outcome.State.Workspaces);
    }

    [TestMethod]
    public void MixedDpiPlacementsUseThePhysicalWorkAreaAndScaleGaps()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 10
            general:border_size = 0
            """)));
        var low = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var high = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 3840, 2160, 2);
        var mid = Display(@"\\.\DISPLAY3", "LG", 5760, 0, 1920, 1080, 1.5);
        core.Handle(new MonitorsChanged([low, high, mid]));

        var first = core.Handle(new WindowAppeared(new WindowId(1), low.WorkArea));
        var second = core.Handle(new WindowAppeared(new WindowId(2), high.WorkArea));
        var third = core.Handle(new WindowAppeared(new WindowId(3), mid.WorkArea));

        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(first, 1));
        Assert.AreEqual(new Rectangle(1940, 20, 3800, 2120), BoundsOf(second, 2));
        Assert.AreEqual(new Rectangle(5775, 15, 1890, 1050), BoundsOf(third, 3));
    }
}
