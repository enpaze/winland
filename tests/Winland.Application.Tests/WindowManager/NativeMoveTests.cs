namespace Winland.Application.Tests;

[TestClass]
public class NativeMoveTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    public void DroppingATiledClientOnTheLeftHalfPutsItLeftOfTheTileUnderThePointer()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        var started = core.Handle(new WindowMoveStarted(new WindowId(2), new Point(1000, 100)));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), new Point(100, 500)));

        Assert.AreEqual(Wide, BoundsOf(started, 1));
        Assert.DoesNotContain(placement => placement.Client == Client(started, 2).Id, started.Placements);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(dropped, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(dropped, 1));
        Assert.IsFalse(Client(dropped, 2).Floating);
    }

    [TestMethod]
    public void DroppingWhereItWasSendsTheClientBackToItsPlace()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        core.Handle(new WindowMoveStarted(new WindowId(2), new Point(1000, 100)));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), new Point(1010, 120)));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(dropped, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(dropped, 2));
    }

    [TestMethod]
    public void AFloatingClientStaysWhereItIsDropped()
    {
        var core = Flat("");
        Appear(core, 1);
        core.Handle(new Dispatch("togglefloating"));

        var started = core.Handle(new WindowMoveStarted(new WindowId(1), new Point(700, 400)));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(1), new Point(900, 500)));

        Assert.IsEmpty(started.Placements);
        Assert.IsEmpty(dropped.Placements);
        Assert.IsTrue(Client(dropped, 1).Floating);
    }

    [TestMethod]
    public void AFloatingClientKeepsTheFrameTheSystemMovedOrSizedItTo()
    {
        var core = Flat("");
        Appear(core, 1);
        core.Handle(new Dispatch("togglefloating"));
        var window = new WindowId(1);

        core.Handle(new WindowMoveStarted(window, new Point(700, 400)));
        var moved = core.Handle(new WindowMoveEnded(window, new Point(900, 500), new Rectangle(300, 200, 640, 400)));

        Assert.IsEmpty(moved.Placements);
        Assert.AreEqual(new Rectangle(300, 200, 640, 400), Client(moved, 1).Bounds);

        core.Handle(new WindowSizeStarted(window, new Point(940, 400)));
        var sizing = core.Handle(new WindowSizing(window, new Point(1040, 400), new Rectangle(300, 200, 740, 400)));
        var sized = core.Handle(new WindowSizeEnded(window, new Point(1100, 400), new Rectangle(300, 200, 800, 400)));

        Assert.IsEmpty(sizing.Placements);
        Assert.AreEqual(new Rectangle(300, 200, 740, 400), Client(sizing, 1).Bounds);
        Assert.AreEqual(new Rectangle(300, 200, 800, 400), Client(sized, 1).Bounds);
    }

    [TestMethod]
    public void MasterDropsTheClientOntoTheMasterWhenThePointerEndsOverIt()
    {
        var core = Flat("general:layout = master\n");
        Appear(core, 1);
        Appear(core, 2);

        core.Handle(new WindowMoveStarted(new WindowId(2), new Point(1500, 500)));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), new Point(200, 500)));

        Assert.IsTrue(BoundsOf(dropped, 2).X < BoundsOf(dropped, 1).X);
        Assert.IsFalse(Client(dropped, 2).Floating);
    }

    [TestMethod]
    public void ScrollingTilesTheClientAgainAfterTheDrop()
    {
        var core = Flat("general:layout = scrolling\n");
        Appear(core, 1);
        Appear(core, 2);

        core.Handle(new WindowMoveStarted(new WindowId(2), new Point(1500, 500)));
        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), new Point(300, 500)));

        Assert.IsFalse(Client(dropped, 2).Floating);
        Assert.Contains(placement => placement.Client == Client(dropped, 2).Id, dropped.Placements);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("0")]
    public void MovingAMaximizedOrFullscreenClientLeavesThatModeAndDropsItBackIn(string mode)
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new Dispatch("focuswindow", "address:0x2"));
        core.Handle(new Dispatch("fullscreen", mode));

        var started = core.Handle(new WindowMoveStarted(new WindowId(2), new Point(900, 20)));

        Assert.AreEqual(FullscreenMode.None, Client(started, 2).Fullscreen);
        Assert.AreEqual(Wide, BoundsOf(started, 1));
        Assert.DoesNotContain(placement => placement.Client == Client(started, 2).Id, started.Placements);

        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), new Point(100, 500)));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(dropped, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(dropped, 1));
        Assert.AreEqual(FullscreenMode.None, Client(dropped, 2).Fullscreen);
    }

    [TestMethod]
    public void AMoveOfAnUnknownWindowChangesNothing()
    {
        var core = Flat("");
        Appear(core, 1);

        var started = core.Handle(new WindowMoveStarted(new WindowId(99), new Point(10, 10)));
        var ended = core.Handle(new WindowMoveEnded(new WindowId(99), new Point(20, 20)));

        Assert.IsEmpty(started.Placements);
        Assert.IsEmpty(ended.Placements);
    }

    [TestMethod]
    public void AClientThatClosesWhileTheSystemMovesItDoesNotBlockTheNextMove()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        Appear(core, 3);
        core.Handle(new WindowMoveStarted(new WindowId(3), new Point(1500, 100)));
        core.Handle(new WindowClosed(new WindowId(3)));

        var started = core.Handle(new WindowMoveStarted(new WindowId(2), new Point(1500, 100)));

        Assert.AreEqual(Wide, BoundsOf(started, 1));
    }

    [TestMethod]
    public void AClientThatClosesWhileTheSystemResizesItDoesNotBlockTheNextResize()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowSizeStarted(new WindowId(1), new Point(960, 500)));
        core.Handle(new WindowClosed(new WindowId(1)));
        Appear(core, 3);

        core.Handle(new WindowSizeStarted(new WindowId(2), new Point(960, 500)));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(2), new Point(900, 500)));

        Assert.IsNotEmpty(ended.Placements);
    }

    [TestMethod]
    public void AClientThatClosesDuringAMouseDragEndsTheDrag()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new PointerGesture("movewindow", new Point(1500, 100), PointerDragPhase.Begin, "mouse:left"));
        core.Handle(new WindowClosed(new WindowId(2)));

        var moved = core.Handle(new PointerGesture("movewindow", new Point(1400, 200), PointerDragPhase.Move, "mouse:left"));
        var started = core.Handle(new WindowMoveStarted(new WindowId(1), new Point(500, 100)));

        Assert.IsEmpty(moved.Placements);
        Assert.AreEqual(new WindowId(1), Assert.ContainsSingle(started.State.Clients).Window);
        Assert.IsFalse(Assert.ContainsSingle(core.Handle(new WindowMoveEnded(new WindowId(1), new Point(500, 100))).State.Clients).Floating);
    }
}
