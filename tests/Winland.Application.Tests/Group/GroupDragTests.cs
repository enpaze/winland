namespace Winland.Application.Tests;

[TestClass]
public class GroupDragTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ATileDroppedOnAGroupMemberJoinsTheGroup()
    {
        var core = Grouped("");

        var dropped = Drag(core, 2, Middle(Tile(core, 1)));

        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], Assert.ContainsSingle(dropped.State.Groups));
        Assert.AreEqual(new WindowId(2), dropped.Focus);
    }

    [TestMethod]
    public void ATileDroppedOnTheFirstTabGoesInFrontOfIt()
    {
        var core = Grouped("group:drag_into_group = 2\n");
        var bar = Assert.ContainsSingle(core.Handle(new Dispatch("resize")).GroupBars);

        var dropped = Drag(core, 2, new Point(bar.Bounds.X + 2, bar.Bounds.Y + (bar.Bounds.Height / 2)));

        Assert.AreSequenceEqual([new WindowId(2), new WindowId(1)], Assert.ContainsSingle(dropped.State.Groups));
    }

    [TestMethod]
    public void AFloatingWindowDoesNotJoinATiledGroup()
    {
        var core = Grouped("");
        core.Handle(new Dispatch("focus", "address:0x2"));
        core.Handle(new Dispatch("float"));
        var bar = Assert.ContainsSingle(core.Handle(new Dispatch("resize")).GroupBars);

        var onMember = Drag(core, 2, Middle(Tile(core, 1)));
        Assert.ContainsSingle(Assert.ContainsSingle(onMember.State.Groups));

        var onBar = Drag(core, 2, new Point(bar.Bounds.X + 2, bar.Bounds.Y + (bar.Bounds.Height / 2)));
        Assert.ContainsSingle(Assert.ContainsSingle(onBar.State.Groups));
    }

    [TestMethod]
    public void AMoveStartedOnATabTakesThatMemberOutAndDragsItAlone()
    {
        var core = Grouped("");
        core.Handle(new Dispatch("focus", "address:0x2"));
        core.Handle(new Dispatch("join", "l"));
        var bar = Assert.ContainsSingle(core.Handle(new Dispatch("resize")).GroupBars);
        var tab = new Point(bar.Bounds.X + bar.TabWidth + bar.Style.GapsIn + 5, bar.Bounds.Y + (bar.Bounds.Height / 2));
        core.Handle(new KeyPressed("Super_L"));

        var pressed = core.Handle(new PointerButtonPressed("mouse:left") { Position = tab });

        Assert.AreSequenceEqual([new WindowId(1)], Assert.ContainsSingle(pressed.State.Groups));
        Assert.Contains(item => item is ClientOutOfGroup left && left.Window == new WindowId(2), pressed.Events);
    }

    [TestMethod]
    public void AMoveStartedOnTheTabOfAGroupOfOneDragsTheWholeGroup()
    {
        var core = Grouped("");
        var bar = Assert.ContainsSingle(core.Handle(new Dispatch("resize")).GroupBars);
        core.Handle(new KeyPressed("Super_L"));

        var pressed = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(bar.Bounds.X + 5, bar.Bounds.Y + (bar.Bounds.Height / 2)) });

        Assert.AreSequenceEqual([new WindowId(1)], Assert.ContainsSingle(pressed.State.Groups));
        Assert.DoesNotContain(item => item is ClientOutOfGroup, pressed.Events);
    }

    [TestMethod]
    public void ATileDraggedOntoAGroupOnAnotherMonitorJoinsItThere()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"
            + "bind = Win+mouse:left, move\n")));
        var right = new Rectangle(1920, 0, 1920, 1080);
        core.Handle(new MonitorsChanged(
        [
            new Monitor(@"\\.\DISPLAY1", "Left", 0, 0, 1920, 1080, 1, true, Wide),
            new Monitor(@"\\.\DISPLAY2", "Right", 1920, 0, 1920, 1080, 1, true, right),
        ]));
        core.Handle(new WindowAppeared(new WindowId(1), Wide));
        core.Handle(new WindowAppeared(new WindowId(2), right));
        core.Handle(new Dispatch("focus", "address:0x2"));
        core.Handle(new Dispatch("group"));
        core.Handle(new KeyPressed("Super_L"));
        var onto = Middle(Tile(core, 2));
        core.Handle(new PointerButtonPressed("mouse:left") { Position = Middle(Tile(core, 1)) });

        var moved = core.Handle(new PointerMoved(onto));
        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = onto });

        Assert.AreEqual(new WorkspaceId("1", 1), Client(moved, 1).Workspace);
        Assert.IsTrue(Client(moved, 1).Floating);
        Assert.AreEqual(new MonitorId(0), moved.State.ActiveMonitor);
        Assert.AreSequenceEqual([new WindowId(2)], Assert.ContainsSingle(moved.State.Groups));
        Assert.AreSequenceEqual([@"movewindow>>1,1,1,\\.\DISPLAY2"], MoveLines(moved));
        Assert.AreSequenceEqual([new WindowId(2), new WindowId(1)], Assert.ContainsSingle(dropped.State.Groups));
        Assert.AreEqual(new WindowId(1), dropped.Focus);
        Assert.AreEqual(new MonitorId(1), dropped.State.ActiveMonitor);
        Assert.IsFalse(Client(dropped, 1).Floating);
        Assert.IsEmpty(MoveLines(dropped));
    }

    [TestMethod]
    public void AWindowHeldOverATabBarMarksWhereItWouldJoinAndJoinsThere()
    {
        var core = GroupOfTwo("");
        var bar = Bar(core);
        Hold(core, 2);

        var first = core.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2)));
        var held = Assert.ContainsSingle(first.GroupBars);
        var pastFirstTab = new Point(held.Bounds.X + (held.TabWidth / 2) + 10, held.Bounds.Y + 2);
        var past = core.Handle(new PointerMoved(pastFirstTab));
        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = pastFirstTab });

        Assert.AreEqual(0, held.Insertion);
        Assert.AreEqual(1, Assert.ContainsSingle(past.GroupBars).Insertion);
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2), new WindowId(3)], Assert.ContainsSingle(dropped.State.Groups));
        Assert.IsNull(Assert.ContainsSingle(dropped.GroupBars).Insertion);
    }

    [TestMethod]
    public void AWindowThatCannotJoinMarksNothingAndTheMarkGoesWhenItStopsBeingAllowed()
    {
        var floating = GroupOfTwo("");
        floating.Handle(new Dispatch("focus", "address:0x2"));
        floating.Handle(new Dispatch("float"));
        var bar = Bar(floating);
        Hold(floating, 2);

        var refused = floating.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2)));

        Assert.IsNull(Assert.ContainsSingle(refused.GroupBars).Insertion);

        var core = GroupOfTwo("");
        Hold(core, 2);
        Assert.AreEqual(0, Assert.ContainsSingle(core.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2))).GroupBars).Insertion);

        var off = core.Handle(new Configure(ConfigParser.Parse("group:drag_into_group = 0\n")));

        Assert.IsNull(Assert.ContainsSingle(off.GroupBars).Insertion);
    }

    [TestMethod]
    public void TheMarkGoesWithTheGroupOrTheWindowItWasFor()
    {
        var core = GroupOfTwo("");
        var bar = Bar(core);
        var over = new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2);
        Hold(core, 2);
        core.Handle(new PointerMoved(over));

        var lost = core.Handle(new WindowClosed(new WindowId(2)));

        Assert.IsNull(Assert.ContainsSingle(lost.GroupBars).Insertion);

        var alone = Grouped("");
        var only = Bar(alone);
        Hold(alone, 2);
        alone.Handle(new PointerMoved(new Point(only.Bounds.X + 2, only.Bounds.Y + 2)));

        var gone = alone.Handle(new WindowClosed(new WindowId(1)));
        var dropped = alone.Handle(new PointerButtonReleased("mouse:left") { Position = new Point(only.Bounds.X + 2, only.Bounds.Y + 2) });

        Assert.IsEmpty(gone.GroupBars);
        Assert.IsEmpty(dropped.State.Groups);
    }

    [TestMethod]
    public void AWindowMovedByItsTitleBarMarksWhereItWouldJoinToo()
    {
        var core = GroupOfTwo("");
        var bar = Bar(core);
        core.Handle(new WindowMoveStarted(new WindowId(2), Middle(Tile(core, 2))));
        var held = Assert.ContainsSingle(core.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2))).GroupBars);
        var over = new Point(held.Bounds.X + (held.TabWidth / 2) + 10, held.Bounds.Y + 2);

        var moved = core.Handle(new PointerMoved(over));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), over, new Rectangle(over.X - 100, over.Y - 10, 400, 300)));

        Assert.AreEqual(1, Assert.ContainsSingle(moved.GroupBars).Insertion);
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2), new WindowId(3)], Assert.ContainsSingle(dropped.State.Groups));
        Assert.IsNull(Assert.ContainsSingle(dropped.GroupBars).Insertion);
    }

    private static WindowManager GroupOfTwo(string extra)
    {
        var core = Grouped(extra);
        core.Handle(new WindowAppeared(new WindowId(3), Wide));
        core.Handle(new Dispatch("focus", "address:0x3"));
        core.Handle(new Dispatch("join", "address:0x1"));
        core.Handle(new Dispatch("tab", "1"));
        return core;
    }

    private static GroupBar Bar(WindowManager core) => Assert.ContainsSingle(core.Handle(new Dispatch("resize")).GroupBars);

    private static void Hold(WindowManager core, long window)
    {
        var from = Middle(Tile(core, window));
        core.Handle(new KeyPressed("Super_L"));
        core.Handle(new PointerButtonPressed("mouse:left") { Position = from });
    }

    private static WindowManager Grouped(string extra)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"
            + "bind = Win+mouse:left, move\n" + extra)));
        core.Handle(new WindowAppeared(new WindowId(1), Wide));
        core.Handle(new WindowAppeared(new WindowId(2), Wide));
        core.Handle(new Dispatch("focus", "address:0x1"));
        core.Handle(new Dispatch("group"));
        return core;
    }

    private static Outcome Drag(WindowManager core, long window, Point to)
    {
        var from = Middle(Tile(core, window));
        core.Handle(new KeyPressed("Super_L"));
        core.Handle(new PointerButtonPressed("mouse:left") { Position = from });
        core.Handle(new PointerMoved(to));
        var dropped = core.Handle(new PointerButtonReleased("mouse:left") { Position = to });
        core.Handle(new KeyReleased("Super_L"));
        return dropped;
    }

    private static Rectangle Tile(WindowManager core, long window) =>
        Assert.ContainsSingle(client => client.Window == new WindowId(window), core.Handle(new Dispatch("resize")).State.Clients).Bounds;

    private static IReadOnlyList<string> MoveLines(Outcome outcome) =>
        [.. CommandProtocol.FormatStream(outcome.Events, outcome.State).Where(line =>
            line.StartsWith("movewindow>>", StringComparison.Ordinal))];

    private static Point Middle(Rectangle box) => new(box.X + (box.Width / 2), box.Y + (box.Height / 2));
}
