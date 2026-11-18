namespace Winland.Application.Tests;

[TestClass]
public class NativeResizeTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    public void DraggingATiledBorderChangesTheSplit()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        var started = core.Handle(new WindowSizeStarted(new WindowId(1), new Point(960, 540)));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(1), new Point(1060, 540)));

        Assert.IsEmpty(started.Placements);
        Assert.AreEqual(new Rectangle(0, 0, 1060, 1080), BoundsOf(ended, 1));
        Assert.AreEqual(new Rectangle(1060, 0, 860, 1080), BoundsOf(ended, 2));
    }

    [TestMethod]
    public void TheNeighbourFollowsTheBorderWhileItIsHeld()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        core.Handle(new WindowSizeStarted(new WindowId(1), new Point(960, 540)));
        var step = core.Handle(new WindowSizing(new WindowId(1), new Point(1060, 540)));

        Assert.IsTrue(step.Live);
        Assert.IsEmpty(step.Motions);
        Assert.DoesNotContain(placement => placement.Client == new ClientId(1), step.Placements);
        Assert.AreEqual(new Rectangle(1060, 0, 860, 1080), BoundsOf(step, 2));

        var ended = core.Handle(new WindowSizeEnded(new WindowId(1), new Point(1100, 540)));

        Assert.IsFalse(ended.Live);
        Assert.AreEqual(new Rectangle(0, 0, 1100, 1080), BoundsOf(ended, 1));
        Assert.AreEqual(new Rectangle(1100, 0, 820, 1080), BoundsOf(ended, 2));
    }

    [TestMethod]
    public void ABorderHeldPastANeighboursMinimumKeepsTheWindowAtItsTile()
    {
        var core = Flat("");
        Appear(core, 1);
        core.Handle(new WindowAppeared(new WindowId(2), Wide) { Traits = WindowTraits.Normal with { MinSize = new Size(600, 0) } });

        core.Handle(new WindowSizeStarted(new WindowId(1), new Point(960, 540), new Rectangle(0, 0, 960, 1080)));
        var within = core.Handle(new WindowSizing(new WindowId(1), new Point(1200, 540), new Rectangle(0, 0, 1200, 1080)));
        var past = core.Handle(new WindowSizing(new WindowId(1), new Point(1500, 540), new Rectangle(0, 0, 1500, 1080)));

        Assert.DoesNotContain(placement => placement.Client == new ClientId(1), within.Placements);
        Assert.AreEqual(new Rectangle(0, 0, 1320, 1080), BoundsOf(past, 1));
        Assert.AreEqual(new Rectangle(1320, 0, 600, 1080), BoundsOf(past, 2));
    }

    [TestMethod]
    public void ASizingStepWithoutASizeChangesNothing()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        var step = core.Handle(new WindowSizing(new WindowId(1), new Point(1060, 540)));

        Assert.IsEmpty(step.Placements);
        Assert.IsFalse(step.Live);
    }

    [TestMethod]
    public void AResizeThatChangesNothingSendsTheClientBackToItsTile()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        core.Handle(new WindowSizeStarted(new WindowId(2), new Point(1500, 1079)));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(2), new Point(1500, 1079)));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(ended, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(ended, 2));
    }

    [TestMethod]
    public void ResizingAMaximizedClientSendsItBackToCoveringTheArea()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowMaximizeRequested(new WindowId(2)));

        core.Handle(new WindowSizeStarted(new WindowId(2), new Point(1919, 540)));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(2), new Point(1500, 540)));

        Assert.AreEqual(Wide, BoundsOf(ended, 2));
        Assert.AreEqual(FullscreenMode.Maximized, Client(ended, 2).Fullscreen);
    }

    [TestMethod]
    public void AFloatingClientKeepsTheSizeItWasGiven()
    {
        var core = Flat("");
        Appear(core, 1);
        core.Handle(new Dispatch("togglefloating"));

        var started = core.Handle(new WindowSizeStarted(new WindowId(1), new Point(900, 700)));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(1), new Point(950, 750)));

        Assert.IsEmpty(started.Placements);
        Assert.IsEmpty(ended.Placements);
        Assert.IsTrue(Client(ended, 1).Floating);
    }

    [TestMethod]
    public void ASizeEventForAnUnknownWindowChangesNothing()
    {
        var core = Flat("");
        Appear(core, 1);

        Assert.IsEmpty(core.Handle(new WindowSizeStarted(new WindowId(99), new Point(10, 10))).Placements);
        Assert.IsEmpty(core.Handle(new WindowSizeEnded(new WindowId(99), new Point(20, 20))).Placements);
    }

    [TestMethod]
    public void TheEndPlacesTheClientWhereTheLayoutHoldsItAfterTheRelease()
    {
        var core = Flat("general:layout = scrolling\nscrolling:column_width = 0.3\n");
        Appear(core, 1);
        Appear(core, 2);

        core.Handle(new WindowSizeStarted(new WindowId(2), new Point(1535, 540)));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(2), new Point(1435, 540)));

        Assert.AreEqual(Client(ended, 2).Bounds, BoundsOf(ended, 2));
    }

    [TestMethod]
    public void ABottomBorderChangesOnlyTheHeight()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);
        Appear(core, 3);
        var tile = new Rectangle(960, 0, 960, 540);

        core.Handle(new WindowSizeStarted(new WindowId(2), new Point(1400, 540), tile));
        var step = core.Handle(new WindowSizing(new WindowId(2), new Point(1200, 640), tile with { Height = 640 }));
        var ended = core.Handle(new WindowSizeEnded(new WindowId(2), new Point(1200, 640), tile with { Height = 640 }));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(step, 1));
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(ended, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 640), BoundsOf(ended, 2));
        Assert.AreEqual(new Rectangle(960, 640, 960, 440), BoundsOf(ended, 3));
    }
}
