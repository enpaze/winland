namespace Winland.Application.Tests;

[TestClass]
public class WorkspaceTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void SwitchingShowsOnlyTheTargetWorkspacesClients()
    {
        var core = Load("""
            bind = Win+1, workspace 1
            bind = Win+2, workspace 2
            """);
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);

        var outcome = Tap(core, "2");

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(MonitorId.Primary, outcome.State.ActiveMonitor);
        Assert.IsEmpty(outcome.Placements);
        Assert.IsEmpty(outcome.Show);
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], outcome.Hide);
        Assert.AreEqual(2, outcome.State.Hidden.Count);
        foreach (var client in outcome.State.Hidden)
{
    Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
}
        Assert.Contains(workspace => workspace.Id == new WorkspaceId("2") && workspace.Monitor == MonitorId.Primary, outcome.State.Workspaces);
    }

    [TestMethod]
    public void SwitchingBackRestoresThatWorkspacesLayout()
    {
        var core = Load("""
            bind = Win+1, workspace 1
            bind = Win+2, workspace 2
            """);
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        Tap(core, "2");
        var opened = Appear(core, 3);

        Assert.AreEqual(new WorkspaceId("2"), opened.State.ActiveWorkspace);
        Assert.AreEqual(Area, BoundsOf(opened, 3));
        Assert.Contains(client => client.Window == new WindowId(1), opened.State.Hidden);

        var back = Tap(core, "1");

        Assert.AreEqual(new WorkspaceId("1"), back.State.ActiveWorkspace);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(back, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(back, 2));
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], back.Show);
        Assert.AreSequenceEqual([new WindowId(3)], back.Hide);
        Assert.Contains(client => client.Window == new WindowId(3), back.State.Hidden);
    }

    [TestMethod]
    public void MoveToAWorkspaceSendsTheActiveClientAndFollows()
    {
        var core = Load("bind = Win+Shift+2, move 2");
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        core.Handle(new KeyPressed("Shift_L"));

        var outcome = Tap(core, "2");

        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(Area, BoundsOf(outcome, 2));
        Assert.AreEqual(new WorkspaceId("2"), Client(outcome, 2).Workspace);
        Assert.AreEqual(new WorkspaceId("1"), Client(outcome, 1).Workspace);
        Assert.AreSequenceEqual([new WindowId(2)], outcome.Show);
        Assert.AreSequenceEqual([new WindowId(1)], outcome.Hide);
        Assert.Contains(client => client.Window == new WindowId(1), outcome.State.Hidden);
    }

    [TestMethod]
    public void MoveToAWorkspaceAndStayKeepsTheCurrentWorkspace()
    {
        var core = Load("bind = Win+Alt+2, move 2 stay");
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        core.Handle(new KeyPressed("Alt_L"));

        var outcome = Tap(core, "2");

        Assert.AreEqual(WorkspaceId.Initial, outcome.State.ActiveWorkspace);
        Assert.AreEqual(Area, BoundsOf(outcome, 1));
        Assert.AreEqual(new WorkspaceId("2"), Client(outcome, 2).Workspace);
        Assert.AreSequenceEqual([new WindowId(2)], outcome.Hide);
        Assert.IsEmpty(outcome.Show);
        Assert.AreSequenceEqual([Client(outcome, 2)], outcome.State.Hidden);
    }

    [TestMethod]
    public void NewWindowJoinsTheActiveWorkspace()
    {
        var core = Load("""
            bind = Win+2, workspace 2
            """);
        Appear(core, 1);
        HoldSuper(core);
        Tap(core, "2");

        var outcome = Appear(core, 4);

        Assert.AreEqual(new WorkspaceId("2"), Client(outcome, 4).Workspace);
        Assert.AreEqual(Area, BoundsOf(outcome, 4));
        Assert.Contains(client => client.Window == new WindowId(1), outcome.State.Hidden);
    }

    [TestMethod]
    public void LastGoesBackToTheWorkspaceShownBefore()
    {
        var core = Load("""
            bind = Win+2, workspace 2
            bind = Win+3, workspace 3
            bind = Win+P, workspace last
            bind = Win+1, workspace 1
            """);
        Appear(core, 1);
        HoldSuper(core);

        var second = Tap(core, "2");
        Assert.AreEqual(new WorkspaceId("2"), second.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], second.Hide);

        var third = Tap(core, "3");
        Assert.AreEqual(new WorkspaceId("3"), third.State.ActiveWorkspace);

        var back = Tap(core, "P");
        Assert.AreEqual(new WorkspaceId("2"), back.State.ActiveWorkspace);

        var previous = Tap(core, "P");
        Assert.AreEqual(new WorkspaceId("3"), previous.State.ActiveWorkspace);

        var home = Tap(core, "1");
        Assert.AreEqual(new WorkspaceId("1"), home.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], home.Show);
    }

    [TestMethod]
    public void NextStepsOverEmptyNumbersToTheNextExistingWorkspace()
    {
        var core = Load("""
            bind = Win+Shift+3, move 3 stay
            bind = Win+M, workspace next
            bind = Win+P, workspace last
            """);
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        core.Handle(new KeyPressed("Shift_L"));
        Tap(core, "3");
        core.Handle(new KeyReleased("Shift_L"));

        var cycled = Tap(core, "M");
        Assert.AreEqual(new WorkspaceId("3"), cycled.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(2)], cycled.Show);

        var back = Tap(core, "P");
        Assert.AreEqual(new WorkspaceId("1"), back.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], back.Show);
    }

    [TestMethod]
    public void EmptyAddressOpensTheFirstEmptyWorkspace()
    {
        var core = Load("bind = Win+E, workspace empty");
        Appear(core, 1);
        HoldSuper(core);

        var outcome = Tap(core, "E");

        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.Contains(client => client.Window == new WindowId(1), outcome.State.Hidden);
    }

    [TestMethod]
    public void MinimizedClientStaysMinimizedAcrossASwitch()
    {
        var core = Load("""
            bind = Win+1, workspace 1
            bind = Win+2, workspace 2
            """);
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowMinimized(new WindowId(2)));
        HoldSuper(core);
        var away = Tap(core, "2");
        Assert.Contains(window => window == new WindowId(2), away.Hide);

        var back = Tap(core, "1");

        Assert.AreSequenceEqual([new WindowId(1)], back.Show);
        Assert.DoesNotContain(window => window == new WindowId(2), back.Show);
        Assert.AreSequenceEqual([new WindowId(2)], back.ShowMinimized);
        Assert.IsTrue(Client(back, 2).Minimized);
        Assert.IsEmpty(core.Handle(new WindowFocused(new WindowId(1))).Hide);
        Assert.AreEqual(Area, BoundsOf(back, 1));
        Assert.DoesNotContain(placement => placement.Client == Client(back, 2).Id, back.Placements);
    }

    [TestMethod]
    public void DispatchSwitchesTheActiveWorkspace()
    {
        var core = new WindowManager();
        Appear(core, 1);
        Appear(core, 2);

        var outcome = core.Handle(new Dispatch("workspace", "2"));

        Assert.IsFalse(outcome.Consumed);
        Assert.IsNull(outcome.Close);
        Assert.IsNull(outcome.Focus);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], outcome.Hide);
        Assert.IsEmpty(outcome.Show);
        foreach (var client in outcome.State.Hidden)
{
    Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
}
    }

    [TestMethod]
    public void MoveToAWorkspaceFollowsAndStayStays()
    {
        var core = new WindowManager();
        Appear(core, 1);
        Appear(core, 2);

        var moved = core.Handle(new Dispatch("move", "3 stay"));

        Assert.IsFalse(moved.Consumed);
        Assert.AreEqual(WorkspaceId.Initial, moved.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), Client(moved, 2).Workspace);
        Assert.AreSequenceEqual([new WindowId(2)], moved.Hide);
        Assert.AreEqual(Area, BoundsOf(moved, 1));

        var followed = core.Handle(new Dispatch("move", "3"));

        Assert.AreEqual(new WorkspaceId("3"), followed.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), Client(followed, 1).Workspace);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(followed, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(followed, 1));
        Assert.AreSequenceEqual([new WindowId(2), new WindowId(1)], followed.Show);
        Assert.IsEmpty(followed.Hide);
    }

    [TestMethod]
    public void FocusStaysInsideTheActiveWorkspace()
    {
        var core = Load("bind = Win+H, focus l");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new Dispatch("workspace", "2"));
        Appear(core, 3);
        Appear(core, 4);
        HoldSuper(core);

        var focused = Tap(core, "H");

        Assert.IsTrue(focused.Consumed);
        Assert.AreEqual(new WindowId(3), focused.Focus);
        Assert.AreEqual(new WorkspaceId("2"), focused.State.ActiveWorkspace);
        Assert.Contains(client => client.Window == new WindowId(1), focused.State.Hidden);
        Assert.Contains(client => client.Window == new WindowId(2), focused.State.Hidden);

        var stopped = core.Handle(new Dispatch("focus", "l"));

        Assert.IsNull(stopped.Focus);
        Assert.AreEqual(new WorkspaceId("2"), stopped.State.ActiveWorkspace);
        Assert.AreEqual(new WindowId(3), core.Handle(new Dispatch("close")).Close);

        var back = core.Handle(new Dispatch("workspace", "1"));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(back, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(back, 2));
        Assert.Contains(client => client.Window == new WindowId(3), back.State.Hidden);
        Assert.Contains(client => client.Window == new WindowId(4), back.State.Hidden);
    }

    [TestMethod]
    public void FloatAfterSwitchingLeavesTheOtherWorkspaceTiled()
    {
        var core = Load("bind = Win+Space, float");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new Dispatch("workspace", "2"));
        Appear(core, 3);
        HoldSuper(core);

        var floating = Tap(core, "Space");

        Assert.IsTrue(floating.Consumed);
        Assert.AreEqual(new WorkspaceId("2"), floating.State.ActiveWorkspace);
        Assert.IsTrue(Client(floating, 3).Floating);
        Assert.IsFalse(Client(floating, 1).Floating);
        Assert.IsFalse(Client(floating, 2).Floating);
        Assert.AreEqual(new Rectangle(640, 340, 640, 400), BoundsOf(floating, 3));

        var back = core.Handle(new Dispatch("workspace", "1"));

        Assert.AreEqual(new WorkspaceId("1"), back.State.ActiveWorkspace);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(back, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(back, 2));
        Assert.Contains(window => window == new WindowId(3), back.Hide);
        Assert.Contains(client => client.Window == new WindowId(3) && client.Floating, back.State.Hidden);
    }

    [TestMethod]
    public void InvalidWorkspaceAddressDoesNotSwitch()
    {
        var core = Load("""
            bind = Win+Z, workspace 0
            bind = Win+N, workspace name:
            """);
        Appear(core, 1);
        HoldSuper(core);

        var zero = Tap(core, "Z");
        Assert.AreEqual(WorkspaceId.Initial, zero.State.ActiveWorkspace);
        Assert.IsEmpty(zero.Hide);

        var blank = Tap(core, "N");
        Assert.AreEqual(WorkspaceId.Initial, blank.State.ActiveWorkspace);
        Assert.Contains(client => client.Window == new WindowId(1), blank.State.Clients);
    }

    [TestMethod]
    public void SwitchingToAnEmptyWorkspaceOnAnotherMonitorMovesTheCursorThere()
    {
        var core = TwoMonitors("");

        var switched = ShowTheRightsWorkspace(core);

        Assert.AreEqual(new WarpTo(new Point(2880, 540)), switched.Warp);
    }

    [TestMethod]
    public void SwitchingToAWorkspaceOnAnotherMonitorMovesTheCursorToItsFocusedClient()
    {
        var core = TwoMonitors("");
        core.Execute("focus-monitor RIGHT");
        core.Execute("workspace 2");
        core.Handle(new WindowAppeared(new WindowId(1), Right));
        core.Handle(new WindowAppeared(new WindowId(2), Right));
        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));
        core.Execute("focus-monitor LEFT");

        var switched = core.Execute("workspace last").Effect;

        Assert.AreEqual(new WarpTo(new Point(2400, 540)), switched.Warp);
    }

    [TestMethod]
    public void WarpCursorOffKeepsTheCursorWhenASwitchLandsOnAnotherMonitor()
    {
        var core = TwoMonitors("warp_cursor = false\n");

        var switched = ShowTheRightsWorkspace(core);

        Assert.IsNull(switched.Warp);
    }

    [TestMethod]
    public void SwitchingOnTheSameMonitorDoesNotMoveTheCursor()
    {
        var core = TwoMonitors("");

        var switched = core.Execute("workspace 3").Effect;

        Assert.IsNull(switched.Warp);
    }

    private static readonly Rectangle Right = new(1920, 0, 1920, 1080);

    private static Outcome ShowTheRightsWorkspace(WindowManager core)
    {
        core.Execute("focus-monitor RIGHT");
        core.Execute("workspace 2");
        core.Execute("focus-monitor LEFT");
        return core.Execute("workspace last").Effect;
    }

    private static WindowManager TwoMonitors(string config)
    {
        var core = Load(config);
        core.Handle(new MonitorsChanged(
        [
            new Monitor("LEFT", "Left", 0, 0, 1920, 1080, 1, true, Area),
            new Monitor("RIGHT", "Right", 1920, 0, 1920, 1080, 1, true, Right),
        ]));
        core.Execute("focus-monitor LEFT");
        return core;
    }

    private const string Gapless = "gaps_in = 0\ngaps_out = 0\nborder_size = 0\n";

    private static WindowManager Load(string text)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(Gapless + text)));
        return core;
    }

    private static void HoldSuper(WindowManager core) => core.Handle(new KeyPressed("Super_L"));
}
