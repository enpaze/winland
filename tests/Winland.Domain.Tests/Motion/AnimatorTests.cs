namespace Winland.Domain.Tests;

[TestClass]
public class AnimatorTests
{
    private static readonly WindowId Window = new(1);
    private static readonly Timing Linear = new(Bezier.Linear, 100);

    [TestMethod]
    public void AMotionFollowsItsCurveAndLandsOnItsGoal()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(100, 0, 200, 100)) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);

        var halfway = Assert.ContainsSingle(animator.Step(50));
        var landed = Assert.ContainsSingle(animator.Step(100));

        Assert.AreEqual(new Rectangle(50, 0, 150, 100), halfway.Bounds);
        Assert.IsFalse(halfway.Done);
        Assert.AreEqual(new Rectangle(100, 0, 200, 100), landed.Bounds);
        Assert.IsTrue(landed.Done);
        Assert.AreEqual(MotionEnd.None, landed.End);
        Assert.AreEqual(new Rectangle(100, 0, 200, 100), landed.Rest);
        Assert.AreEqual(0, animator.Count);
        Assert.IsEmpty(animator.Step(150));
    }

    [TestMethod]
    public void AFrameCarriesTheRectangleTheWindowRestsOn()
    {
        var animator = new Animator();
        var rest = new Rectangle(0, 0, 100, 100);
        animator.Start(new Motion(Window, new Rectangle(50, 50, 0, 0)) { From = rest, Move = Linear, End = MotionEnd.Close, Rest = rest }, now: 0);

        var frame = Assert.ContainsSingle(animator.Step(50));

        Assert.AreEqual(rest, frame.Content);
    }

    [TestMethod]
    public void AFrameCarriesTheGoalOfItsMoveAndAFadeAloneHasNone()
    {
        var animator = new Animator();
        var goal = new Rectangle(100, 0, 200, 100);
        var other = new WindowId(2);
        animator.Start(new Motion(Window, goal) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);
        animator.Start(new Motion(other, goal) { FadeTo = 0, Fade = Linear }, now: 0);

        var frames = animator.Step(50);

        Assert.AreEqual(goal, frames.Single(frame => frame.Window == Window).Goal);
        Assert.IsNull(frames.Single(frame => frame.Window == other).Goal);
    }

    [TestMethod]
    public void AFadeWithoutAMoveLetsARunningMoveToTheSameGoalContinue()
    {
        var animator = new Animator();
        var goal = new Rectangle(100, 0, 100, 100);
        animator.Start(new Motion(Window, goal) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);
        animator.Start(new Motion(Window, goal) { FadeTo = 0, Fade = Linear, End = MotionEnd.Hide, Rest = goal }, now: 50);

        Assert.AreEqual(new Rectangle(75, 0, 100, 100), Assert.ContainsSingle(animator.Step(75)).Bounds);
    }

    [TestMethod]
    public void AMotionWithoutAMoveToAnotherGoalDropsTheRunningMove()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(100, 0, 100, 100)) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);
        animator.Start(new Motion(Window, new Rectangle(300, 0, 100, 100)) { FadeTo = 0, Fade = Linear }, now: 50);

        Assert.IsNull(Assert.ContainsSingle(animator.Step(75)).Bounds);
    }

    [TestMethod]
    public void ACurvedMotionUsesTheCurveBetweenItsEnds()
    {
        var animator = new Animator();
        var curve = new Timing(new Bezier(0.25, 0.1, 0.25, 1), 1000);
        animator.Start(new Motion(Window, new Rectangle(1000, 0, 10, 10)) { From = new Rectangle(0, 0, 10, 10), Move = curve }, now: 0);

        var frame = Assert.ContainsSingle(animator.Step(500));

        Assert.AreEqual(802, frame.Bounds!.Value.X);
    }

    [TestMethod]
    public void AnOvershootingMotionStillLandsExactly()
    {
        var animator = new Animator();
        var overshoot = new Timing(new Bezier(0.5, 0.9, 0.1, 1.5), 100);
        animator.Start(new Motion(Window, new Rectangle(100, 0, 10, 10)) { From = new Rectangle(0, 0, 10, 10), Move = overshoot }, now: 0);

        var passing = Enumerable.Range(1, 9).Select(i => animator.Peek(Window, i * 10)!.Bounds!.Value.X).Max();
        var landed = Assert.ContainsSingle(animator.Step(100));

        Assert.IsTrue(passing > 100);
        Assert.AreEqual(new Rectangle(100, 0, 10, 10), landed.Bounds);
    }

    [TestMethod]
    public void AnInterruptedMotionContinuesFromWhereItIsAndLandsOnTheNewRectangle()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(100, 0, 100, 100)) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);
        Assert.AreEqual(new Rectangle(50, 0, 100, 100), Assert.ContainsSingle(animator.Step(50)).Bounds);

        animator.Start(new Motion(Window, new Rectangle(0, 200, 100, 100)) { Move = Linear }, now: 50);

        Assert.AreEqual(new Rectangle(50, 0, 100, 100), animator.Peek(Window, 50)!.Bounds);
        Assert.AreEqual(new Rectangle(25, 100, 100, 100), Assert.ContainsSingle(animator.Step(100)).Bounds);
        var landed = Assert.ContainsSingle(animator.Step(150));
        Assert.AreEqual(new Rectangle(0, 200, 100, 100), landed.Bounds);
        Assert.IsTrue(landed.Done);
        Assert.AreEqual(new Rectangle(0, 200, 100, 100), landed.Rest);
    }

    [TestMethod]
    public void APlacementOnTheSameGoalLeavesTheMotionRunning()
    {
        var animator = new Animator();
        var goal = new Rectangle(100, 0, 100, 100);
        animator.Start(new Motion(Window, goal) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);

        Assert.IsTrue(animator.Settle(Window, goal));
        Assert.AreEqual(new Rectangle(50, 0, 100, 100), Assert.ContainsSingle(animator.Step(50)).Bounds);
    }

    [TestMethod]
    public void APlacementOnAnotherRectangleStopsTheMovement()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(100, 0, 100, 100)) { From = new Rectangle(0, 0, 100, 100), Move = Linear }, now: 0);

        Assert.IsFalse(animator.Settle(Window, new Rectangle(300, 0, 100, 100)));

        var frame = Assert.ContainsSingle(animator.Step(50));
        Assert.IsNull(frame.Bounds);
        Assert.IsTrue(frame.Done);
        Assert.AreEqual(new Rectangle(300, 0, 100, 100), frame.Rest);
    }

    [TestMethod]
    public void ALeavingWindowEndsHiddenOnItsLayoutRectangle()
    {
        var animator = new Animator();
        var rest = new Rectangle(0, 0, 100, 100);
        animator.Start(
            new Motion(Window, rest with { X = -1920 })
            {
                From = rest,
                Move = Linear,
                FadeTo = 0,
                Fade = Linear,
                AlphaTo = 200,
                End = MotionEnd.Hide,
                Rest = rest,
            },
            now: 0);

        Assert.IsTrue(animator.Settle(Window, new Rectangle(0, 0, 50, 50)));
        var halfway = Assert.ContainsSingle(animator.Step(50));
        var landed = Assert.ContainsSingle(animator.Step(100));

        Assert.AreEqual(new Rectangle(-960, 0, 100, 100), halfway.Bounds);
        Assert.AreEqual((byte)100, halfway.Alpha);
        Assert.IsTrue(landed.Done);
        Assert.AreEqual(MotionEnd.Hide, landed.End);
        Assert.AreEqual((byte)0, landed.Alpha);
        Assert.AreEqual(new Rectangle(0, 0, 50, 50), landed.Rest);
        Assert.AreEqual((byte)200, landed.RestAlpha);
    }

    [TestMethod]
    public void AFadeMultipliesTheNewestAlpha()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(0, 0, 10, 10)) { FadeFrom = 0, Fade = Linear, AlphaTo = 200 }, now: 0);

        Assert.AreEqual((byte)100, Assert.ContainsSingle(animator.Step(50)).Alpha);
        Assert.IsTrue(animator.Paint(Window, 100, default, now: 50).Alpha);
        Assert.AreEqual((byte)60, Assert.ContainsSingle(animator.Step(60)).Alpha);
        var landed = Assert.ContainsSingle(animator.Step(100));

        Assert.AreEqual((byte)100, landed.Alpha);
        Assert.AreEqual((byte)100, landed.RestAlpha);
        Assert.IsFalse(animator.Paint(Window, 50, default, now: 120).Alpha);
    }

    [TestMethod]
    public void AnAlphaChangeMovesTowardANewerAlpha()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(0, 0, 10, 10)) { AlphaFrom = 0, AlphaTo = 200, Alpha = Linear }, now: 0);
        Assert.AreEqual((byte)100, Assert.ContainsSingle(animator.Step(50)).Alpha);

        animator.Paint(Window, 0, default, now: 50);

        Assert.AreEqual((byte)50, Assert.ContainsSingle(animator.Step(100)).Alpha);
        Assert.AreEqual((byte)0, Assert.ContainsSingle(animator.Step(150)).Alpha);
    }

    [TestMethod]
    public void ABorderChangeLandsOnItsColor()
    {
        var animator = new Animator();
        var black = new BorderColor(0xFF, 0, 0, 0);
        var white = new BorderColor(0xFF, 0xFF, 0xFF, 0xFF);
        animator.Start(new Motion(Window, new Rectangle(0, 0, 10, 10)) { BorderFrom = black, BorderTo = white, Border = Linear }, now: 0);

        var middle = Assert.ContainsSingle(animator.Step(50)).Border!.Value;
        var landed = Assert.ContainsSingle(animator.Step(100));

        Assert.IsInRange(1, 254, middle.R);
        Assert.IsInRange(0, 1, Math.Abs(middle.R - middle.G));
        Assert.AreEqual(white, landed.Border);
        Assert.IsTrue(animator.Paint(Window, 255, white, now: 0).Border == false);
    }

    [TestMethod]
    public void LandingPutsEveryMotionOnItsGoalAtOnce()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(100, 0, 100, 100)) { From = new Rectangle(0, 0, 100, 100), Move = Linear, FadeFrom = 0, Fade = Linear }, now: 0);
        animator.Start(new Motion(new WindowId(2), new Rectangle(-5, 0, 1, 1)) { Move = Linear, End = MotionEnd.Close, Rest = new Rectangle(0, 0, 1, 1) }, now: 0);

        var frames = animator.Land();

        var first = Assert.ContainsSingle(frame => frame.Window == Window, frames);
        Assert.AreEqual(new Rectangle(100, 0, 100, 100), first.Bounds);
        Assert.AreEqual((byte)255, first.Alpha);
        Assert.IsTrue(first.Done);
        var closing = Assert.ContainsSingle(frame => frame.Window == new WindowId(2), frames);
        Assert.AreEqual(MotionEnd.Close, closing.End);
        Assert.AreEqual(new Rectangle(0, 0, 1, 1), closing.Rest);
        Assert.AreEqual(0, animator.Count);
    }

    [TestMethod]
    public void AMotionWithoutTimingsLandsOnTheNextStep()
    {
        var animator = new Animator();
        animator.Start(new Motion(Window, new Rectangle(0, 0, 10, 10)) { End = MotionEnd.Hide }, now: 0);

        var frame = Assert.ContainsSingle(animator.Step(0));

        Assert.IsTrue(frame.Done);
        Assert.AreEqual(MotionEnd.Hide, frame.End);
        Assert.IsNull(frame.Bounds);
    }
}
