namespace Winland.Application.Tests;

[TestClass]
public class FullscreenFocusTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    [DataRow("1")]
    [DataRow("0")]
    public void ThePointerOverACoveredTileDoesNotFocusIt(string mode)
    {
        var core = Desk();
        Move(core, new Point(1500, 500), 1_000);
        core.Handle(new Dispatch("fullscreen", mode));
        Assert.AreEqual("\"address\":\"0x2\"", Address(core));

        var over = Move(core, new Point(300, 500), 1_100);

        Assert.IsNull(over.Focus);
        Assert.DoesNotContain(item => item is FocusChanged, over.Events);
        Assert.AreEqual("\"address\":\"0x2\"", Address(core));
    }

    [TestMethod]
    public void AMaximizedClientTakesTheFocusWhenThePointerIsOverIt()
    {
        var core = Desk();
        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        core.Handle(new WindowMaximizeRequested(new WindowId(2)));
        Assert.AreEqual("\"address\":\"0x1\"", Address(core));

        Move(core, new Point(300, 500), 1_000);
        var over = Move(core, new Point(400, 520), 1_100);

        Assert.AreEqual(new WindowId(2), over.Focus);
    }

    [TestMethod]
    public void AClientThatCoversItsMonitorByItselfOwnsThePointer()
    {
        var core = Desk();
        Move(core, new Point(1500, 500), 1_000);

        var covered = core.Handle(new WindowFullscreenRequested(new WindowId(2), true));
        var over = Move(core, new Point(300, 500), 1_100);

        Assert.AreEqual(FullscreenMode.Fullscreen, Client(covered, 2).Fullscreen);
        Assert.IsNull(over.Focus);
        Assert.AreEqual("\"address\":\"0x2\"", Address(core));
    }

    [TestMethod]
    public void AClientThatLeavesItsOwnFullscreenGoesBackToItsTile()
    {
        var core = Desk();
        Move(core, new Point(1500, 500), 1_000);
        core.Handle(new WindowFullscreenRequested(new WindowId(2), true));

        var left = core.Handle(new WindowFullscreenRequested(new WindowId(2), false));

        Assert.AreEqual(FullscreenMode.None, Client(left, 2).Fullscreen);
        Assert.Contains(placement => placement.Client == Client(left, 2).Id && placement.Bounds.Width < Area.Width, left.Placements);
        Assert.AreEqual(new WindowId(1), Move(core, new Point(300, 500), 1_100).Focus);
    }

    [TestMethod]
    public void AClientThatGoesFullscreenOrLeavesItByItselfIsPlacedWithoutAMotion()
    {
        var core = new WindowManager(ConfigParser.Parse("general:border_size = 0\nanimation = global, 1, 5, default\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var entered = core.Handle(new WindowFullscreenRequested(new WindowId(2), true));
        var left = core.Handle(new WindowFullscreenRequested(new WindowId(2), false));

        Assert.Contains(placement => placement.Client == Client(entered, 2).Id, entered.Placements);
        Assert.Contains(placement => placement.Client == Client(left, 2).Id, left.Placements);
        Assert.DoesNotContain(motion => motion.Window == new WindowId(2) && motion.Move is not null, entered.Motions);
        Assert.DoesNotContain(motion => motion.Window == new WindowId(2) && motion.Move is not null, left.Motions);
    }

    [TestMethod]
    public void AWindowThatLeavesAFullscreenItNeverHadChangesNothing()
    {
        var core = Desk();
        core.Handle(new Dispatch("fullscreen", "1"));

        var left = core.Handle(new WindowFullscreenRequested(new WindowId(2), false));

        Assert.AreEqual(FullscreenMode.Maximized, Client(left, 2).Fullscreen);
        Assert.IsEmpty(left.Placements);
    }

    [TestMethod]
    public void WithNothingCoveringTheTilesStillFollowThePointer()
    {
        var core = Desk();
        Move(core, new Point(1500, 500), 1_000);

        var over = Move(core, new Point(300, 500), 1_100);

        Assert.AreEqual(new WindowId(1), over.Focus);
    }

    private static WindowManager Desk()
    {
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        return core;
    }

    private static Outcome Move(WindowManager core, Point point, long time) =>
        core.Handle(new PointerMoved(point) { TimeMs = time });

    private static string Address(WindowManager core)
    {
        var text = core.Execute("j/get window").Text;
        var at = text.IndexOf("\"address\":", StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, text);
        return text[at..(at + "\"address\":\"0x0\"".Length)];
    }
}
