namespace Winland.Application.Tests;

[TestClass]
public class SizeLimitTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly Size SmallMax = new(400, 300);

    [TestMethod]
    public void SmallMaximumFloatsInsteadOfLeavingAnEmptyTile()
    {
        var core = new WindowManager();
        var bounds = new Rectangle(40, 50, 380, 260);

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { MaxSize = SmallMax },
            Bounds = bounds,
        });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.AreEqual(new Rectangle(760, 390, 400, 300), Assert.ContainsSingle(outcome.Placements).Bounds);
    }

    [TestMethod]
    public void SmallMaximumFloatsAndLeavesTheExistingTileFull()
    {
        var core = new WindowManager();
        var bounds = new Rectangle(40, 50, 380, 260);
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var floated = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MaxSize = SmallMax },
            Bounds = bounds,
        });

        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), floated.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.AreEqual(new Rectangle(760, 390, 400, 300), Assert.ContainsSingle(placement => placement.Client == client.Id, floated.Placements).Bounds);
        Assert.Contains(candidate => candidate.Window == new WindowId(1) && !candidate.Floating, floated.State.Clients);

        var split = core.Handle(new WindowAppeared(new WindowId(3), Area));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(split, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(split, 3));
        Assert.IsTrue(Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), split.State.Clients).Floating);
    }

    [TestMethod]
    public void MaximumBelowEitherSideOfTheTargetTileFloats()
    {
        var core = new WindowManager();
        var bounds = new Rectangle(10, 20, 200, 150);
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var floated = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MaxSize = new Size(1000, 4000) },
            Bounds = bounds,
        });

        Assert.IsTrue(Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), floated.State.Clients).Floating);
        Assert.AreEqual(new Rectangle(480, 0, 960, 1080), BoundsOf(floated, 2));
    }

    [TestMethod]
    public void MaximumThatCoversTheTargetTileStaysTiled()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MaxSize = new Size(1920, 1080) },
        });

        Assert.IsFalse(Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), outcome.State.Clients).Floating);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void LargeMinimumIsNotSquashedAndTheNeighbourTakesTheRest()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1400, 100) },
        });

        Assert.AreEqual(new Rectangle(0, 0, 520, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(520, 0, 1400, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void LargeMinimumOnTheCrossAxisLeavesTheNeighbourOnTheMonitor()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(100, 2000) },
        });

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 2000), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void NestedMinimumWidensItsColumnAndShrinksTheNeighbour()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(3), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1500, 100) },
        });

        Assert.AreEqual(new Rectangle(0, 0, 420, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(420, 0, 1500, 540), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(420, 540, 1500, 540), BoundsOf(outcome, 3));
    }

    [TestMethod]
    public void MinimumsThatExceedTheMonitorStayOnIt()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1200, 100) },
        });

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1200, 100) },
        });

        Assert.AreEqual(new Rectangle(0, 0, 1200, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(720, 0, 1200, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void AMinimumAlmostAsWideAsTheMonitorLeavesTheNeighbourTheSmallestShare()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1900, 100) },
        });

        Assert.AreEqual(new Rectangle(0, 0, 96, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(20, 0, 1900, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void AnOversizedMinimumLeavesEveryOtherTileOnTheMonitor()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1920, 1080) },
        });
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new WindowAppeared(new WindowId(3), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(4), Area));

        Assert.AreEqual(Area, BoundsOf(outcome, 1));
        foreach (var window in new long[] { 2, 3, 4 })
        {
            var bounds = BoundsOf(outcome, window);
            Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0, $"window {window} is {bounds}");
            Assert.IsTrue(
                bounds.X >= Area.X && bounds.Y >= Area.Y && bounds.X + bounds.Width <= Area.X + Area.Width && bounds.Y + bounds.Height <= Area.Y + Area.Height,
                $"window {window} is {bounds}");
        }
    }

    [TestMethod]
    public void SoleWindowMinimumLargerThanTheMonitorExtendsPastIt()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(3000, 2000) },
        });

        Assert.AreEqual(new Rectangle(0, 0, 3000, 2000), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void BorderIsOutsideTheMinimum()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("""
            border_size = 10
            gaps_in = 0
            gaps_out = 0
            """)));
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1400, 100) },
        });

        Assert.AreEqual(new Rectangle(10, 10, 480, 1060), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(510, 10, 1400, 1060), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void ReportedMaximumShrinksIntoAFloatAndTheNeighbourTakesTheTile()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new WindowLimitsChanged(new WindowId(2), null, SmallMax));

        Assert.IsTrue(Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), outcome.State.Clients).Floating);
        Assert.AreEqual(new Rectangle(960, 0, 400, 300), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void ReportedMinimumGrowsTheTile()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new WindowLimitsChanged(new WindowId(2), new Size(1400, 100), null));

        Assert.AreEqual(new Rectangle(0, 0, 520, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(520, 0, 1400, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void AWindowsOwnMinimumStillHoldsATile()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = new Size(1400, 100) },
        });

        Assert.AreEqual(new Rectangle(520, 0, 1400, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void VisibleMinimumShrinksByTheFrameInsets()
    {
        var insets = new FrameInsets(7, 0, 7, 7);

        var (min, max) = TrackLimits.Visible(new Size(200, 120), new Size(414, 314), insets);

        Assert.AreEqual(new Size(186, 113), min);
        Assert.AreEqual(new Size(400, 307), max);
    }

    [TestMethod]
    public void OuterMaximumBelowFiveIsNoMaximum()
    {
        var (min, max) = TrackLimits.Visible(new Size(80, 40), new Size(4, 3), FrameInsets.None);

        Assert.AreEqual(new Size(80, 40), min);
        Assert.IsNull(max);
    }

    [TestMethod]
    public void OneUnlimitedMaximumAxisLeavesTheOther()
    {
        var (_, max) = TrackLimits.Visible(null, new Size(4, 300), FrameInsets.None);

        Assert.AreEqual(new Size(int.MaxValue, 300), max);
    }

    [TestMethod]
    public void MissingOuterTrackSizesAreNoLimits()
    {
        var (min, max) = TrackLimits.Visible(null, null, new FrameInsets(7, 0, 7, 7));

        Assert.IsNull(min);
        Assert.IsNull(max);
    }

    [TestMethod]
    public void AMinimumUsesTheVisibleFrameAndPlacementAddsTheInsetsOnce()
    {
        var insets = new FrameInsets(7, 0, 7, 7);
        var outerMin = new Size(1414, 107);
        var (visible, _) = TrackLimits.Visible(outerMin, null, insets);
        Assert.AreEqual(new Size(1400, 100), visible);

        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { MinSize = visible },
        });

        var tile = BoundsOf(outcome, 2);
        Assert.AreEqual(1400, tile.Width);
        Assert.AreEqual(Area.Height, tile.Height);

        var placed = insets.OuterFor(tile);
        Assert.AreEqual(outerMin.Width, placed.Width);
        Assert.AreEqual(tile.Height + insets.Top + insets.Bottom, placed.Height);
    }
}
