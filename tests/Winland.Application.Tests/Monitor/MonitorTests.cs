namespace Winland.Application.Tests;

public class MonitorTests
{
    [Fact]
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

        Assert.Single(outcome.State.Clients);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
    }

    [Fact]
    public void EachMonitorOwnsItsOwnLayout()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));

        var first = core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        var second = core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        Assert.Equal(left.WorkArea, BoundsOf(first, 1));
        Assert.Equal(right.WorkArea, BoundsOf(second, 2));
        Assert.Equal(2, second.State.Clients.Count);
        Assert.DoesNotContain(second.Placements, placement => placement.Client == first.State.Clients[0].Id);
    }

    // A program such as Windows Terminal puts its new window where its last one was. The window still opens on the
    // focused Monitor, as a window that was open before Winland started stays on the one it is on.
    [Fact]
    public void AWindowThatOpensGoesToTheFocusedMonitorWhereverItsProgramPutIt()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focusmonitor", @"\\.\DISPLAY2"));

        var opened = core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea) { Bounds = new Rectangle(100, 100, 800, 600), Opened = true });

        Assert.Equal(right.WorkArea, BoundsOf(opened, 2));
    }

    // A tray icon's flyout, such as JetBrains Toolbox's, puts itself above the tray and hides when it loses focus. Moved
    // to the middle of the focused Monitor, it would hide as the pointer crossed a tile on its way there.
    [Fact]
    public void AFramelessWindowOpensWhereItsProgramPutIt()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("focusmonitor", @"\\.\DISPLAY2"));
        var flyout = new Rectangle(1480, 340, 440, 700);

        var opened = core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea)
        {
            Bounds = flyout,
            Opened = true,
            Foreground = true,
            Traits = WindowTraits.Normal with { Resizable = false, Frameless = true },
        });

        var client = Assert.Single(opened.State.Clients, client => client.Window == new WindowId(3));
        Assert.True(client.Floating);
        Assert.False(client.Pinned);
        Assert.Equal(flyout, client.Bounds);
        Assert.Equal(new WorkspaceId("1"), client.Workspace);
    }

    [Fact]
    public void AWindowWithAnOwnerOpensOnItsOwnersMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), right.WorkArea));
        core.Handle(new Dispatch("focusmonitor", @"\\.\DISPLAY1"));

        var opened = core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea) { Owner = new WindowId(1), Opened = true });

        Assert.True(BoundsOf(opened, 2).X >= right.WorkArea.X);
    }

    [Fact]
    public void TwoClientsOnOneMonitorStillSplit()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea));

        Assert.Equal(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.Equal(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [Fact]
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

        Assert.Equal(new Rectangle(0, 0, 1000, 1000), BoundsOf(outcome, 1));
        Assert.Equal(new Rectangle(1000, 0, 1000, 1000), BoundsOf(outcome, 2));
    }

    [Fact]
    public void UnpluggingAMonitorMovesItsClients()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new MonitorsChanged([left]));

        Assert.Single(outcome.State.Monitors);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
        Assert.Contains(outcome.State.Hidden, client => client.Window == new WindowId(2) && client.Workspace == new WorkspaceId("2"));
        Assert.Contains(outcome.State.Workspaces, workspace => workspace.Id == new WorkspaceId("2") && workspace.Monitor == new MonitorId(0));
    }

    [Fact]
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

        Assert.Equal(new Rectangle(10, 10, 1900, 1060), BoundsOf(first, 1));
        Assert.Equal(new Rectangle(1940, 20, 3800, 2120), BoundsOf(second, 2));
        Assert.Equal(new Rectangle(5775, 15, 1890, 1050), BoundsOf(third, 3));
    }

    [Theory]
    [InlineData(3840, 2, 1920)]
    [InlineData(1920, 1.5, 1280)]
    [InlineData(5, 2, 3)]
    [InlineData(-5, 2, -3)]
    [InlineData(100, 0, 100)]
    public void LogicalPixelsDividePhysicalPixelsByTheMonitorScale(int physical, double scale, int logical)
    {
        Assert.Equal(logical, MonitorPixels.Logical(physical, scale));
    }

    [Fact]
    public void APhysicalPositionIsTheLogicalPositionTimesTheMonitorScale()
    {
        Assert.Equal(3840, MonitorPixels.Physical(1920, 2));
        Assert.Equal(1920, MonitorPixels.Physical(1280, 1.5));
    }

}
