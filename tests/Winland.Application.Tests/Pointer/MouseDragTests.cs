namespace Winland.Application.Tests;

[TestClass]
public class MouseDragTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    private static readonly WorkspaceId RightWorkspace = WorkspaceId.FromNumber(1, new MonitorId(1));

    [TestMethod]
    public void FloatingMoveFollowsThePointer()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });

        var moved = core.Handle(new PointerMoved(new Point(750, 430)));

        Assert.IsTrue(moved.Live);
        Assert.AreEqual(new Rectangle(690, 370, 640, 400), BoundsOf(moved, 1));
        Assert.IsTrue(Client(moved, 1).Floating);
    }

    [TestMethod]
    public void TiledDragDropsTheClientIntoTheTreeUnderThePointer()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(1000, 100) });
        core.Handle(new PointerMoved(new Point(100, 500)));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(100, 500) });

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(dropped, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(dropped, 1));
        Assert.IsFalse(Client(dropped, 2).Floating);
        Assert.IsFalse(dropped.Live);
    }

    [TestMethod]
    public void SecondDragButtonLeavesTheFirstDragToDropItsClient()
    {
        var core = Flat("bind = Win+mouse:left, move\nbind = Win+mouse:right, resize\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(480, 540) });
        core.Handle(new PointerMoved(new Point(500, 540)));
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(1440, 540) });
        core.Handle(new PointerButtonReleased("mouse:right") { Position = new Point(1440, 540) });

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(500, 540) });

        Assert.IsFalse(Client(dropped, 1).Floating);
        Assert.IsFalse(Client(dropped, 2).Floating);
        Assert.IsFalse(dropped.Live);
    }

    [TestMethod]
    public void SecondGestureBeginLeavesTheFirstDragToDropItsClient()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new PointerGesture("move", new Point(480, 540), PointerDragPhase.Begin, "mouse:left"));
        core.Handle(new PointerGesture("move", new Point(500, 540), PointerDragPhase.Move, "mouse:left"));
        core.Handle(new PointerGesture("resize", new Point(1440, 540), PointerDragPhase.Begin, "mouse:right"));

        var dropped = core.Handle(new PointerGesture("move", new Point(500, 540), PointerDragPhase.End, "mouse:left"));

        Assert.IsFalse(Client(dropped, 1).Floating);
        Assert.IsFalse(Client(dropped, 2).Floating);
    }

    [TestMethod]
    public void FloatingResizeGrowsFromTheGrabbedCorner()
    {
        var core = Flat("bind = Win+mouse:right, resize\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(1270, 730) });

        var resized = core.Handle(new PointerMoved(new Point(1310, 760)));

        Assert.AreEqual(new Rectangle(640, 340, 680, 430), BoundsOf(resized, 1));
    }

    [TestMethod]
    public void TiledResizeChangesTheSplitRatio()
    {
        var core = Flat("bind = Win+mouse:right, resize\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(960, 540) });

        var resized = core.Handle(new PointerMoved(new Point(1060, 540)));

        Assert.AreEqual(new Rectangle(0, 0, 1060, 1080), BoundsOf(resized, 1));
        Assert.AreEqual(new Rectangle(1060, 0, 860, 1080), BoundsOf(resized, 2));
    }

    [TestMethod]
    public void TheDragTakesTheClientUnderThePointerAndFocusesIt()
    {
        var core = Flat("bind = Win+mouse:right, resize\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var pressed = core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(480, 540) });
        var resized = core.Handle(new PointerMoved(new Point(580, 540)));

        Assert.AreEqual(new WindowId(1), pressed.Focus);
        Assert.AreEqual(new Rectangle(0, 0, 1060, 1080), BoundsOf(resized, 1));
        Assert.AreEqual(new Rectangle(1060, 0, 860, 1080), BoundsOf(resized, 2));
    }

    [TestMethod]
    public void APressOffEveryClientDoesNotDrag()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(10, 20) });

        var moved = core.Handle(new PointerMoved(new Point(60, 50)));

        Assert.IsEmpty(moved.Placements);
    }

    [TestMethod]
    public void ALiftedTileIsCenteredOnThePointer()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(1000, 100) });

        var lifted = core.Handle(new PointerMoved(new Point(1200, 300)));

        Assert.AreEqual(new Rectangle(720, -240, 960, 1080), BoundsOf(lifted, 2));
        Assert.IsTrue(Client(lifted, 2).Floating);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(lifted, 1));
    }

    [TestMethod]
    public void ADropFollowsThePointer()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(1000, 100) });
        core.Handle(new PointerMoved(new Point(100, 500)));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(100, 500) });

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(dropped, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(dropped, 1));
    }

    [TestMethod]
    public void ADropSplitsAlongTheLongerSideOfTheTile()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(1000, 100) });
        core.Handle(new PointerMoved(new Point(960, 1000)));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(960, 1000) });

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(dropped, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(dropped, 2));
    }

    [TestMethod]
    public void ResizingMovesTheGrabbedEdgeAndKeepsTheOtherOne()
    {
        var core = FourTiles("");
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(1000, 600) });

        var resized = core.Handle(new PointerMoved(new Point(900, 600)));

        Assert.AreEqual(new Rectangle(0, 0, 860, 1080), BoundsOf(resized, 1));
        Assert.AreEqual(new Rectangle(860, 0, 1060, 540), BoundsOf(resized, 2));
        Assert.AreEqual(new Rectangle(860, 540, 580, 540), BoundsOf(resized, 3));
        Assert.AreEqual(new Rectangle(1440, 540, 480, 540), BoundsOf(resized, 4));
    }

    [TestMethod]
    public void MasterSmartResizingTakesSpaceFromTheTileAboveAGrabbedTopEdge()
    {
        var core = Flat("general:layout = master\nbind = Win+mouse:right, resize\n");
        Outcome placed = null!;
        for (var window = 1; window <= 4; window++)
            placed = Appear(core, window);
        var stack = placed.Placements.Where(placement => placement.Bounds.X > 0).OrderBy(placement => placement.Bounds.Y).ToList();
        Assert.AreEqual(3, stack.Count);
        var above = stack[0];
        var middle = stack[1];
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(middle.Bounds.X + 10, middle.Bounds.Y + 10) });

        var resized = core.Handle(new PointerMoved(new Point(middle.Bounds.X + 10, middle.Bounds.Y - 50)));

        var grown = Assert.ContainsSingle(placement => placement.Client == middle.Client, resized.Placements).Bounds;
        var shrunk = Assert.ContainsSingle(placement => placement.Client == above.Client, resized.Placements).Bounds;
        Assert.AreEqual(middle.Bounds.Y - 60, grown.Y);
        Assert.AreEqual(middle.Bounds.Y + middle.Bounds.Height, grown.Y + grown.Height);
        Assert.AreEqual(above.Bounds.Height - 60, shrunk.Height);
    }

    [TestMethod]
    public void AMouseBindPressedOnTheBorderTakesTheClient()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 2\nbind = Win+mouse:right, resize\n")));
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        var frame = Client(core.Handle(new Dispatch("resize")), 1).Bounds;
        Press(core, "Super_L");

        var corner = new Point(frame.X + frame.Width, frame.Y + frame.Height);
        core.Handle(new PointerButtonPressed("mouse:right") { Position = corner });
        var resized = core.Handle(new PointerMoved(new Point(corner.X + 100, corner.Y + 10)));

        Assert.AreEqual(frame with { Width = frame.Width + 100, Height = frame.Height + 10 }, BoundsOf(resized, 1));
    }

    [TestMethod]
    [DataRow("bindm = Win+mouse:left, move", "Unsupported keyword 'bindm'.")]
    [DataRow("bindmd = Win+mouse:left, \"Drag\", move", "Unsupported keyword 'bindmd'.")]
    [DataRow("bind = Win+Z, mouse move", "Unknown dispatcher 'mouse'.")]
    public void TheOldDragFormsWarnAndAreSkipped(string line, string warning)
    {
        var document = ConfigParser.Parse(line + "\n", dispatchers: WindowManager.DispatcherNames);

        Assert.StartsWith(warning, Assert.ContainsSingle(document.Warnings).Message, StringComparison.Ordinal);
        Assert.IsEmpty(document.Binds);
    }

    [TestMethod]
    public void AMouseKeyWithAMoveToAWorkspaceIsAClickNotADrag()
    {
        var core = Flat("bind = Win+mouse:left, move 2 stay\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");

        var pressed = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        var moved = core.Handle(new PointerMoved(new Point(750, 430)));

        Assert.IsTrue(pressed.Consumed);
        Assert.AreEqual(new WorkspaceId("2"), Client(pressed, 1).Workspace);
        Assert.IsFalse(moved.Live);
    }

    [TestMethod]
    public void ADragPressWithNoWindowUnderThePointerGoesThrough()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");

        var empty = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(10, 10) });
        core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(10, 10) });
        var held = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });

        Assert.IsFalse(empty.Consumed);
        Assert.IsFalse(core.HasDragTarget(new Point(10, 10)));
        Assert.IsTrue(held.Consumed);
        Assert.IsTrue(core.HasDragTarget(new Point(700, 400)));
    }

    [TestMethod]
    public void ATileDroppedOnAnotherMonitorJoinsTheWorkspaceItShows()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(1400, 500) });
        var crossed = core.Handle(new PointerMoved(new Point(2400, 500)));

        var crossing = Client(crossed, 2);
        Assert.IsTrue(crossing.Floating);
        Assert.AreEqual(RightWorkspace, crossing.Workspace);
        Assert.AreEqual(new MonitorId(0), crossed.State.ActiveMonitor);
        Assert.Contains(item => item is ClientMoved changed && changed.Window == new WindowId(2), crossed.Events);
        Assert.AreSequenceEqual([@"movewindow>>2,1,1,\\.\DISPLAY2"], MoveLines(crossed));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(2400, 500) });

        var moved = Client(dropped, 2);
        Assert.IsFalse(moved.Floating);
        Assert.AreNotEqual(Client(dropped, 1).Workspace, moved.Workspace);
        Assert.AreEqual(new Rectangle(1920, 0, 1920, 1080), moved.Bounds);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), Client(dropped, 1).Bounds);
        Assert.DoesNotContain(item => item is ClientMoved, dropped.Events);
    }

    [TestMethod]
    public void AFloatingWindowDroppedOnAnotherMonitorKeepsItsRectangleThere()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        var crossed = core.Handle(new PointerMoved(new Point(2700, 400)));
        Assert.AreEqual(RightWorkspace, Client(crossed, 1).Workspace);
        Assert.AreEqual(new MonitorId(0), crossed.State.ActiveMonitor);

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(2700, 400) });

        var moved = Client(dropped, 1);
        Assert.IsTrue(moved.Floating);
        Assert.AreEqual(new Rectangle(2640, 340, 640, 400), moved.Bounds);
        Assert.AreNotEqual(WorkspaceId.Initial, moved.Workspace);
        Assert.AreEqual(new MonitorId(1), dropped.State.ActiveMonitor);
        Assert.IsEmpty(MoveLines(dropped));
    }

    [TestMethod]
    public void AFloatingDragJoinsTheWorkspaceUnderItsMiddleAndKeepsTheGrab()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });

        var held = core.Handle(new PointerMoved(new Point(1600, 400)));
        Assert.AreEqual(WorkspaceId.Initial, Client(held, 1).Workspace);
        Assert.AreEqual(new Rectangle(1540, 340, 640, 400), BoundsOf(held, 1));
        Assert.IsEmpty(MoveLines(held));

        var crossed = core.Handle(new PointerMoved(new Point(1700, 400)));
        Assert.AreEqual(RightWorkspace, Client(crossed, 1).Workspace);
        Assert.IsTrue(Client(crossed, 1).Floating);
        Assert.AreEqual(new Rectangle(1640, 340, 640, 400), BoundsOf(crossed, 1));
        Assert.AreEqual(new MonitorId(0), crossed.State.ActiveMonitor);
        Assert.AreSequenceEqual([@"movewindow>>1,1,1,\\.\DISPLAY2"], MoveLines(crossed));

        var followed = core.Handle(new PointerMoved(new Point(1800, 400)));
        Assert.AreEqual(RightWorkspace, Client(followed, 1).Workspace);
        Assert.AreEqual(new Rectangle(1740, 340, 640, 400), BoundsOf(followed, 1));
        Assert.IsEmpty(MoveLines(followed));
    }

    [TestMethod]
    public void AFloatingDragInMasterKeepsFollowingAfterItCrosses()
    {
        var core = TwoMonitors("general:layout = master\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        var crossed = core.Handle(new PointerMoved(new Point(2700, 400)));

        var followed = core.Handle(new PointerMoved(new Point(2800, 400)));

        Assert.AreEqual(RightWorkspace, Client(crossed, 1).Workspace);
        Assert.AreSequenceEqual([@"movewindow>>1,1,1,\\.\DISPLAY2"], MoveLines(crossed));
        Assert.AreEqual(new Rectangle(2740, 340, 640, 400), BoundsOf(followed, 1));
        Assert.AreEqual(RightWorkspace, Client(followed, 1).Workspace);
        Assert.IsEmpty(MoveLines(followed));
    }

    [TestMethod]
    public void AFloatingDragCrossesBackOntoTheMonitorItLeft()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        core.Handle(new PointerMoved(new Point(2700, 400)));

        var back = core.Handle(new PointerMoved(new Point(100, 400)));

        Assert.AreEqual(WorkspaceId.Initial, Client(back, 1).Workspace);
        Assert.AreEqual(new Rectangle(40, 340, 640, 400), BoundsOf(back, 1));
        Assert.AreSequenceEqual([@"movewindow>>1,1,1,\\.\DISPLAY1"], MoveLines(back));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(100, 400) });
        Assert.AreEqual(WorkspaceId.Initial, Client(dropped, 1).Workspace);
        Assert.IsTrue(Client(dropped, 1).Floating);
        Assert.AreEqual(new MonitorId(0), dropped.State.ActiveMonitor);
        Assert.IsEmpty(MoveLines(dropped));
    }

    [TestMethod]
    public void ALiftedTileDropsIntoTheLayoutItJoinedAtThePointer()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        var right = new Rectangle(1920, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(2), right));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(960, 540) });

        var crossed = core.Handle(new PointerMoved(new Point(3360, 540)));
        Assert.IsTrue(Client(crossed, 1).Floating);
        Assert.AreEqual(RightWorkspace, Client(crossed, 1).Workspace);
        Assert.AreSequenceEqual([@"movewindow>>1,1,1,\\.\DISPLAY2"], MoveLines(crossed));
        Assert.Contains(line => line == "changefloatingmode>>1,1", Lines(crossed));

        var followed = core.Handle(new PointerMoved(new Point(3400, 540)));
        Assert.IsTrue(Client(followed, 1).Floating);
        Assert.AreEqual(new Rectangle(2440, 0, 1920, 1080), BoundsOf(followed, 1));
        Assert.IsEmpty(MoveLines(followed));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(3360, 540) });
        Assert.IsFalse(Client(dropped, 1).Floating);
        Assert.IsFalse(Client(dropped, 2).Floating);
        Assert.AreEqual(RightWorkspace, Client(dropped, 1).Workspace);
        Assert.AreEqual(RightWorkspace, Client(dropped, 2).Workspace);
        Assert.AreEqual(new Rectangle(2880, 0, 960, 1080), BoundsOf(dropped, 1));
        Assert.AreEqual(new Rectangle(1920, 0, 960, 1080), BoundsOf(dropped, 2));
    }

    [TestMethod]
    public void AFloatingResizeJoinsTheWorkspaceUnderItsMiddleAndKeepsResizing()
    {
        var core = TwoMonitors("bind = Win+mouse:right, resize\n");
        Appear(core, 1);
        var floated = core.Handle(new Dispatch("float"));
        Place(core, 1, Client(floated, 1).Bounds with { X = 1280, Y = 300 });
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(1900, 690) });

        var crossed = core.Handle(new PointerMoved(new Point(2600, 690)));
        Assert.AreEqual(new Rectangle(1280, 300, 1340, 400), BoundsOf(crossed, 1));
        Assert.AreEqual(RightWorkspace, Client(crossed, 1).Workspace);
        Assert.AreEqual(new MonitorId(0), crossed.State.ActiveMonitor);
        Assert.AreSequenceEqual([@"movewindow>>1,1,1,\\.\DISPLAY2"], MoveLines(crossed));

        var grown = core.Handle(new PointerMoved(new Point(2700, 690)));
        Assert.AreEqual(new Rectangle(1280, 300, 1440, 400), BoundsOf(grown, 1));
        Assert.AreEqual(RightWorkspace, Client(grown, 1).Workspace);
        Assert.IsEmpty(MoveLines(grown));

        var dropped = core.Handle(new PointerButtonReleased("mouse:right") { Position = new Point(2700, 690) });
        Assert.AreEqual(RightWorkspace, Client(dropped, 1).Workspace);
        Assert.IsTrue(Client(dropped, 1).Floating);
        Assert.AreEqual(new MonitorId(0), dropped.State.ActiveMonitor);
    }

    [TestMethod]
    public void ATiledResizeDoesNotJoinAnotherMonitor()
    {
        var core = TwoMonitors("bind = Win+mouse:right, resize\n");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(960, 540) });

        var resized = core.Handle(new PointerMoved(new Point(1200, 540)));

        Assert.AreEqual(WorkspaceId.Initial, Client(resized, 1).Workspace);
        Assert.AreEqual(WorkspaceId.Initial, Client(resized, 2).Workspace);
        Assert.IsFalse(Client(resized, 1).Floating);
        Assert.IsEmpty(MoveLines(resized));
    }

    [TestMethod]
    public void ADragJoinsTheSpecialWorkspaceTheOtherMonitorShows()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        core.Handle(new Dispatch("focus-monitor", @"\\.\DISPLAY2"));
        core.Handle(new Dispatch("scratchpad", "scratch"));
        core.Handle(new Dispatch("focus-monitor", @"\\.\DISPLAY1"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });

        var crossed = core.Handle(new PointerMoved(new Point(2700, 400)));

        Assert.AreEqual(new WorkspaceId("special:scratch"), Client(crossed, 1).Workspace);
        Assert.IsTrue(Client(crossed, 1).Floating);
        Assert.AreEqual(new Rectangle(2640, 340, 640, 400), BoundsOf(crossed, 1));
        Assert.AreEqual(new WorkspaceId("special:scratch"), Screen(crossed, 1).SpecialWorkspace);
        Assert.AreSequenceEqual([@"movewindow>>1,-98,special:scratch,\\.\DISPLAY2"], MoveLines(crossed));
        Assert.AreEqual(new MonitorId(0), crossed.State.ActiveMonitor);
    }

    [TestMethod]
    public void ADragJoinsTheSpecialWorkspaceItsOwnMonitorOpens()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        core.Handle(new MonitorsChanged(
        [
            new Monitor(@"\\.\DISPLAY1", "Left", 0, 0, 1920, 1080, 1, true, Wide),
        ]));
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        core.Handle(new PointerMoved(new Point(750, 430)));
        core.Handle(new Dispatch("scratchpad", "scratch"));

        var joined = core.Handle(new PointerMoved(new Point(760, 440)));

        Assert.AreEqual(new WorkspaceId("special:scratch"), Client(joined, 1).Workspace);
        Assert.IsTrue(Client(joined, 1).Floating);
        Assert.AreEqual(new Rectangle(700, 380, 640, 400), BoundsOf(joined, 1));
        Assert.AreSequenceEqual([@"movewindow>>1,-98,special:scratch,\\.\DISPLAY1"], MoveLines(joined));
    }

    [TestMethod]
    public void AWindowDroppedOnItsOwnMonitorStaysOnItsWorkspace()
    {
        var core = TwoMonitors();
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        core.Handle(new PointerMoved(new Point(900, 400)));

        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(900, 400) });

        Assert.AreEqual(WorkspaceId.Initial, Client(dropped, 1).Workspace);
    }

    private static WindowManager TwoMonitors(string extra = "")
    {
        var core = Flat(extra + "bind = Win+mouse:left, move\n");
        core.Handle(new MonitorsChanged(
        [
            new Monitor(@"\\.\DISPLAY1", "Left", 0, 0, 1920, 1080, 1, true, Wide),
            new Monitor(@"\\.\DISPLAY2", "Right", 1920, 0, 1920, 1080, 1, true, new Rectangle(1920, 0, 1920, 1080)),
        ]));
        return core;
    }

    [TestMethod]
    public void InMonocleAFloatingClientFollowsThePointer()
    {
        var core = Flat("general:layout = monocle\nbind = Win+mouse:left, move\n");
        Appear(core, 1);
        core.Handle(new Dispatch("float"));
        Press(core, "Super_L");
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });

        var moved = core.Handle(new PointerMoved(new Point(750, 430)));

        Assert.AreEqual(new Rectangle(690, 370, 640, 400), BoundsOf(moved, 1));
    }

    [TestMethod]
    public void AMoveShowsTheGrabbingPointerUntilItIsDropped()
    {
        var core = Flat("bind = Win+mouse:left, move\n");
        Appear(core, 1);
        Press(core, "Super_L");

        var pressed = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(700, 400) });
        var moved = core.Handle(new PointerMoved(new Point(750, 430)));
        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(750, 430) });

        Assert.AreEqual("grabbing", pressed.ResizeCursor);
        Assert.AreEqual("grabbing", moved.ResizeCursor);
        Assert.IsNull(dropped.ResizeCursor);
    }

    [TestMethod]
    [DataRow(100, 100, "top_left_corner")]
    [DataRow(1800, 100, "top_right_corner")]
    [DataRow(1800, 1000, "bottom_right_corner")]
    [DataRow(100, 1000, "bottom_left_corner")]
    public void AResizeShowsTheCornerItGrabbed(int x, int y, string cursor)
    {
        var core = Flat("bind = Win+mouse:right, resize\n");
        Appear(core, 1);
        Press(core, "Super_L");

        Assert.AreEqual(cursor, core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(x, y) }).ResizeCursor);
    }

    private static WindowManager FourTiles(string options)
    {
        var core = Flat(options + "bind = Win+mouse:right, resize\n");
        Outcome placed = null!;
        for (var window = 1; window <= 4; window++)
            placed = Appear(core, window);
        Assert.AreEqual(new Rectangle(960, 540, 480, 540), BoundsOf(placed, 3));
        Assert.AreEqual(new Rectangle(1440, 540, 480, 540), BoundsOf(placed, 4));
        Press(core, "Super_L");
        return core;
    }

    private static MonitorSnapshot Screen(Outcome outcome, int id) =>
        Assert.ContainsSingle(screen => screen.Id == new MonitorId(id), outcome.State.Screens);

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);

    private static IReadOnlyList<string> MoveLines(Outcome outcome) =>
        [.. Lines(outcome).Where(line => line.StartsWith("movewindow>>", StringComparison.Ordinal))];
}
