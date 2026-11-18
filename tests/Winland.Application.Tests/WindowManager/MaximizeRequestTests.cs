namespace Winland.Application.Tests;

[TestClass]
public class MaximizeRequestTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    public void AMaximizeRequestFillsTheWorkAreaAndAskingAgainGoesBack()
    {
        var core = Flat();
        Appear(core, 1);
        Appear(core, 2);

        var maximized = core.Handle(new WindowMaximizeRequested(new WindowId(2)));

        Assert.AreEqual(FullscreenMode.Maximized, Client(maximized, 2).Fullscreen);
        Assert.AreEqual(Wide, BoundsOf(maximized, 2));

        var left = core.Handle(new WindowMaximizeRequested(new WindowId(2)));

        Assert.AreEqual(FullscreenMode.None, Client(left, 2).Fullscreen);
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(left, 2));
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(left, 1));
    }

    [TestMethod]
    public void ARequestFromAWindowThatIsNotFocusedMaximizesThatWindow()
    {
        var core = Flat();
        Appear(core, 1);
        Appear(core, 2);

        var maximized = core.Handle(new WindowMaximizeRequested(new WindowId(1)));

        Assert.AreEqual(FullscreenMode.Maximized, Client(maximized, 1).Fullscreen);
        Assert.AreEqual(FullscreenMode.None, Client(maximized, 2).Fullscreen);
    }

    [TestMethod]
    public void AWindowThatShowedMaximizedIsTiledAndItsLaterRequestsAreHonored()
    {
        var core = Flat();
        Appear(core, 1);
        core.Handle(new WindowAppeared(new WindowId(2), Wide) { ShownMaximized = true });

        var ignored = core.Handle(new WindowMaximizeRequested(new WindowId(2)));

        Assert.AreEqual(FullscreenMode.None, Client(ignored, 2).Fullscreen);
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(ignored, 2));

        var maximized = core.Handle(new WindowMaximizeRequested(new WindowId(2)));

        Assert.AreEqual(FullscreenMode.Maximized, Client(maximized, 2).Fullscreen);
    }

    [TestMethod]
    public void ARequestFromAnUnknownWindowChangesNothing()
    {
        var core = Flat();
        Appear(core, 1);

        var outcome = core.Handle(new WindowMaximizeRequested(new WindowId(99)));

        Assert.IsEmpty(outcome.Placements);
        Assert.AreEqual(FullscreenMode.None, Client(outcome, 1).Fullscreen);
    }

    [TestMethod]
    public void AWindowThatRestoredASizeOfItsOwnIsPutBackOnItsTile()
    {
        var core = Flat();
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowMaximizeRequested(new WindowId(2)));
        core.Handle(new WindowMaximizeRequested(new WindowId(2)));

        var drifted = core.Handle(new WindowDrifted(new WindowId(2), new Rectangle(400, 200, 1528, 850)));

        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(drifted, 2));
        Assert.IsEmpty(drifted.Motions);
    }

    [TestMethod]
    public void AWindowAlreadyOnItsTileIsLeftAlone()
    {
        var core = Flat();
        Appear(core, 1);
        Appear(core, 2);

        var drifted = core.Handle(new WindowDrifted(new WindowId(2), new Rectangle(960, 0, 960, 1080)));

        Assert.IsEmpty(drifted.Placements);
    }
}
