namespace Winland.Application.Tests;

[TestClass]
public class MonitorDispatchTests
{
    [TestMethod]
    public void MoveFocusCrossesOntoTheNeighbouringMonitor()
    {
        var core = TwoMonitors();

        var outcome = core.Handle(new Dispatch("movefocus", "r"));

        Assert.AreEqual(new WindowId(2), outcome.Focus);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), outcome.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("1"), Client(outcome, 1).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 2).Workspace);
        Assert.AreSequenceEqual(
            [
                @"focus>>\\.\DISPLAY2,1,1,2,",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void MoveWindowCrossesOntoTheNeighbouringMonitor()
    {
        var core = TwoMonitors();

        var outcome = core.Handle(new Dispatch("movewindow", "r"));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), outcome.State.ActiveWorkspace);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 1).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 2).Workspace);
        Assert.AreEqual(new Rectangle(1920, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(2880, 0, 960, 1080), BoundsOf(outcome, 2));
        Assert.AreSequenceEqual(
            [
                @"movewindow>>1,1,1,\\.\DISPLAY2",
                @"focus>>\\.\DISPLAY2,1,1,1,",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void MoveFocusStillCrossesWhenOuterGapsKeepTheClientInset()
    {
        var core = Gapped();

        var outcome = core.Handle(new Dispatch("movefocus", "r"));

        Assert.AreEqual(new WindowId(2), outcome.Focus);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
    }

    [TestMethod]
    public void MoveFocusCrossesToTheTileThatSharesTheEdgeWithDefaultGaps()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 5\ngeneral:gaps_out = 20\ngeneral:border_size = 2\n")));
        var left = Display(@"\\.\DISPLAY1", "Dell", new Rectangle(0, 0, 1080, 1920));
        var right = Display(@"\\.\DISPLAY2", "Samsung", new Rectangle(1080, 0, 1080, 1920));
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(3), right.WorkArea));
        var opened = core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea));
        Assert.IsTrue(Client(opened, 3).Bounds.Y < Client(opened, 4).Bounds.Y);
        Assert.AreEqual(new WindowId(1), core.Handle(new Dispatch("focuswindow", "address:0x1")).Focus);

        var outcome = core.Handle(new Dispatch("movefocus", "r"));

        Assert.AreEqual(new WindowId(3), outcome.Focus);
    }

    [TestMethod]
    [DataRow(1, (long)4)]
    [DataRow(2, (long)2)]
    public void MoveFocusCrossesToAClientWithinAOnePixelGapButNotTwo(int gap, long expected)
    {
        var core = TwoMonitors();
        var left = Display(@"\\.\DISPLAY1", "Dell", Left);
        var right = Display(@"\\.\DISPLAY2", "Samsung", Right);
        Float(core, 2, new Rectangle(2500, 900, 100, 100));
        core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea));
        Float(core, 3, new Rectangle(Right.X - gap - 100, 100, 100, 100));
        core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea));
        Float(core, 4, new Rectangle(Right.X, 100, 100, 100));
        Focus(core, 2);
        Focus(core, 3);

        var outcome = core.Handle(new Dispatch("movefocus", "r"));

        Assert.AreEqual(new WindowId(expected), outcome.Focus);
    }

    [TestMethod]
    public void MoveFocusCrossesToTheMostRecentNeighbor()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n")));
        var left = Display(@"\\.\DISPLAY1", "Dell", new Rectangle(0, 0, 1080, 1920));
        var right = Display(@"\\.\DISPLAY2", "Samsung", new Rectangle(1080, -300, 1080, 1920));
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        var opened = core.Handle(new WindowAppeared(new WindowId(3), right.WorkArea));
        Assert.AreEqual(new Rectangle(1080, 660, 1080, 960), Client(opened, 3).Bounds);
        Focus(core, 3);
        Focus(core, 2);
        Focus(core, 1);

        var outcome = core.Handle(new Dispatch("movefocus", "r"));

        Assert.AreEqual(new WindowId(2), outcome.Focus);
    }

    [TestMethod]
    public void MoveFocusFromAFloatingClientCrossesToAFloatingClientBeforeATile()
    {
        var core = TwoMonitors();
        var left = Display(@"\\.\DISPLAY1", "Dell", Left);
        var right = Display(@"\\.\DISPLAY2", "Samsung", Right);
        core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea));
        Float(core, 3, new Rectangle(Right.X - 100, 100, 100, 100));
        core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea));
        Float(core, 4, new Rectangle(Right.X, 100, 100, 100));
        Focus(core, 4);
        Focus(core, 2);
        Focus(core, 3);

        var outcome = core.Handle(new Dispatch("movefocus", "r"));

        Assert.AreEqual(new WindowId(4), outcome.Focus);
    }

    private static void Focus(WindowManager core, long window)
    {
        core.Handle(new Dispatch("focuswindow", $"address:0x{window:x}"));
        core.Handle(new WindowFocused(new WindowId(window)));
    }

    private static void Float(WindowManager core, long window, Rectangle bounds)
    {
        Focus(core, window);
        core.Handle(new Dispatch("togglefloating"));
        var moved = Place(core, window, bounds);
        Assert.AreEqual(bounds, Client(moved, window).Bounds);
    }

    [TestMethod]
    public void DwindleMoveWindowCrossesFromATileAtTheEdgeOfTheGaps()
    {
        var core = Gapped();

        var outcome = core.Handle(new Dispatch("movewindow", "r"));

        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 1).Workspace);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
    }

    [TestMethod]
    public void MasterMoveWindowCrossesWhenItCannotMoveInsideTheWorkspace()
    {
        var core = Gapped("""
            general:layout = master
            general:gaps_in = 0
            general:gaps_out = 20
            general:border_size = 0
            """);

        var outcome = core.Handle(new Dispatch("movewindow", "r"));

        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 1).Workspace);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), outcome.State.ActiveWorkspace);
    }

    [TestMethod]
    public void FocusMonitorAddressesDirectionNameIdAndDescription()
    {
        var core = TwoMonitors();

        var right = core.Handle(new Dispatch("focusmonitor", "r"));
        Assert.AreEqual(new WindowId(2), right.Focus);
        Assert.AreEqual(new MonitorId(1), right.State.ActiveMonitor);
        Assert.AreSequenceEqual(
            [
                @"focus>>\\.\DISPLAY2,1,1,2,",
            ],
            Lines(right));

        var same = core.Handle(new Dispatch("focusmonitor", "current"));
        Assert.IsNull(same.Error);
        Assert.AreEqual(new MonitorId(1), same.State.ActiveMonitor);
        Assert.IsEmpty(same.Events);

        var left = core.Handle(new Dispatch("focusmonitor", @"\\.\DISPLAY1"));
        Assert.AreEqual(new WindowId(1), left.Focus);
        Assert.AreEqual(new MonitorId(0), left.State.ActiveMonitor);

        var byId = core.Handle(new Dispatch("focusmonitor", "1"));
        Assert.AreEqual(new MonitorId(1), byId.State.ActiveMonitor);
        Assert.AreEqual(new WindowId(2), byId.Focus);

        var described = core.Handle(new Dispatch("focusmonitor", "desc:Dell"));
        Assert.AreEqual(new MonitorId(0), described.State.ActiveMonitor);

        var relative = core.Handle(new Dispatch("focusmonitor", "+1"));
        Assert.AreEqual(new MonitorId(1), relative.State.ActiveMonitor);

        var missing = core.Handle(new Dispatch("focusmonitor", "missing"));
        Assert.AreEqual("error: monitor not found", missing.Error);
        Assert.AreEqual(new MonitorId(1), missing.State.ActiveMonitor);
    }

    [TestMethod]
    public void MoveCurrentWorkspaceFollowsOntoTheOtherMonitor()
    {
        var core = TwoMonitors();

        var outcome = core.Handle(new Dispatch("movecurrentworkspacetomonitor", "right"));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(2, new MonitorId(1)), outcome.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), Workspace(outcome, new WorkspaceId("2")).Monitor);
        Assert.AreEqual(new MonitorId(1), Workspace(outcome, WorkspaceId.FromNumber(2, new MonitorId(1))).Monitor);
        Assert.AreEqual(WorkspaceId.FromNumber(2, new MonitorId(1)), Client(outcome, 1).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 2).Workspace);
        Assert.AreEqual(Right, BoundsOf(outcome, 1));
        Assert.AreSequenceEqual([new WindowId(2)], outcome.Hide);
        Assert.Contains(client => client.Window == new WindowId(2), outcome.State.Hidden);
        Assert.AreSequenceEqual(
            [
                @"createworkspace>>2,2,\\.\DISPLAY1",
                @"moveworkspace>>2,2,\\.\DISPLAY2",
                @"focus>>\\.\DISPLAY2,2,2,1,",
            ],
            Lines(outcome));

        var monitors = System.Text.Json.JsonElement.Parse(core.Execute("j/monitors").Text).EnumerateArray().ToArray();
        Assert.AreEqual("2", monitors[0].GetProperty("activeWorkspace").GetProperty("name").GetString());
        Assert.AreEqual(@"\\.\DISPLAY1", monitors[0].GetProperty("activeWorkspace").GetProperty("monitor").GetString());
        Assert.IsFalse(monitors[0].GetProperty("focused").GetBoolean());
        Assert.AreEqual("2", monitors[1].GetProperty("activeWorkspace").GetProperty("name").GetString());
        Assert.AreEqual(@"\\.\DISPLAY2", monitors[1].GetProperty("activeWorkspace").GetProperty("monitor").GetString());
        Assert.IsTrue(monitors[1].GetProperty("focused").GetBoolean());
    }

    [TestMethod]
    public void MovingTheActiveWorkspaceLeavesAPinnedClientOnTheSourceMonitor()
    {
        var core = TwoMonitors("windowrule = float, class:stick\n");
        core.Handle(new WindowAppeared(new WindowId(4), Left) { Traits = WindowTraits.Normal with { Class = "stick" } });
        core.Handle(new Dispatch("pin"));

        var outcome = core.Handle(new Dispatch("movecurrentworkspacetomonitor", "right"));

        var pinned = Client(outcome, 4);
        Assert.IsTrue(pinned.Pinned);
        Assert.AreEqual(new WorkspaceId("2"), pinned.Workspace);
        Assert.AreEqual(new MonitorId(0), Workspace(outcome, new WorkspaceId("2")).Monitor);
        Assert.DoesNotContain(client => client.Window == new WindowId(4), outcome.State.Hidden);
        Assert.AreEqual(WorkspaceId.FromNumber(2, new MonitorId(1)), Client(outcome, 1).Workspace);
        Assert.AreEqual(new MonitorId(1), Workspace(outcome, WorkspaceId.FromNumber(2, new MonitorId(1))).Monitor);
    }

    [TestMethod]
    public void MoveWindowMonTargetsTheNamedMonitor()
    {
        var core = TwoMonitors();

        var outcome = core.Handle(new Dispatch("movewindow", @"mon:\\.\DISPLAY2"));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(outcome, 1).Workspace);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
    }

    private static readonly Rectangle Left = new(0, 0, 1920, 1080);

    private static readonly Rectangle Right = new(1920, 0, 1920, 1080);

    [TestMethod]
    public void MoveFocusOntoAnotherMonitorMovesTheCursorToTheWindowsMiddle()
    {
        var core = TwoMonitors();

        var moved = core.Handle(new Dispatch("movefocus", "r"));

        var target = Client(moved, 2).Bounds;
        Assert.AreEqual(new WarpTo(new Point(target.X + (target.Width / 2), target.Y + (target.Height / 2))), moved.Warp);
    }

    [TestMethod]
    public void NoWarpsKeepsTheCursorWhereItIsOnMoveFocus()
    {
        var core = TwoMonitors("cursor:no_warps = true\n");

        var moved = core.Handle(new Dispatch("movefocus", "r"));

        Assert.IsNull(moved.Warp);
    }

    [TestMethod]
    [DataRow("bind = SUPER, K, exec, notepad\nbind = SUPER, K, movefocus, r\n")]
    [DataRow("bind = SUPER, K, movefocus, r\nbind = SUPER, K, exec, notepad\n")]
    public void ABindThatMovesFocusMovesTheCursorWhateverOtherDispatchersItRuns(string config)
    {
        var core = TwoMonitors(config);
        core.Handle(new KeyPressed("Super_L"));

        var moved = core.Handle(new KeyPressed("K"));

        var target = Client(moved, 2).Bounds;
        Assert.AreEqual(new WarpTo(new Point(target.X + (target.Width / 2), target.Y + (target.Height / 2))), moved.Warp);
    }

    [TestMethod]
    public void MoveFocusAnswersNoErrorWhenNothingTakesTheFocus()
    {
        var core = TwoMonitors();

        var moved = core.Handle(new Dispatch("movefocus", "l"));

        Assert.IsNull(moved.Error);
    }

    private static WindowManager Gapped(string? config = null)
    {
        var text = config ?? """
            general:gaps_in = 0
            general:gaps_out = 20
            general:border_size = 0
            """;
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(text)));
        var left = Display(@"\\.\DISPLAY1", "Dell", Left);
        var right = Display(@"\\.\DISPLAY2", "Samsung", Right);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        return core;
    }

    private static Workspace Workspace(Outcome outcome, WorkspaceId id) =>
        Assert.ContainsSingle(workspace => workspace.Id == id, outcome.State.Workspaces);

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);
}
