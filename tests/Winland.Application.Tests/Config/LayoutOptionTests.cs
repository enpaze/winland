namespace Winland.Application.Tests;

[TestClass]
public class LayoutOptionTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);
    private static readonly Rectangle Tall = new(0, 0, 1080, 1920);

    [TestMethod]
    public void InnerGapsShrinkClientsThatDoNotTouchThatEdge()
    {
        var core = Configured("""
            gaps_in = 10
            gaps_out = 0
            border_size = 0
            """);

        core.Handle(Appeared(1, Wide));
        var outcome = core.Handle(Appeared(2, Wide));

        Assert.AreEqual(new Rectangle(0, 0, 950, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(970, 0, 950, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void GapSidesFollowTheSameCountRulesAsAMargin()
    {
        var horizontal = Configured("""
            gaps_in = 8 4
            gaps_out = 0
            border_size = 0
            """);
        horizontal.Handle(Appeared(1, Wide));
        var sideBySide = horizontal.Handle(Appeared(2, Wide));

        Assert.AreEqual(new Rectangle(0, 0, 956, 1080), BoundsOf(sideBySide, 1));
        Assert.AreEqual(new Rectangle(964, 0, 956, 1080), BoundsOf(sideBySide, 2));

        var vertical = Configured("""
            gaps_in = 10 0 30 0
            gaps_out = 0
            border_size = 0
            """);
        vertical.Handle(Appeared(1, Tall));
        var stacked = vertical.Handle(Appeared(2, Tall));

        Assert.AreEqual(new Rectangle(0, 0, 1080, 930), BoundsOf(stacked, 1));
        Assert.AreEqual(new Rectangle(0, 970, 1080, 950), BoundsOf(stacked, 2));
    }

    [TestMethod]
    public void OuterGapsInsetTheTilingArea()
    {
        var core = Configured("""
            gaps_in = 0
            gaps_out = 10 30 50 70
            border_size = 0
            """);

        var outcome = core.Handle(Appeared(1, Wide));

        Assert.AreEqual(new Rectangle(70, 10, 1820, 1020), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void BorderSizeReservesSpaceOnEverySide()
    {
        var core = Configured("""
            gaps_in = 0
            gaps_out = 0
            border_size = 2
            """);

        var outcome = core.Handle(Appeared(1, Wide));

        Assert.AreEqual(new Rectangle(2, 2, 1916, 1076), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void BorderSizeClampsToItsLimit()
    {
        var core = Configured("""
            gaps_in = 0
            gaps_out = 0
            border_size = 50
            """);

        var outcome = core.Handle(Appeared(1, Wide));

        Assert.AreEqual(new Rectangle(20, 20, 1880, 1040), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void OmittedOptionsUseTheDocumentedDefaults()
    {
        var core = Configured("");

        var outcome = core.Handle(Appeared(1, Wide));

        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void LatestOptionWinsAndALaterConfigStartsOver()
    {
        var core = Configured("""
            gaps_in = 0
            gaps_out = 10
            gaps_out = 40
            border_size = 0
            """);
        var first = core.Handle(Appeared(1, Wide));
        Assert.AreEqual(new Rectangle(40, 40, 1840, 1000), BoundsOf(first, 1));

        var replaced = core.Handle(new Configure(ConfigParser.Parse("""
            gaps_in = 0
            border_size = 0
            """)));

        Assert.AreEqual(new Rectangle(8, 8, 1904, 1064), BoundsOf(replaced, 1));
    }

    [TestMethod]
    [DataRow("nan")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-infinity")]
    public void ANumberThatIsNotFiniteWarnsAndIsSkipped(string value)
    {
        var document = ConfigParser.Parse($"""
            column_width = 0.7
            column_width = {value}
            active_opacity = 0.5
            active_opacity = {value}
            """);

        var scrolling = ScrollingOptions.From(document);
        var cursor = CursorOptions.From(document);

        TestSequence.Equal([2, 4], document.Warnings.Select(warning => warning.Line));

        Assert.AreEqual(0.7, scrolling.ColumnWidth);
    }

    [TestMethod]
    public void InvalidOptionValuesAreLeftOut()
    {
        var core = Configured("""
            gaps_in = 0
            gaps_out = 40
            gaps_out = no
            border_size = 0
            """);

        var outcome = core.Handle(Appeared(1, Wide));

        Assert.AreEqual(new Rectangle(40, 40, 1840, 1000), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void ChangingGapsMovesClientsAlreadyPlaced()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Wide));

        var outcome = core.Handle(new Configure(ConfigParser.Parse("""
            gaps_in = 0
            gaps_out = 10
            border_size = 0
            """)));

        Assert.AreEqual(new Rectangle(10, 10, 1900, 1060), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void ASplitKeepsItsDirectionWhenTheContainerChangesShape()
    {
        var core = Configured(FlatConfig(""));
        core.Handle(Appeared(1, Wide));
        core.Handle(Appeared(2, Wide));
        core.Handle(Appeared(3, Wide));

        var outcome = core.Handle(new WindowClosed(new WindowId(1)));

        Assert.AreEqual(new Rectangle(0, 0, 1920, 540), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(0, 540, 1920, 540), BoundsOf(outcome, 3));
    }

    [TestMethod]
    public void ASplitsDirectionSurvivesMinimizeAndRestore()
    {
        var core = Configured(FlatConfig(""));
        core.Handle(Appeared(1, Wide));
        core.Handle(Appeared(2, Wide));
        core.Handle(Appeared(3, Wide));
        core.Handle(new WindowMinimized(new WindowId(3)));

        var outcome = core.Handle(new WindowRestored(new WindowId(3)));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 540), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(960, 540, 960, 540), BoundsOf(outcome, 3));
    }

    [TestMethod]
    public void AnotherLayoutNameStillTilesWithDwindle()
    {
        var core = Configured(FlatConfig("""
            layout = tabbed
            """));
        core.Handle(Appeared(1, Wide));

        var outcome = core.Handle(Appeared(2, Wide));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void TheFloatingLayoutFloatsEveryNewClient()
    {
        var core = Configured(FlatConfig("""
            layout = floating
            """));
        core.Handle(Appeared(1, Wide));

        var outcome = core.Handle(Appeared(2, Wide));

        Assert.IsTrue(Client(outcome, 1).Floating);
        Assert.IsTrue(Client(outcome, 2).Floating);
        Assert.AreEqual("floating", Assert.ContainsSingle(outcome.State.Workspaces).TiledLayout);
    }

    [TestMethod]
    public void FloatingClientOpensCenteredWhenGapsAndBorderAreSet()
    {
        var core = Configured("""
            gaps_in = 12
            gaps_out = 20
            border_size = 4
            """);
        var bounds = new Rectangle(400, 300, 320, 200);
        var tiled = core.Handle(Appeared(1, Wide));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Wide)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = bounds,
        });

        Assert.AreEqual(new Rectangle(24, 24, 1872, 1032), BoundsOf(tiled, 1));
        Assert.AreEqual(NewFloat.Centered(Wide, bounds), BoundsOf(outcome, 2));
    }

}
