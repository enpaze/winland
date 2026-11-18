namespace Winland.Domain.Tests;

[TestClass]
public class FrameInsetsTests
{
    [TestMethod]
    public void InvisibleBorderIsRemovedFromTheVisibleFrame()
    {
        var outer = new Rectangle(93, 40, 814, 607);
        var visible = new Rectangle(100, 40, 800, 600);

        var insets = FrameInsets.Between(outer, visible);

        Assert.AreEqual(new FrameInsets(7, 0, 7, 7), insets);
    }

    [TestMethod]
    public void PlacementExpandsTheOuterRectangleSoTheVisibleFrameFillsTheLayout()
    {
        var insets = new FrameInsets(7, 0, 7, 7);

        var outer = insets.OuterFor(new Rectangle(100, 40, 800, 600));

        Assert.AreEqual(new Rectangle(93, 40, 814, 607), outer);
    }

    [TestMethod]
    public void NoInvisibleBorderLeavesTheLayoutRectangleAlone()
    {
        var outer = FrameInsets.None.OuterFor(new Rectangle(10, 20, 300, 200));

        Assert.AreEqual(new Rectangle(10, 20, 300, 200), outer);
    }

    [TestMethod]
    public void ScaledInsetsDifferOnEachEdge()
    {
        var outer = new Rectangle(-1927, 8, 822, 611);
        var visible = new Rectangle(-1920, 8, 800, 600);

        var insets = FrameInsets.Between(outer, visible);

        Assert.AreEqual(new FrameInsets(7, 0, 15, 11), insets);
        Assert.AreEqual(new Rectangle(100, 50, 830, 411), insets.OuterFor(new Rectangle(107, 50, 808, 400)));
    }

    [TestMethod]
    public void AVisibleFrameThatAlreadyMatchesNeedsNoCorrection()
    {
        var target = new Rectangle(100, 40, 800, 600);
        var requested = new Rectangle(93, 40, 814, 607);

        var correction = FrameInsets.Correct(target, requested, requested, target);

        Assert.AreEqual(new FrameInsets.Correction(requested, false), correction);
    }

    [TestMethod]
    public void AShortVisibleFrameIsCorrectedOnceFromTheLandedFrame()
    {
        var target = new Rectangle(100, 200, 800, 600);
        var requested = new Rectangle(100, 200, 800, 600);
        var landedOuter = new Rectangle(100, 200, 800, 600);
        var landedVisible = new Rectangle(107, 200, 786, 593);

        var correction = FrameInsets.Correct(target, requested, landedOuter, landedVisible);

        Assert.AreEqual(new FrameInsets.Correction(new Rectangle(93, 200, 814, 607), false), correction);
    }

    [TestMethod]
    public void AClampedWindowKeepsItsSizeAndIsMovedOnce()
    {
        var target = new Rectangle(10, 10, 400, 300);
        var requested = new Rectangle(3, 10, 414, 307);
        var landedOuter = new Rectangle(50, 80, 500, 400);
        var landedVisible = new Rectangle(57, 80, 486, 393);

        var correction = FrameInsets.Correct(target, requested, landedOuter, landedVisible);

        Assert.AreEqual(new FrameInsets.Correction(new Rectangle(3, 10, 500, 400), true), correction);
    }

    [TestMethod]
    public void AWindowClampedOnOneAxisIsStillCorrectedOnTheOther()
    {
        var target = new Rectangle(100, 200, 800, 600);
        var requested = new Rectangle(100, 200, 800, 600);
        var landedOuter = new Rectangle(100, 200, 500, 600);
        var landedVisible = new Rectangle(107, 200, 486, 593);

        var correction = FrameInsets.Correct(target, requested, landedOuter, landedVisible);

        Assert.AreEqual(new FrameInsets.Correction(new Rectangle(93, 200, 500, 607), true), correction);
    }

    [TestMethod]
    public void InsetsStayCachedUntilTheDpiChanges()
    {
        Assert.IsTrue(FrameInsets.AppliesAt(96, 96));
        Assert.IsFalse(FrameInsets.AppliesAt(96, 144));
        Assert.IsFalse(FrameInsets.AppliesAt(144, 96));
    }
}
