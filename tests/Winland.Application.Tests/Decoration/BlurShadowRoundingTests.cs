namespace Winland.Application.Tests;

[TestClass]
public class BlurShadowRoundingTests
{
    private static readonly Rectangle Area = new(0, 0, 800, 600);

    [TestMethod]
    public void DefaultConfigBlursAndUsesRoundCorners()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("")));

        var decoration = For(core.Handle(Appeared(1, Area)), 1);

        Assert.IsTrue(decoration.Blur);
        Assert.AreEqual(CornerPreference.Round, decoration.Corners);
        Assert.IsNull(decoration.Region);
    }

    [TestMethod]
    public void TheBlurSwitchIsHonoredAndTheOtherBlurAndShadowKeywordsWarn()
    {
        var document = ConfigParser.Parse("""
            decoration {
                blur {
                    enabled = false
                    popups = true
                }
                shadow {
                    enabled = false
                    range = 20
                }
            }
            """);
        var core = new WindowManager();
        core.Handle(new Configure(document));
        core.Handle(Appeared(1, Area));

        var off = For(core.Handle(Appeared(2, Area)), 1);

        Assert.AreSequenceEqual(
            [
                new ConfigWarning(4, "Unsupported keyword 'decoration:blur:popups'."),
                new ConfigWarning(7, "Unsupported keyword 'decoration:shadow:enabled'."),
                new ConfigWarning(8, "Unsupported keyword 'decoration:shadow:range'."),
            ],
            document.Warnings);
        Assert.IsFalse(off.Blur);

        var reloaded = core.Handle(new Configure(ConfigParser.Parse("decoration:blur:enabled = true\n")));

        Assert.IsTrue(For(reloaded, 1).Blur);
        Assert.IsEmpty(reloaded.ResetDecorations);
    }

    [TestMethod]
    [DataRow(0, (double)1.0, CornerPreference.DoNotRound, 0)]
    [DataRow(8, (double)1.0, CornerPreference.Round, 0)]
    [DataRow(8, (double)2.0, CornerPreference.Round, 0)]
    [DataRow(4, (double)1.5, CornerPreference.RoundSmall, 0)]
    [DataRow(10, (double)1.0, CornerPreference.DoNotRound, 10)]
    [DataRow(10, (double)2.0, CornerPreference.DoNotRound, 20)]
    [DataRow(5, (double)1.5, CornerPreference.DoNotRound, 8)]
    [DataRow(6, double.NaN, CornerPreference.DoNotRound, 6)]
    public void DwmCornersAreUsedOnlyForTheirOwnRadiusAndAnyOtherIsCut(
        int rounding,
        double scale,
        CornerPreference preference,
        int radius)
    {
        var choice = CornerShape.For(rounding, scale);

        Assert.AreEqual(preference, choice.Preference);
        Assert.AreEqual(radius == 0 ? null : new CornerRegion(radius), choice.Region);
    }

    [TestMethod]
    public void ARadiusThatRoundsToNothingIsSquare()
    {
        Assert.AreEqual(CornerChoice.Square, CornerShape.For(1, 0.2));
    }

    [TestMethod]
    public void AnExactRadiusIsCutAtTheClientsMonitorScale()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("decoration:rounding = 10\n")));
        var scaled = new Monitor("DISPLAY1", "DISPLAY1", 0, 0, 1600, 1200, 2, true, new Rectangle(0, 0, 1600, 1200));
        core.Handle(new MonitorsChanged([scaled]));

        var decoration = For(core.Handle(new WindowAppeared(new WindowId(1), scaled.WorkArea)), 1);

        Assert.AreEqual(CornerPreference.DoNotRound, decoration.Corners);
        Assert.AreEqual(new CornerRegion(20), decoration.Region);
    }

    [TestMethod]
    public void ACircularOutlineStaysInsideTheFrameAndTouchesEveryEdge()
    {
        var outline = CornerShape.Outline(100, 60, new CornerRegion(10));

        Assert.AreEqual(new Point(0, 10), outline[0]);
        Assert.Contains(new Point(10, 0), outline);
        Assert.Contains(new Point(90, 0), outline);
        Assert.Contains(new Point(100, 50), outline);
        Assert.Contains(new Point(10, 60), outline);
        foreach (var point in outline)
{
    Assert.IsInRange(0, 100, point.X);
}
        foreach (var point in outline)
{
    Assert.IsInRange(0, 60, point.Y);
}
        Assert.AreEqual(outline.Count, outline.Distinct().Count());
        Assert.DoesNotContain(new Point(0, 0), outline);
        var mid = Math.Round(10 - (10 / Math.Sqrt(2)), MidpointRounding.AwayFromZero);
        Assert.Contains(point => Math.Abs(point.X - mid) <= 1 && Math.Abs(point.Y - mid) <= 1, outline);
    }

    [TestMethod]
    public void ARadiusIsCutToHalfTheShorterSideAndNothingIsTracedForAnEmptyFrame()
    {
        var pill = CornerShape.Outline(100, 20, new CornerRegion(50));

        Assert.AreEqual(new Point(0, 10), pill[0]);
        Assert.Contains(new Point(10, 0), pill);
        Assert.Contains(new Point(90, 0), pill);
        Assert.IsEmpty(CornerShape.Outline(0, 20, new CornerRegion(5)));
        Assert.AreSequenceEqual(
            [new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1)],
            CornerShape.Outline(1, 1, new CornerRegion(5)));
    }

    private static Decoration For(Outcome outcome, long window)
    {
        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(window), outcome.State.Clients);
        return Assert.ContainsSingle(decoration => decoration.Client == client.Id, outcome.Decorations);
    }
}
