namespace Winland.Application.Tests;

[TestClass]
public class ScrollingLayoutTests
{
    private static readonly double[] DefaultColumnWidths = [0.333, 0.5, 0.667, 1.0];
    private static readonly double[] ParsedColumnWidths = [0.2, 0.4];
    private static readonly Rectangle Area = new(0, 0, 1000, 800);

    [TestMethod]
    public void TapeWiderThanTheMonitorShowsTheFittedColumnsAndOmitsTheRest()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.Add(Window(2), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        var placed = layout.Add(Window(3), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        Assert.DoesNotContain(item => item.Client == new ClientId(1), placed);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(placed, 2));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Box(placed, 3));
    }

    [TestMethod]
    public void OneColumnSpansTheMonitor()
    {
        var layout = Open();

        var placed = layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        Assert.AreEqual(Area, Box(placed, 1));
    }

    [TestMethod]
    public void InnerGapsOpenBetweenColumns()
    {
        var layout = Open();
        layout.Apply(LayoutOptions.Unconfigured with { GapsIn = EdgeInsets.Uniform(10) });
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        var placed = layout.Add(Window(2), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        Assert.AreEqual(new Rectangle(0, 0, 490, 800), Box(placed, 1));
        Assert.AreEqual(new Rectangle(510, 0, 490, 800), Box(placed, 2));
    }

    [TestMethod]
    public void APartlyVisibleColumnKeepsItsTapePosition()
    {
        var layout = Tape();

        var placed = layout.LayoutMessage(Window(3), "move +100");

        Assert.AreEqual(new Rectangle(-400, 0, 500, 800), Box(placed, 1));
        Assert.AreEqual(new Rectangle(100, 0, 500, 800), Box(placed, 2));
        Assert.AreEqual(new Rectangle(600, 0, 500, 800), Box(placed, 3));
    }

    [TestMethod]
    public void ColumnResizeAbsoluteAndConfiguredWidths()
    {
        var layout = Tape();

        var absolute = layout.LayoutMessage(Window(3), "colresize 1");
        Assert.AreEqual(new Rectangle(0, 0, 1000, 800), Box(absolute, 3));
        Assert.DoesNotContain(item => item.Client == new ClientId(1), absolute);

        var fresh = Tape();
        var configured = fresh.LayoutMessage(Window(3), "colresize +conf");
        Assert.AreEqual(new Rectangle(333, 0, 667, 800), Box(configured, 3));

        var back = fresh.LayoutMessage(Window(3), "colresize -conf");
        Assert.AreEqual(500, Box(back, 3).Width);
    }

    [TestMethod]
    public void ColumnResizeAllAndRelative()
    {
        var layout = Tape();

        var all = layout.LayoutMessage(Window(3), "colresize all 0.25");
        Assert.AreEqual(250, Box(all, 1).Width);
        Assert.AreEqual(250, Box(all, 2).Width);
        Assert.AreEqual(250, Box(all, 3).Width);

        var grown = layout.LayoutMessage(Window(1), "colresize +0.25");
        Assert.AreEqual(500, Box(grown, 1).Width);
    }

    [TestMethod]
    public void FitAllSharesTheMonitor()
    {
        var layout = Tape();

        var placed = layout.LayoutMessage(Window(3), "fit all");

        Assert.AreEqual(new Rectangle(0, 0, 333, 800), Box(placed, 1));
        Assert.AreEqual(new Rectangle(333, 0, 334, 800), Box(placed, 2));
        Assert.AreEqual(new Rectangle(667, 0, 333, 800), Box(placed, 3));
    }

    [TestMethod]
    public void FocusWrapsToTheFirstColumnAndBringsItIntoView()
    {
        var layout = Tape();

        var placed = layout.LayoutMessage(Window(3), "focus l");

        Assert.AreEqual(Window(2), layout.LayoutMessageFocus);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(placed, 2));
        Assert.DoesNotContain(item => item.Client == new ClientId(1), placed);

        placed = layout.LayoutMessage(Window(2), "focus l");

        Assert.AreEqual(Window(1), layout.LayoutMessageFocus);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(placed, 1));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Box(placed, 2));
        Assert.DoesNotContain(item => item.Client == new ClientId(3), placed);
    }

    [TestMethod]
    public void FocusAtTheEndWraps()
    {
        var layout = Tape();
        layout.LayoutMessage(Window(3), "focus l");
        layout.LayoutMessage(Window(2), "focus l");

        var placed = layout.LayoutMessage(Window(1), "focus l");

        Assert.AreEqual(Window(3), layout.LayoutMessageFocus);
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Box(placed, 3));
    }

    [TestMethod]
    public void InhibitedScrollKeepsTheViewport()
    {
        var layout = Tape();
        var before = layout.Visible();

        var held = layout.LayoutMessage(Window(3), "inhibit_scroll");
        var centered = layout.LayoutMessage(Window(3), "center");

        Assert.IsNull(layout.LayoutMessageError);
        Assert.DoesNotContain(item => item.Client == new ClientId(1), before);
        Assert.DoesNotContain(item => item.Client == new ClientId(1), held);
        Assert.AreEqual(Box(before, 2), Box(held, 2));
        Assert.AreEqual(Box(before, 3), Box(centered, 3));

        layout.LayoutMessage(Window(3), "inhibit_scroll 0");
        var moved = layout.LayoutMessage(Window(3), "center");
        Assert.AreNotEqual(Box(before, 3), Box(moved, 3));
    }

    [TestMethod]
    public void InhibitedScrollKeepsTheViewportOnAMove()
    {
        var layout = Tape();
        var before = layout.Visible();

        layout.LayoutMessage(Window(3), "inhibit_scroll");
        var moved = layout.LayoutMessage(Window(3), "move +100");

        Assert.DoesNotContain(item => item.Client == new ClientId(1), moved);
        Assert.AreEqual(Box(before, 2), Box(moved, 2));
    }

    [TestMethod]
    public void AWorkspaceFittedToAnotherMonitorShowsItsColumnsThere()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        layout.Fit(new Rectangle(1920, 0, 1000, 800), new Rectangle(1920, 0, 1000, 800));
        var shown = layout.Visible();

        Assert.AreEqual(new Rectangle(1920, 0, 1000, 800), Box(shown, 1));
    }

    [TestMethod]
    public void ConsumeStacksTheNextColumnAndExpelSplitsItAgain()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.Add(Window(2), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        var stacked = layout.LayoutMessage(Window(1), "consume");

        Assert.AreEqual(new Rectangle(0, 0, 1000, 400), Box(stacked, 1));
        Assert.AreEqual(new Rectangle(0, 400, 1000, 400), Box(stacked, 2));

        var split = layout.LayoutMessage(Window(1), "expel");
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(split, 1));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Box(split, 2));
    }

    [TestMethod]
    public void PromoteMovesTheFocusedClientIntoItsOwnColumn()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.Add(Window(2), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.LayoutMessage(Window(1), "consume");

        var promoted = layout.LayoutMessage(Window(2), "promote");

        Assert.AreEqual(500, Box(promoted, 1).Width);
        Assert.AreEqual(500, Box(promoted, 2).Width);
        Assert.IsTrue(Box(promoted, 2).X >= Box(promoted, 1).X);
    }

    [TestMethod]
    public void SwapColExchangesNeighbors()
    {
        var layout = Tape();

        var placed = layout.LayoutMessage(Window(2), "swapcol r");

        Assert.IsNull(layout.LayoutMessageError);
        Assert.AreEqual(500, Box(placed, 2).Width);
        Assert.AreEqual(0, Box(placed, 3).X);
        Assert.AreEqual(500, Box(placed, 2).X);
    }

    [TestMethod]
    public void UnknownMessageIsRejected()
    {
        var layout = Tape();

        var placed = layout.LayoutMessage(Window(3), "nosuch");

        Assert.IsEmpty(placed);
        Assert.AreEqual("error: no such layoutmsg for scrolling", layout.LayoutMessageError);
    }

    [TestMethod]
    public void FloatingClientKeepsItsBounds()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        var bounds = new Rectangle(40, 50, 200, 120);

        var placed = layout.Add(Window(2), Area, null, true, bounds, WorkspaceId.Initial, monitor: Area);

        Assert.AreEqual(bounds, Box(placed, 2));
        Assert.AreEqual(Area, Assert.ContainsSingle(layout.Placed()).Bounds);
    }

    [TestMethod]
    public void MoveColumnStepsTheViewportAndFitActiveFillsIt()
    {
        var layout = Tape();

        var next = layout.LayoutMessage(Window(2), "move +col");
        Assert.AreEqual(Window(3), layout.LayoutMessageFocus);
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Box(next, 3));

        var previous = layout.LayoutMessage(Window(3), "move -col");
        Assert.AreEqual(Window(2), layout.LayoutMessageFocus);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(previous, 2));

        var filled = layout.LayoutMessage(Window(1), "fit active");
        Assert.AreEqual(new Rectangle(0, 0, 1000, 800), Box(filled, 1));
        Assert.DoesNotContain(item => item.Client == new ClientId(2), filled);
    }

    [TestMethod]
    public void ConsumeOrExpelJoinsALoneColumnWithItsNeighbor()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.Add(Window(2), Area, null, false, default, WorkspaceId.Initial, monitor: Area);

        var joined = layout.LayoutMessage(Window(1), "consume_or_expel next");

        Assert.IsNull(layout.LayoutMessageError);
        Assert.AreEqual(new Rectangle(0, 0, 1000, 400), Box(joined, 2));
        Assert.AreEqual(new Rectangle(0, 400, 1000, 400), Box(joined, 1));
    }

    [TestMethod]
    public void FollowFocusFitsAColumnThatIsAlreadyPartlyInView()
    {
        var layout = Tape();
        layout.LayoutMessage(Window(3), "move +450");
        layout.NoteFocus(Window(1));

        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(layout.Visible(), 1));
    }

    [TestMethod]
    public void ScrollingOptionsUseTheSourceDefaults()
    {
        var options = ScrollingOptions.From(ConfigParser.Parse(""));

        Assert.AreEqual(0.5, options.ColumnWidth);
    }

    [TestMethod]
    public void ScrollingOptionsParseAndClamp()
    {
        var options = ScrollingOptions.From(ConfigParser.Parse("""
            scrolling:column_width = 2
            """));

        Assert.AreEqual(1, options.ColumnWidth);

        var narrow = ScrollingOptions.From(ConfigParser.Parse("scrolling:column_width = 0.01\n"));
        Assert.AreEqual(0.1, narrow.ColumnWidth);
    }

    [TestMethod]
    public void CoreSelectsScrollingAndConcealsTheHiddenColumn()
    {
        var core = Configured("""
            general:layout = scrolling
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            """);
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        var outcome = core.Handle(Appeared(3, Area));

        Assert.AreSequenceEqual([Window(1)], outcome.Hide);
        Assert.DoesNotContain(placement => placement.Client == Client(outcome, 1).Id, outcome.Placements);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), BoundsOf(outcome, 3));

        var focused = core.Handle(new Dispatch("layoutmsg", "focus l"));
        Assert.AreEqual(Window(2), focused.Focus);

        var rejected = core.Handle(new Dispatch("layoutmsg", "nosuch"));
        Assert.AreEqual("error: no such layoutmsg for scrolling", rejected.Error);
    }

    [TestMethod]
    public void ConcealedColumnIsHiddenAndReturnsWhenScrolledBack()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        var third = core.Handle(Appeared(3, Area));

        Assert.AreSequenceEqual([Window(1)], third.Hide);
        Assert.IsEmpty(third.Show);
        Assert.DoesNotContain(placement => placement.Client == new ClientId(1), third.Placements);

        var nearer = core.Handle(new Dispatch("layoutmsg", "focus l"));
        Assert.AreEqual(Window(2), nearer.Focus);
        Assert.IsEmpty(nearer.Hide);
        Assert.IsEmpty(nearer.Show);

        var back = core.Handle(new Dispatch("layoutmsg", "focus l"));

        Assert.AreEqual(Window(1), back.Focus);
        Assert.AreSequenceEqual([Window(1)], back.Show);
        Assert.AreSequenceEqual([Window(3)], back.Hide);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), BoundsOf(back, 1));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), BoundsOf(back, 2));
        Assert.DoesNotContain(placement => placement.Client == new ClientId(3), back.Placements);
    }

    [TestMethod]
    public void APartlyVisibleColumnStaysShownOnItsTapeRectangle()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        core.Handle(Appeared(3, Area));

        var moved = core.Handle(new Dispatch("layoutmsg", "move +100"));

        Assert.AreEqual(new Rectangle(-400, 0, 500, 800), BoundsOf(moved, 1));
        Assert.AreEqual(new Rectangle(100, 0, 500, 800), BoundsOf(moved, 2));
        Assert.AreEqual(new Rectangle(600, 0, 500, 800), BoundsOf(moved, 3));
        Assert.AreSequenceEqual([Window(1)], moved.Show);
        Assert.DoesNotContain(window => window == Window(1), moved.Hide);
    }

    [TestMethod]
    public void ReturningToTheWorkspaceShowsOnlyOverlappingColumns()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        var third = core.Handle(Appeared(3, Area));
        Assert.AreSequenceEqual([Window(1)], third.Hide);
        core.Handle(new Dispatch("workspace", "2"));

        var back = core.Handle(new Dispatch("workspace", "1"));

        Assert.AreEqual(new WorkspaceId("1"), back.State.ActiveWorkspace);
        Assert.AreSequenceEqual([Window(2), Window(3)], back.Show);
        Assert.DoesNotContain(window => window == Window(1), back.Show);
        Assert.AreEqual(new Rectangle(0, 0, 500, 800), BoundsOf(back, 2));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), BoundsOf(back, 3));
        Assert.DoesNotContain(placement => placement.Client == Client(back, 1).Id, back.Placements);
    }

    private static ScrollingLayout Open() => Open(Area);

    private static ScrollingLayout Open(Rectangle area)
    {
        var layout = new ScrollingLayout();
        layout.Apply(LayoutOptions.Unconfigured);
        layout.Fit(area, area);
        return layout;
    }

    private static ScrollingLayout Tape()
    {
        var layout = Open();
        layout.Add(Window(1), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.Add(Window(2), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        layout.Add(Window(3), Area, null, false, default, WorkspaceId.Initial, monitor: Area);
        return layout;
    }

    private static WindowManager Scrolling() => Flat("general:layout = scrolling\n");

    private static WindowId Window(long id) => new(id);

    private static Rectangle Box(IReadOnlyList<Placement> placements, long window)
    {
        var placement = Assert.ContainsSingle(item => item.Client == new ClientId(window), placements);
        return placement.Bounds;
    }

    [TestMethod]
    public void FocusThatScrollsTheStripPlacesTheWindows()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Execute("layoutmsg colresize 0.8");
        var opened = core.Handle(Appeared(2, Area));
        Assert.IsTrue(BoundsOf(opened, 1).X < 0);

        var focused = core.Execute("movefocus l").Effect;

        Assert.AreEqual(0, BoundsOf(focused, 1).X);
        Assert.IsTrue(BoundsOf(focused, 2).X >= 800);
    }

    [TestMethod]
    public void MoveFocusReadsTheWholeDirectionWord()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));

        Assert.IsNull(core.Execute("movefocus leftover").Effect.Focus);
        Assert.AreEqual(Window(1), core.Execute("movefocus left").Effect.Focus);
    }

    [TestMethod]
    public void ScrollingMovesColumnsWithoutResizingThem()
    {
        var core = Scrolling();
        for (var window = 1; window <= 5; window++)
            core.Handle(Appeared(window, Area));
        core.Execute("layoutmsg colresize all 0.333");

        var widths = new HashSet<int>();
        for (var step = 0; step < 4; step++)
        {
            foreach (var placement in core.Execute("movefocus l").Effect.Placements)
                widths.Add(placement.Bounds.Width);
        }

        Assert.ContainsSingle(widths);
    }

    [TestMethod]
    public void ClosingAColumnLeftOfTheViewLeavesTheColumnsInViewInPlace()
    {
        var layout = Tape();

        var placed = layout.Remove(Window(1));

        Assert.AreEqual(new Rectangle(0, 0, 500, 800), Box(placed, 2));
        Assert.AreEqual(new Rectangle(500, 0, 500, 800), Box(placed, 3));
    }

    [TestMethod]
    public void KeyboardResizeGrowsARowAtItsSmallestHeight()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        core.Execute("movefocus l");
        core.Execute("layoutmsg consume");
        core.Execute("focuswindow address:0x2");
        var smallest = BoundsOf(core.Execute("resizeactive 0 -2000").Effect, 2).Height;

        var grown = BoundsOf(core.Execute("resizeactive 0 100").Effect, 2).Height;

        Assert.IsTrue(grown > smallest);
    }

    [TestMethod]
    public void AColumnScrolledOutOfViewThatShowsItselfIsHiddenAgain()
    {
        var core = Scrolling();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        var third = core.Handle(Appeared(3, Area));
        Assert.Contains(Window(1), third.Hide);

        var shown = core.Handle(Appeared(1, Area));

        Assert.Contains(Window(1), shown.Hide);
    }
}
