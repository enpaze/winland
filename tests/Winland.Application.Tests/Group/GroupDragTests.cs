namespace Winland.Application.Tests;

public class GroupDragTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [Fact]
    public void ATileDroppedOnAGroupMemberJoinsTheGroup()
    {
        var core = Grouped("");

        var dropped = Drag(core, 2, Middle(Tile(core, 1)));

        Assert.Equal([new WindowId(1), new WindowId(2)], Assert.Single(dropped.State.Groups));
        Assert.Equal(new WindowId(2), dropped.Focus);
    }

    [Fact]
    public void ATileDroppedOnTheFirstTabGoesInFrontOfIt()
    {
        var core = Grouped("group:drag_into_group = 2\n");
        var bar = Assert.Single(core.Handle(new Dispatch("resizewindow")).GroupBars);

        var dropped = Drag(core, 2, new Point(bar.Bounds.X + 2, bar.Bounds.Y + (bar.Bounds.Height / 2)));

        Assert.Equal([new WindowId(2), new WindowId(1)], Assert.Single(dropped.State.Groups));
    }

    [Fact]
    public void AFloatingWindowDoesNotJoinATiledGroup()
    {
        var core = Grouped("");
        core.Handle(new Dispatch("focuswindow", "address:0x2"));
        core.Handle(new Dispatch("togglefloating"));
        var bar = Assert.Single(core.Handle(new Dispatch("resizewindow")).GroupBars);

        var onMember = Drag(core, 2, Middle(Tile(core, 1)));
        Assert.Single(Assert.Single(onMember.State.Groups));

        var onBar = Drag(core, 2, new Point(bar.Bounds.X + 2, bar.Bounds.Y + (bar.Bounds.Height / 2)));
        Assert.Single(Assert.Single(onBar.State.Groups));
    }

    [Fact]
    public void AMoveStartedOnATabTakesThatMemberOutAndDragsItAlone()
    {
        var core = Grouped("");
        core.Handle(new Dispatch("focuswindow", "address:0x2"));
        core.Handle(new Dispatch("moveintogroup", "l"));
        var bar = Assert.Single(core.Handle(new Dispatch("resizewindow")).GroupBars);
        var tab = new Point(bar.Bounds.X + bar.TabWidth + bar.Style.GapsIn + 5, bar.Bounds.Y + (bar.Bounds.Height / 2));
        core.Handle(new KeyPressed("Super_L"));

        var pressed = core.Handle(new PointerButtonPressed("mouse:272") { Position = tab });

        Assert.Equal([new WindowId(1)], Assert.Single(pressed.State.Groups));
        Assert.Contains(pressed.Events, item => item is ClientOutOfGroup left && left.Window == new WindowId(2));
    }

    [Fact]
    public void AMoveStartedOnTheTabOfAGroupOfOneDragsTheWholeGroup()
    {
        var core = Grouped("");
        var bar = Assert.Single(core.Handle(new Dispatch("resizewindow")).GroupBars);
        core.Handle(new KeyPressed("Super_L"));

        var pressed = core.Handle(new PointerButtonPressed("mouse:272") { Position = new Point(bar.Bounds.X + 5, bar.Bounds.Y + (bar.Bounds.Height / 2)) });

        Assert.Equal([new WindowId(1)], Assert.Single(pressed.State.Groups));
        Assert.DoesNotContain(pressed.Events, item => item is ClientOutOfGroup);
    }

    [Fact]
    public void ATileDraggedOntoAGroupOnAnotherMonitorJoinsItThere()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"
            + "bindm = SUPER, mouse:272, movewindow\n")));
        var right = new Rectangle(1920, 0, 1920, 1080);
        core.Handle(new MonitorsChanged(
        [
            new Monitor(@"\\.\DISPLAY1", "Left", 0, 0, 1920, 1080, 1, true, Wide),
            new Monitor(@"\\.\DISPLAY2", "Right", 1920, 0, 1920, 1080, 1, true, right),
        ]));
        core.Handle(new WindowAppeared(new WindowId(1), Wide));
        core.Handle(new WindowAppeared(new WindowId(2), right));
        core.Handle(new Dispatch("focuswindow", "address:0x2"));
        core.Handle(new Dispatch("togglegroup"));
        core.Handle(new KeyPressed("Super_L"));
        var onto = Middle(Tile(core, 2));
        core.Handle(new PointerButtonPressed("mouse:272") { Position = Middle(Tile(core, 1)) });

        var moved = core.Handle(new PointerMoved(onto));
        var dropped = core.Handle(new PointerButtonReleased("mouse:272") { Position = onto });

        Assert.Equal(new WorkspaceId("1", 1), Client(moved, 1).Workspace);
        Assert.True(Client(moved, 1).Floating);
        Assert.Equal(new MonitorId(0), moved.State.ActiveMonitor);
        Assert.Equal([new WindowId(2)], Assert.Single(moved.State.Groups));
        Assert.Equal([@"movewindow>>1,1,\\.\DISPLAY2", @"movewindowv2>>1,1,1,\\.\DISPLAY2"], MoveLines(moved));
        Assert.Equal([new WindowId(2), new WindowId(1)], Assert.Single(dropped.State.Groups));
        Assert.Equal(new WindowId(1), dropped.Focus);
        Assert.Equal(new MonitorId(1), dropped.State.ActiveMonitor);
        Assert.False(Client(dropped, 1).Floating);
        Assert.Empty(MoveLines(dropped));
    }

    [Fact]
    public void AWindowHeldOverATabBarMarksWhereItWouldJoinAndJoinsThere()
    {
        var core = GroupOfTwo("");
        var bar = Bar(core);
        Hold(core, 2);

        var first = core.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2)));
        var held = Assert.Single(first.GroupBars);
        var pastFirstTab = new Point(held.Bounds.X + (held.TabWidth / 2) + 10, held.Bounds.Y + 2);
        var past = core.Handle(new PointerMoved(pastFirstTab));
        var dropped = core.Handle(new PointerButtonReleased("mouse:272") { Position = pastFirstTab });

        Assert.Equal(0, held.Insertion);
        Assert.Equal(1, Assert.Single(past.GroupBars).Insertion);
        Assert.Equal([new WindowId(1), new WindowId(2), new WindowId(3)], Assert.Single(dropped.State.Groups));
        Assert.Null(Assert.Single(dropped.GroupBars).Insertion);
    }

    [Fact]
    public void AWindowThatCannotJoinMarksNothingAndTheMarkGoesWhenItStopsBeingAllowed()
    {
        var floating = GroupOfTwo("");
        floating.Handle(new Dispatch("focuswindow", "address:0x2"));
        floating.Handle(new Dispatch("togglefloating"));
        var bar = Bar(floating);
        Hold(floating, 2);

        var refused = floating.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2)));

        Assert.Null(Assert.Single(refused.GroupBars).Insertion);

        var core = GroupOfTwo("");
        Hold(core, 2);
        Assert.Equal(0, Assert.Single(core.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2))).GroupBars).Insertion);

        var off = core.Handle(new Configure(ConfigParser.Parse("group:drag_into_group = 0\n")));

        Assert.Null(Assert.Single(off.GroupBars).Insertion);
    }

    [Fact]
    public void TheMarkGoesWithTheGroupOrTheWindowItWasFor()
    {
        var core = GroupOfTwo("");
        var bar = Bar(core);
        var over = new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2);
        Hold(core, 2);
        core.Handle(new PointerMoved(over));

        var lost = core.Handle(new WindowClosed(new WindowId(2)));

        Assert.Null(Assert.Single(lost.GroupBars).Insertion);

        var alone = Grouped("");
        var only = Bar(alone);
        Hold(alone, 2);
        alone.Handle(new PointerMoved(new Point(only.Bounds.X + 2, only.Bounds.Y + 2)));

        var gone = alone.Handle(new WindowClosed(new WindowId(1)));
        var dropped = alone.Handle(new PointerButtonReleased("mouse:272") { Position = new Point(only.Bounds.X + 2, only.Bounds.Y + 2) });

        Assert.Empty(gone.GroupBars);
        Assert.Empty(dropped.State.Groups);
    }

    [Fact]
    public void AWindowMovedByItsTitleBarMarksWhereItWouldJoinToo()
    {
        var core = GroupOfTwo("");
        var bar = Bar(core);
        core.Handle(new WindowMoveStarted(new WindowId(2), Middle(Tile(core, 2))));
        var held = Assert.Single(core.Handle(new PointerMoved(new Point(bar.Bounds.X + 2, bar.Bounds.Y + 2))).GroupBars);
        var over = new Point(held.Bounds.X + (held.TabWidth / 2) + 10, held.Bounds.Y + 2);

        var moved = core.Handle(new PointerMoved(over));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), over, new Rectangle(over.X - 100, over.Y - 10, 400, 300)));

        Assert.Equal(1, Assert.Single(moved.GroupBars).Insertion);
        Assert.Equal([new WindowId(1), new WindowId(2), new WindowId(3)], Assert.Single(dropped.State.Groups));
        Assert.Null(Assert.Single(dropped.GroupBars).Insertion);
    }

    private static WindowManager GroupOfTwo(string extra)
    {
        var core = Grouped(extra);
        core.Handle(new WindowAppeared(new WindowId(3), Wide));
        core.Handle(new Dispatch("focuswindow", "address:0x3"));
        core.Handle(new Dispatch("moveintogroup", "address:0x1"));
        core.Handle(new Dispatch("changegroupactive", "1"));
        return core;
    }

    private static GroupBar Bar(WindowManager core) => Assert.Single(core.Handle(new Dispatch("resizewindow")).GroupBars);

    private static void Hold(WindowManager core, long window)
    {
        var from = Middle(Tile(core, window));
        core.Handle(new KeyPressed("Super_L"));
        core.Handle(new PointerButtonPressed("mouse:272") { Position = from });
    }

    private static WindowManager Grouped(string extra)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"
            + "bindm = SUPER, mouse:272, movewindow\n" + extra)));
        core.Handle(new WindowAppeared(new WindowId(1), Wide));
        core.Handle(new WindowAppeared(new WindowId(2), Wide));
        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        core.Handle(new Dispatch("togglegroup"));
        return core;
    }

    private static Outcome Drag(WindowManager core, long window, Point to)
    {
        var from = Middle(Tile(core, window));
        core.Handle(new KeyPressed("Super_L"));
        core.Handle(new PointerButtonPressed("mouse:272") { Position = from });
        core.Handle(new PointerMoved(to));
        var dropped = core.Handle(new PointerButtonReleased("mouse:272") { Position = to });
        core.Handle(new KeyReleased("Super_L"));
        return dropped;
    }

    private static Rectangle Tile(WindowManager core, long window) =>
        Assert.Single(core.Handle(new Dispatch("resizewindow")).State.Clients, client => client.Window == new WindowId(window)).Bounds;

    private static IReadOnlyList<string> MoveLines(Outcome outcome) =>
        [.. CommandProtocol.FormatStream(outcome.Events, outcome.State).Where(line =>
            line.StartsWith("movewindow>>", StringComparison.Ordinal) || line.StartsWith("movewindowv2>>", StringComparison.Ordinal))];

    private static Point Middle(Rectangle box) => new(box.X + (box.Width / 2), box.Y + (box.Height / 2));
}
