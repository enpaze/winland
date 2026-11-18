namespace Winland.Application.Tests;

[TestClass]
public class PlacementRuleTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly Rectangle Left = new(0, 0, 1920, 1080);
    private static readonly Rectangle Right = new(1920, 0, 1920, 1080);
    private static readonly Rectangle Own = new(5, 7, 200, 150);

    [TestMethod]
    public void ANewFloatOpensCenteredInTheFloatingWorkArea()
    {
        var core = new WindowManager();
        var area = new Rectangle(100, 0, 1820, 1040);

        var outcome = core.Handle(Dialog(1, area));

        Assert.AreEqual(new Rectangle(910, 445, 200, 150), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void AFloatWithoutASizeOpensAtTheDefaultSizeCentered()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
        });

        Assert.AreEqual(new Rectangle(640, 340, 640, 400), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void SizeSetsTheFloatSizeAndTheFloatStaysCentered()
    {
        var core = Ruled("size 800 600");

        var outcome = core.Handle(Box(1));

        Assert.AreEqual(new Rectangle(560, 240, 800, 600), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void APercentageIsHalfTheWorkArea()
    {
        var core = Ruled("size 50% 50%", "move 0% 0%");
        var area = new Rectangle(100, 40, 1800, 1000);

        var outcome = core.Handle(Box(1, area));

        Assert.AreEqual(new Rectangle(100, 40, 900, 500), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void MoveIsMonitorLocalAndAcceptsMinusValues()
    {
        var core = Configured("""
            windowrule = float, class:box
            windowrule = move 10 5, class:box
            """);
        core.Handle(new MonitorsChanged([Display(@"\\.\DISPLAY1", "Dell", Left), Display(@"\\.\DISPLAY2", "Samsung", Right)]));

        var outcome = core.Handle(Box(1, Right));

        Assert.AreEqual(new Rectangle(1920 + 10, 5, 200, 150), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void AMovedNewFloatIsKeptOnScreenByDefault()
    {
        var core = Ruled("move -50 -5");

        var outcome = core.Handle(Box(1));

        Assert.AreEqual(new Rectangle(0, 0, 200, 150), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void KeepingANewFloatOnScreenLeavesItsBorderOut()
    {
        var core = Configured("""
            general:border_size = 3
            windowrule = float, class:box
            windowrule = move 1900 1000, class:box
            """);

        var outcome = core.Handle(Box(1));

        Assert.AreEqual(new Rectangle(1920 - 200, 1080 - 150, 200, 150), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void AMovedFloatIsKeptOnScreen()
    {
        var core = Configured("""
            general:border_size = 0
            windowrule = float, class:box
            """);
        core.Handle(Box(1));

        var moved = Place(core, 1, new Rectangle(-500, -500, 200, 150));

        Assert.AreEqual(new Rectangle(0, 0, 200, 150), Client(moved, 1).Bounds);
    }

    [TestMethod]
    public void MoveAndSizeBothApply()
    {
        var core = Ruled("size 400 300", "move 10 20");

        var outcome = core.Handle(Box(1));

        Assert.AreEqual(new Rectangle(10, 20, 400, 300), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void CenterAndMoveReplaceEachOtherAndTheLastWins()
    {
        var centered = Ruled("move 10 10", "center").Handle(Box(1));
        var moved = Ruled("center", "move 10 10").Handle(Box(1));

        Assert.AreEqual(new Rectangle(860, 465, 200, 150), BoundsOf(centered, 1));
        Assert.AreEqual(new Rectangle(10, 10, 200, 150), BoundsOf(moved, 1));
    }

    [TestMethod]
    public void MoveAndSizeOnlyApplyToFloatingClients()
    {
        var core = new WindowManager(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            windowrule = move 10 10, class:box
            windowrule = size 400 300, class:box
            """));

        var tiled = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "box" },
            Bounds = Own,
        });

        Assert.IsFalse(Assert.ContainsSingle(tiled.State.Clients).Floating);
        Assert.AreEqual(Area, BoundsOf(tiled, 1));
    }

    [TestMethod]
    public void AnExpressionThatCannotBeComputedIsSkipped()
    {
        var core = Ruled("size 100/0 100");

        var outcome = core.Handle(Box(1));

        Assert.AreEqual(new Rectangle(860, 465, 200, 150), BoundsOf(outcome, 1));
    }

    [TestMethod]
    [DataRow("(", "1", ")")]
    [DataRow("-", "1", "")]
    [DataRow("2^", "2", "")]
    [DataRow("abs(", "1", ")")]
    public void AnExpressionNestedTooDeeplyWarnsInsteadOfOverflowingTheStack(string open, string middle, string close)
    {
        var nested = string.Concat(Enumerable.Repeat(open, 100_000)) + middle + string.Concat(Enumerable.Repeat(close, 100_000));

        var document = ConfigParser.Parse($"windowrule = size {nested} 10, class:box\n");

        Assert.AreSequenceEqual([new ConfigWarning(1, "Malformed window rule.")], document.Warnings);
        Assert.IsEmpty(document.WindowRules);
    }

    [TestMethod]
    public void AnExpressionWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse("windowrule = size monitor_w*0.5 10, class:box\n");

        Assert.AreSequenceEqual([new ConfigWarning(1, "Malformed window rule.")], document.Warnings);
        Assert.IsEmpty(document.WindowRules);
    }

    [TestMethod]
    [DataRow("move 10")]
    [DataRow("move 10,10")]
    [DataRow("size 800x600 1")]
    [DataRow("move unknown_w 0")]
    [DataRow("move (10 10")]
    [DataRow("center maybe")]
    [DataRow("monitor")]
    public void AMalformedPlacementRuleWarnsAndIsSkipped(string effect)
    {
        var document = ConfigParser.Parse($"windowrule = {effect}, class:box\n");

        Assert.AreSequenceEqual([new ConfigWarning(1, "Malformed window rule.")], document.Warnings);
        Assert.IsEmpty(document.WindowRules);
    }

    [TestMethod]
    public void MonitorOpensTheWindowOnThatMonitorAndFocusesIt()
    {
        var core = Monitors("windowrule = monitor desc:Samsung, class:box\n");

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Left) { Traits = WindowTraits.Normal with { Class = "box" } });

        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(Right, BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void MonitorRuleWithStayOpensTheWindowThereWithoutFocusingIt()
    {
        var core = Monitors("windowrule = monitor desc:Samsung stay, class:box\n");

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Left) { Traits = WindowTraits.Normal with { Class = "box" } });

        Assert.AreEqual(new MonitorId(0), outcome.State.ActiveMonitor);
        Assert.AreEqual(Right, BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void AMonitorThatDoesNotExistKeepsTheMonitorTheWindowOpenedOn()
    {
        var core = Monitors("windowrule = monitor HDMI-A-9, class:box\n");

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Left) { Traits = WindowTraits.Normal with { Class = "box" } });

        Assert.AreEqual(new MonitorId(0), outcome.State.ActiveMonitor);
        Assert.AreEqual(Left, BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void TheFirstResizeAfterMapIsUndoneOnce()
    {
        long now = 0;
        var core = Ruled("size 800 600");
        core.Clock = () => now;
        core.Handle(Box(1));
        var placed = new Rectangle(560, 240, 800, 600);

        Assert.IsTrue(core.AwaitsFirstResize(new WindowId(1)));
        var moved = core.Handle(new WindowResized(new WindowId(1), placed with { X = 0 }));
        now = 100;
        var undone = core.Handle(new WindowResized(new WindowId(1), new Rectangle(0, 0, 1200, 900)));
        var kept = core.Handle(new WindowResized(new WindowId(1), new Rectangle(0, 0, 1000, 700)));

        Assert.IsEmpty(moved.Placements);
        Assert.AreEqual(placed, BoundsOf(undone, 1));
        Assert.IsEmpty(kept.Placements);
        Assert.IsFalse(core.AwaitsFirstResize(new WindowId(1)));
    }

    [TestMethod]
    public void AResizeLongAfterMapIsLeftAlone()
    {
        long now = 0;
        var core = Ruled("center");
        core.Clock = () => now;
        core.Handle(Box(1));
        now = WindowManager.ReapplyMilliseconds + 1;

        var resized = core.Handle(new WindowResized(new WindowId(1), new Rectangle(0, 0, 1200, 900)));

        Assert.IsEmpty(resized.Placements);
        Assert.IsFalse(core.AwaitsFirstResize(new WindowId(1)));
    }

    private static WindowManager Ruled(params string[] effects)
    {
        var text = "windowrule = float, class:box\n";
        foreach (var effect in effects)
            text += $"windowrule = {effect}, class:box\n";

        return new WindowManager(ConfigParser.Parse(text));
    }

    private static WindowManager Monitors(string config)
    {
        var core = new WindowManager(ConfigParser.Parse("general:gaps_out = 0\ngeneral:border_size = 0\n" + config));
        core.Handle(new MonitorsChanged([Display(@"\\.\DISPLAY1", "Dell", Left), Display(@"\\.\DISPLAY2", "Samsung", Right)]));
        return core;
    }

    private static WindowAppeared Box(long window, Rectangle? area = null) =>
        new(new WindowId(window), area ?? Area)
        {
            Traits = WindowTraits.Normal with { Class = "box" },
            Bounds = Own,
        };

    private static WindowAppeared Dialog(long window, Rectangle area) =>
        new(new WindowId(window), area)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = Own,
        };
}
