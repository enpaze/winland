namespace Winland.Application.Tests;

[TestClass]
public class WorkspaceTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void SwitchingShowsOnlyTheTargetWorkspacesClients()
    {
        var core = Load("""
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
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
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
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
    public void MoveToWorkspaceSendsTheActiveClientAndFollows()
    {
        var core = Load("bind = SUPER SHIFT, 2, movetoworkspace, 2");
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
    public void MoveToWorkspaceSilentKeepsTheCurrentWorkspace()
    {
        var core = Load("bind = SUPER ALT, 2, movetoworkspacesilent, 2");
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
            bind = SUPER, 2, workspace, 2
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
    public void RelativeAndPreviousAddressesFollowTheManual()
    {
        var core = Load("""
            bind = SUPER, O, workspace, +1
            bind = SUPER, I, workspace, -1
            bind = SUPER, P, workspace, previous
            bind = SUPER, 1, workspace, 1
            """);
        Appear(core, 1);
        HoldSuper(core);

        var next = Tap(core, "O");
        Assert.AreEqual(new WorkspaceId("2"), next.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], next.Hide);

        var third = Tap(core, "O");
        Assert.AreEqual(new WorkspaceId("3"), third.State.ActiveWorkspace);

        var back = Tap(core, "I");
        Assert.AreEqual(new WorkspaceId("2"), back.State.ActiveWorkspace);

        var previous = Tap(core, "P");
        Assert.AreEqual(new WorkspaceId("3"), previous.State.ActiveWorkspace);

        var home = Tap(core, "1");
        Assert.AreEqual(new WorkspaceId("1"), home.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], home.Show);
    }

    [TestMethod]
    public void NumericStepIncludesEmptyIdsAndMonitorStepUsesExistingWorkspaces()
    {
        var core = Load("""
            bind = SUPER SHIFT, 3, movetoworkspacesilent, 3
            bind = SUPER, R, workspace, r+1
            bind = SUPER, M, workspace, m+1
            bind = SUPER, P, workspace, previous
            """);
        Appear(core, 1);
        HoldSuper(core);
        core.Handle(new KeyPressed("Shift_L"));
        Tap(core, "3");
        core.Handle(new KeyReleased("Shift_L"));

        var stepped = Tap(core, "R");

        Assert.AreEqual(new WorkspaceId("2"), stepped.State.ActiveWorkspace);
        Assert.Contains(client => client.Window == new WindowId(1) && client.Workspace == new WorkspaceId("3"), stepped.State.Hidden);

        var home = Tap(core, "P");
        Assert.AreEqual(new WorkspaceId("1"), home.State.ActiveWorkspace);

        var cycled = Tap(core, "M");
        Assert.AreEqual(new WorkspaceId("3"), cycled.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], cycled.Show);
    }

    [TestMethod]
    public void EmptyAddressOpensTheFirstEmptyWorkspace()
    {
        var core = Load("bind = SUPER, E, workspace, empty");
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
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
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
    public void DispatchMoveToWorkspaceFollowsAndSilentStays()
    {
        var core = new WindowManager();
        Appear(core, 1);
        Appear(core, 2);

        var moved = core.Handle(new Dispatch("movetoworkspacesilent", "3"));

        Assert.IsFalse(moved.Consumed);
        Assert.AreEqual(WorkspaceId.Initial, moved.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), Client(moved, 2).Workspace);
        Assert.AreSequenceEqual([new WindowId(2)], moved.Hide);
        Assert.AreEqual(Area, BoundsOf(moved, 1));

        var followed = core.Handle(new Dispatch("movetoworkspace", "3"));

        Assert.AreEqual(new WorkspaceId("3"), followed.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), Client(followed, 1).Workspace);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(followed, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(followed, 1));
        Assert.AreSequenceEqual([new WindowId(2), new WindowId(1)], followed.Show);
        Assert.IsEmpty(followed.Hide);
    }

    [TestMethod]
    public void MoveFocusStaysInsideTheActiveWorkspace()
    {
        var core = Load("bind = SUPER, H, movefocus, l");
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

        var stopped = core.Handle(new Dispatch("movefocus", "l"));

        Assert.IsNull(stopped.Focus);
        Assert.AreEqual(new WorkspaceId("2"), stopped.State.ActiveWorkspace);
        Assert.AreEqual(new WindowId(3), core.Handle(new Dispatch("killactive")).Close);

        var back = core.Handle(new Dispatch("workspace", "1"));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(back, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(back, 2));
        Assert.Contains(client => client.Window == new WindowId(3), back.State.Hidden);
        Assert.Contains(client => client.Window == new WindowId(4), back.State.Hidden);
    }

    [TestMethod]
    public void ToggleFloatingAfterSwitchingLeavesTheOtherWorkspaceTiled()
    {
        var core = Load("bind = SUPER, Space, togglefloating");
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
            bind = SUPER, Z, workspace, 0
            bind = SUPER, N, workspace, name:
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
        core.Execute("dispatch focusmonitor RIGHT");
        core.Execute("dispatch workspace 2");
        core.Handle(new WindowAppeared(new WindowId(1), Right));
        core.Handle(new WindowAppeared(new WindowId(2), Right));
        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));
        core.Execute("dispatch focusmonitor LEFT");

        var switched = core.Execute("dispatch workspace previous").Effect;

        Assert.AreEqual(new WarpTo(new Point(2400, 540)), switched.Warp);
    }

    [TestMethod]
    public void NoWarpsKeepsTheCursorWhenASwitchLandsOnAnotherMonitor()
    {
        var core = TwoMonitors("cursor:no_warps = true\n");

        var switched = ShowTheRightsWorkspace(core);

        Assert.IsNull(switched.Warp);
    }

    [TestMethod]
    public void SwitchingOnTheSameMonitorDoesNotMoveTheCursor()
    {
        var core = TwoMonitors("");

        var switched = core.Execute("dispatch workspace 3").Effect;

        Assert.IsNull(switched.Warp);
    }

    private static readonly Rectangle Right = new(1920, 0, 1920, 1080);

    private static Outcome ShowTheRightsWorkspace(WindowManager core)
    {
        core.Execute("dispatch focusmonitor RIGHT");
        core.Execute("dispatch workspace 2");
        core.Execute("dispatch focusmonitor LEFT");
        return core.Execute("dispatch workspace previous").Effect;
    }

    private static WindowManager TwoMonitors(string config)
    {
        var core = Load(config);
        core.Handle(new MonitorsChanged(
        [
            new Monitor("LEFT", "Left", 0, 0, 1920, 1080, 1, true, Area),
            new Monitor("RIGHT", "Right", 1920, 0, 1920, 1080, 1, true, Right),
        ]));
        core.Execute("dispatch focusmonitor LEFT");
        return core;
    }

    private const string Gapless = "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n";

    private static WindowManager Load(string text)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(Gapless + text)));
        return core;
    }

    private static void HoldSuper(WindowManager core) => core.Handle(new KeyPressed("Super_L"));
}
