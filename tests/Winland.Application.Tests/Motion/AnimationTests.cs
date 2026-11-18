namespace Winland.Application.Tests;

[TestClass]
public class AnimationTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly Rectangle Left = new(0, 0, 960, 1080);
    private static readonly Rectangle Right = new(960, 0, 960, 1080);
    private static readonly Timing WindowTiming = new(Bezier.Default, 300);
    private static readonly Timing TransitionTiming = new(Bezier.Default, 800);

    [TestMethod]
    public void AnOpenedWindowPopsInAndFadesIn()
    {
        var core = new WindowManager();

        var outcome = Appear(core, 1);

        Assert.AreEqual(Area, BoundsOf(outcome, 1));
        var motion = Assert.ContainsSingle(outcome.Motions);
        Assert.AreEqual(new WindowId(1), motion.Window);
        Assert.AreEqual(Area, motion.To);
        Assert.AreEqual(new Rectangle(957, 537, 5, 5), motion.From);
        Assert.IsTrue(motion.Warp);
        Assert.AreEqual(WindowTiming, motion.Move);
        Assert.AreEqual(0, motion.FadeFrom);
        Assert.AreEqual(1, motion.FadeTo);
        Assert.AreEqual(TransitionTiming, motion.Fade);
        Assert.AreEqual(MotionEnd.None, motion.End);
        Assert.AreEqual(Area, motion.Rest);
    }

    [TestMethod]
    public void AWindowThatMakesRoomMovesFromItsOldRectangle()
    {
        var core = new WindowManager();
        Appear(core, 1);

        var outcome = Appear(core, 2);

        Assert.AreEqual(Left, BoundsOf(outcome, 1));
        var moved = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), outcome.Motions);
        Assert.AreEqual(Left, moved.To);
        Assert.AreEqual(Area, moved.From);
        Assert.IsFalse(moved.Warp);
        Assert.AreEqual(WindowTiming, moved.Move);
        Assert.IsNull(moved.Fade);
        Assert.Contains(motion => motion.Window == new WindowId(2) && motion.FadeFrom == 0, outcome.Motions);
    }

    [TestMethod]
    public void ResizingADwindleTileAnimatesItAndItsNeighbour()
    {
        var core = Load("");
        Appear(core, 1);
        Appear(core, 2);

        var resized = core.Handle(new Dispatch("resize", "-100 0"));

        Assert.AreEqual(2, resized.Motions.Count(motion => !motion.Warp && motion.Move is not null));
    }

    [TestMethod]
    public void ASwitchToAHigherWorkspaceSlidesTheOldOneOutToTheLeft()
    {
        var core = Load("""
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
            """);
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);

        var outcome = Tap(core, "2");

        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], outcome.Hide);
        var first = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), outcome.Motions);
        Assert.AreEqual(Left with { X = -1920 }, first.To);
        Assert.AreEqual(Left, first.Rest);
        Assert.AreEqual(MotionEnd.Hide, first.End);
        Assert.AreEqual(TransitionTiming, first.Move);
        Assert.IsNull(first.Fade);
        var second = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), outcome.Motions);
        Assert.AreEqual(Right with { X = 960 - 1920 }, second.To);
        Assert.AreEqual(Right, second.Rest);
    }

    [TestMethod]
    public void AWindowSlidingOutKeepsItsBorderUntilItHides()
    {
        var core = Load("""
            general:border_size = 2
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
            """);
        Appear(core, 1);
        HoldSuper(core);

        var away = Tap(core, "2");

        Assert.AreEqual(MotionEnd.Hide, Assert.ContainsSingle(away.Motions).End);
        Assert.IsNotNull(Assert.ContainsSingle(away.Decorations).Ring);
    }

    [TestMethod]
    public void AWindowHiddenWithoutMotionLosesItsBorderAtOnce()
    {
        var core = Load("""
            general:border_size = 2
            animations:enabled = false
            bind = SUPER, 2, workspace, 2
            """);
        Appear(core, 1);
        HoldSuper(core);

        Assert.IsNull(Assert.ContainsSingle(Tap(core, "2").Decorations).Ring);
    }

    [TestMethod]
    public void AClientGoingIntoFullscreenMovesFromItsTileToTheMonitor()
    {
        var core = Load("");
        Appear(core, 1);
        Appear(core, 2);

        var into = core.Handle(new Dispatch("fullscreen"));
        var grown = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), into.Motions);
        Assert.AreEqual(Right, grown.From);
        Assert.AreEqual(Area, grown.To);
        Assert.AreEqual(WindowTiming, grown.Move);
    }

    [TestMethod]
    public void AClientLeavingFullscreenMovesBackToItsTile()
    {
        var core = Load("");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new Dispatch("fullscreen"));

        var back = core.Handle(new Dispatch("fullscreen"));

        var shrunk = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), back.Motions);
        Assert.AreEqual(Area, shrunk.From);
        Assert.AreEqual(Right, shrunk.To);
    }

    [TestMethod]
    public void ASwitchBackSlidesInFromTheLeftAndKeepsTheLayoutRectangles()
    {
        var core = Load("""
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
            """);
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        Tap(core, "2");
        Appear(core, 3);

        var back = Tap(core, "1");

        Assert.AreEqual(Left, BoundsOf(back, 1));
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], back.Show);
        var entering = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), back.Motions);
        Assert.AreEqual(Left with { X = -1920 }, entering.From);
        Assert.IsFalse(entering.Warp);
        Assert.AreEqual(Left, entering.To);
        Assert.AreEqual(MotionEnd.None, entering.End);
        var leaving = Assert.ContainsSingle(motion => motion.Window == new WindowId(3), back.Motions);
        Assert.AreEqual(Area with { X = 1920 }, leaving.To);
        Assert.AreEqual(Area, leaving.Rest);
        Assert.AreEqual(MotionEnd.Hide, leaving.End);
    }

    [TestMethod]
    public void AnimationsOffPlansNoMotion()
    {
        var core = Load("""
            general:border_size = 2
            animations:enabled = false
            bind = SUPER, 2, workspace, 2
            """);
        Assert.IsEmpty(Appear(core, 1).Motions);
        Assert.IsEmpty(Appear(core, 2).Motions);
        HoldSuper(core);

        var outcome = Tap(core, "2");

        Assert.AreEqual(2, outcome.Hide.Count);
        Assert.IsEmpty(outcome.Motions);
        Assert.IsEmpty(core.Handle(new Dispatch("close")).Motions);
    }

    [TestMethod]
    public void ReducedMotionPlansNoMotion()
    {
        var core = new WindowManager { ReducedMotion = () => true };

        Assert.IsEmpty(Appear(core, 1).Motions);
        Assert.IsEmpty(Appear(core, 2).Motions);
    }

    [TestMethod]
    public void ReloadingWithAnimationsOffStopsThem()
    {
        var core = new WindowManager();
        Appear(core, 1);

        core.Handle(new Configure(ConfigParser.Parse("animations:enabled = no")));

        Assert.IsEmpty(Appear(core, 2).Motions);
    }

    [TestMethod]
    public void FocusMovesTheAlphaAndBorderOfBothClients()
    {
        var core = Load("""
            decoration:inactive_opacity = 0.5
            """);
        Appear(core, 1);
        Appear(core, 2);

        var outcome = core.Handle(new Dispatch("focus", "l"));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        var focused = Assert.ContainsSingle(motion => motion.Window == new WindowId(1), outcome.Motions);
        var left = Assert.ContainsSingle(motion => motion.Window == new WindowId(2), outcome.Motions);
        Assert.AreEqual((byte)255, focused.AlphaTo);
        Assert.IsTrue(focused.AlphaFrom < 255);
        Assert.AreEqual(TransitionTiming, focused.Alpha);
        Assert.AreEqual(left.AlphaFrom, focused.AlphaTo);
        Assert.AreEqual(focused.AlphaFrom, left.AlphaTo);
        Assert.AreEqual(DecorationOptions.Documented.InactiveBorder, focused.BorderFrom);
        Assert.AreEqual(DecorationOptions.Documented.ActiveBorder, focused.BorderTo);
        Assert.AreEqual(TransitionTiming, focused.Border);
        Assert.IsNull(focused.Move);
        Assert.AreEqual(Left, focused.To);
    }

    [TestMethod]
    public void ADraggedWindowMovesAtOnce()
    {
        var core = Load("bind = SUPER, mouse:left, move");
        Appear(core, 1);
        var floated = core.Handle(new Dispatch("float"));
        var bounds = Assert.ContainsSingle(floated.Placements).Bounds;
        var grab = new Point(bounds.X + 10, bounds.Y + 20);
        HoldSuper(core);
        core.Handle(new PointerButtonPressed("mouse:left") { Position = grab });

        var moved = core.Handle(new PointerMoved(new Point(grab.X + 50, grab.Y + 30)));

        Assert.IsNotEmpty(moved.Placements);
        Assert.DoesNotContain(motion => motion.Move is not null, moved.Motions);
    }

    [TestMethod]
    public void TheTileThatMakesRoomForALiftedTileAnimatesWhileTheLiftedOneFollowsThePointer()
    {
        var core = Load("bind = SUPER, mouse:left, move");
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(1400, 500) });

        var lifted = core.Handle(new PointerMoved(new Point(1450, 520)));

        Assert.IsTrue(lifted.Live);
        var grown = Assert.ContainsSingle(motion => motion.Move is not null, lifted.Motions);
        Assert.AreEqual(new WindowId(1), grown.Window);
        Assert.AreEqual(Left, grown.From);
        Assert.AreEqual(Area, grown.To);
        Assert.DoesNotContain(motion => motion.Move is not null, core.Handle(new PointerMoved(new Point(1500, 540))).Motions);
    }

    [TestMethod]
    public void AMouseResizeOfATileMovesItsNeighborAtOnce()
    {
        var core = Load("bind = SUPER, mouse:right, resize");
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        core.Handle(new PointerButtonPressed("mouse:right") { Position = new Point(1000, 500) });

        var resized = core.Handle(new PointerMoved(new Point(1100, 500)));

        Assert.IsNotEmpty(resized.Placements);
        Assert.DoesNotContain(motion => motion.Move is not null, resized.Motions);
    }

    [TestMethod]
    public void AnInterruptedSwitchLandsOnTheFinalRectangles()
    {
        var core = Load("""
            bind = SUPER, 1, workspace, 1
            bind = SUPER, 2, workspace, 2
            """);
        Appear(core, 1);
        Appear(core, 2);
        HoldSuper(core);
        var animator = new Animator();
        Play(animator, Tap(core, "2"), now: 0);
        animator.Step(400);

        Play(animator, Tap(core, "1"), now: 400);
        var landed = animator.Step(400 + TransitionTiming.Milliseconds);

        foreach (var frame in landed)
{
    Assert.IsTrue(frame.Done);
}
        Assert.AreEqual(Left, Assert.ContainsSingle(frame => frame.Window == new WindowId(1), landed).Bounds);
        Assert.AreEqual(Right, Assert.ContainsSingle(frame => frame.Window == new WindowId(2), landed).Bounds);
        foreach (var frame in landed)
{
    Assert.AreEqual(MotionEnd.None, frame.End);
}
        Assert.AreEqual(0, animator.Count);
    }

    private static void Play(Animator animator, Outcome outcome, double now)
    {
        foreach (var motion in outcome.Motions)
            animator.Start(motion, now);
    }

    [TestMethod]
    public void ADroppedWindowSlidesIntoItsTileFromWhereTheSystemLeftIt()
    {
        var core = Load("""
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            """);
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowMoveStarted(new WindowId(2), new Point(1000, 100)));
        var left = new Rectangle(40, 300, 960, 1080);

        var dropped = core.Handle(new WindowMoveEnded(new WindowId(2), new Point(100, 500), left));

        var motion = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), dropped.Motions);
        Assert.AreEqual(left, motion.From);
        Assert.AreEqual(Left, motion.To);
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
