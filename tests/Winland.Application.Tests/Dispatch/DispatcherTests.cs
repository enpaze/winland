namespace Winland.Application.Tests;

[TestClass]
public class DispatcherTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);
    private static readonly Rectangle Tall = new(0, 0, 1080, 1920);

    [TestMethod]
    public void FocusReachesTheNeighborInEachDirection()
    {
        var core = TwoWindows();

        var left = core.Handle(new Dispatch("focus", "l"));

        Assert.AreEqual(new WindowId(1), left.Focus);
        Assert.IsEmpty(left.Placements);

        var right = core.Handle(new Dispatch("focus", "right"));

        Assert.AreEqual(new WindowId(2), right.Focus);

        var stacked = ThreeWindows();
        Assert.AreEqual(new WindowId(2), FocusToward(stacked, "u"));
        Assert.IsNull(FocusToward(stacked, "r"));
        Assert.AreEqual(new WindowId(3), FocusToward(stacked, "d"));
        Assert.AreEqual(new WindowId(1), FocusToward(stacked, "l"));
        Assert.IsNull(FocusToward(stacked, "l"));
    }

    private static WindowId? Active(WindowManager core)
    {
        var address = System.Text.Json.JsonElement.Parse(core.Execute("j/get window").Text).GetProperty("address").GetString() ?? "";
        return address.Length > 2 ? new WindowId(Convert.ToInt64(address[2..], 16)) : null;
    }

    [TestMethod]
    public void FocusPicksTheNeighborFocusedLast()
    {
        var core = ThreeWindows();
        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));

        Assert.AreEqual(new WindowId(3), core.Handle(new Dispatch("focus", "r")).Focus);
    }

    [TestMethod]
    [DataRow("dwindle")]
    public void FocusFromAFloatingClientLooksByAngle(string layout)
    {
        var core = Floats("general:layout = " + layout + "\n");
        ShowFloat(core, 2, 400, 150);
        ShowFloat(core, 1, 100, 100);

        Assert.AreEqual(new WindowId(2), FocusToward(core, "r"));
    }

    [TestMethod]
    public void FocusFromAFloatingClientNeverPicksATile()
    {
        var core = Floats("");
        Appear(core, 1);
        Appear(core, 2);
        ShowFloat(core, 3, 760, 100);

        Assert.IsNull(FocusToward(core, "r"));
    }

    [TestMethod]
    public void FocusStopsWhenNothingLiesThatWay()
    {
        var core = TwoWindows();
        core.Handle(new Dispatch("focus", "l"));

        var outcome = core.Handle(new Dispatch("focus", "l"));

        Assert.IsNull(outcome.Focus);
        Assert.AreEqual(new WindowId(1), core.Handle(new Dispatch("close")).Close);
    }

    [TestMethod]
    public void BoundFocusRequestsFocusAndLaterDispatchersUseIt()
    {
        var core = Load("""
            bind = SUPER, H, focus, l
            bind = SUPER, Q, close
            """);
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var focused = Press(core, "H");

        Assert.IsTrue(focused.Consumed);
        Assert.AreEqual(new WindowId(1), focused.Focus);
        Assert.AreEqual(new WindowId(1), Press(core, "Q").Close);
    }

    [TestMethod]
    public void FocusBecomesTheWindowNewClientsSplit()
    {
        var core = TwoWindows();
        core.Handle(new Dispatch("focus", "l"));

        var outcome = Appear(core, 3);

        Assert.AreEqual(new Rectangle(0, 0, 960, 540), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 540, 960, 540), BoundsOf(outcome, 3));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void MoveWindowSwapsTheActiveClientWithTheNeighbor()
    {
        var core = TwoWindows();

        var outcome = core.Handle(new Dispatch("move", "l"));

        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void MoveWindowUpSwapsAVerticalPair()
    {
        var core = ThreeWindows();

        var outcome = core.Handle(new Dispatch("move", "up"));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 540, 960, 540), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(960, 0, 960, 540), BoundsOf(outcome, 3));
    }

    [TestMethod]
    public void BoundMoveWindowRearrangesTheTree()
    {
        var core = Load("bind = SUPER, L, move, l");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var outcome = Press(core, "L");

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void MoveWindowWithoutADirectionLeavesTheTreeAlone()
    {
        var core = TwoWindows();

        var outcome = core.Handle(new Dispatch("move"));

        Assert.IsEmpty(outcome.Placements);
        Assert.AreEqual(new WindowId(2), core.Handle(new Dispatch("close")).Close);
    }

    [TestMethod]
    public void MoveWindowSnapsAFloatingClientToTheWorkAreaEdge()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Wide)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = new Rectangle(100, 100, 200, 150),
        });

        var right = core.Handle(new Dispatch("move", "r"));
        Assert.AreEqual(new Rectangle(1720, 465, 200, 150), BoundsOf(right, 1));

        var down = core.Handle(new Dispatch("move", "down"));
        Assert.AreEqual(new Rectangle(1720, 930, 200, 150), BoundsOf(down, 1));
    }

    [TestMethod]
    public void ResizeChangesTheSplitRatioOnTheFreeAxis()
    {
        var sideBySide = TwoWindows();

        var wider = sideBySide.Handle(new Dispatch("resize", "480 0"));

        Assert.AreEqual(new Rectangle(0, 0, 480, 1080), BoundsOf(wider, 1));
        Assert.AreEqual(new Rectangle(480, 0, 1440, 1080), BoundsOf(wider, 2));

        var stacked = new WindowManager();
        Appear(stacked, 1, Tall);
        Appear(stacked, 2, Tall);

        var taller = stacked.Handle(new Dispatch("resize", "0 480"));

        Assert.AreEqual(new Rectangle(0, 0, 1080, 480), BoundsOf(taller, 1));
        Assert.AreEqual(new Rectangle(0, 480, 1080, 1440), BoundsOf(taller, 2));
        Assert.AreEqual(new Rectangle(0, 0, 1080, 480), BoundsOf(stacked.Handle(new Dispatch("resize", "20 0")), 1));
    }

    [TestMethod]
    public void ResizeOnAFloatingClientGrowsItFromTheCenter()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Wide)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = new Rectangle(100, 100, 200, 150),
        });

        var outcome = core.Handle(new Dispatch("resize", "20 10"));

        Assert.AreEqual(new Rectangle(850, 460, 220, 160), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void BoundResizeChangesTheSplit()
    {
        var core = Load("bind = SUPER, H, resize, 480 0");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var outcome = Press(core, "H");

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new Rectangle(0, 0, 480, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(480, 0, 1440, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void FloatLiftsTheActiveClientOutAndBack()
    {
        var core = TwoWindows();

        var floating = core.Handle(new Dispatch("float"));

        Assert.IsTrue(Client(floating, 2).Floating);
        Assert.IsFalse(Client(floating, 1).Floating);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(floating, 1));
        Assert.AreEqual(new Rectangle(1120, 340, 640, 400), BoundsOf(floating, 2));

        var tiled = core.Handle(new Dispatch("float"));

        Assert.IsFalse(Client(tiled, 2).Floating);
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(tiled, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(tiled, 2));
    }

    [TestMethod]
    public void FloatRecentersATiledClientAtItsLastFloatingSize()
    {
        var core = Flat("");
        core.Handle(new WindowAppeared(new WindowId(1), Wide)
        {
            Bounds = new Rectangle(100, 80, 800, 600),
        });

        var floated = core.Handle(new Dispatch("float"));

        Assert.IsTrue(Client(floated, 1).Floating);
        Assert.AreEqual(new Rectangle(560, 240, 800, 600), BoundsOf(floated, 1));

        var same = Flat("");
        same.Handle(new WindowAppeared(new WindowId(1), Wide)
        {
            Bounds = Wide,
        });

        var grown = same.Handle(new Dispatch("float"));

        Assert.AreEqual(new Rectangle(-10, -10, 1930, 1090), BoundsOf(grown, 1));
    }

    [TestMethod]
    public void BoundFloatSwitchesTheActiveClient()
    {
        var core = Load("bind = SUPER, Space, float");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var outcome = Press(core, "Space");

        Assert.IsTrue(outcome.Consumed);
        Assert.IsTrue(Client(outcome, 2).Floating);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void SplitFlipsTheCurrentSplitWhenItIsPreserved()
    {
        var core = Flat("");
        Appear(core, 1);
        Appear(core, 2);

        var outcome = core.Handle(new Dispatch("split"));

        Assert.AreEqual(new Rectangle(0, 0, 1920, 540), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 540, 1920, 540), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void BoundSplitRuns()
    {
        var core = new WindowManager();
        var document = ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            bind = SUPER, T, split
            """);
        core.Handle(new Configure(document));
        core.Handle(new ConfigLoaded(document));
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var outcome = Press(core, "T");

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 540), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 540, 1920, 540), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void DispatchRunsCloseOnTheSamePath()
    {
        var core = TwoWindows();

        var outcome = core.Handle(new Dispatch("close"));

        Assert.AreEqual(new WindowId(2), outcome.Close);
        Assert.Contains(client => client.Window == new WindowId(2), outcome.State.Clients);
        Assert.IsNull(outcome.Focus);
    }

    [TestMethod]
    public void UnknownDispatcherIsReportedWithoutAnEffect()
    {
        var core = TwoWindows();

        var outcome = core.Handle(new Dispatch("nosuch", "notepad"));

        Assert.IsFalse(outcome.Consumed);
        Assert.IsNull(outcome.Close);
        Assert.IsNull(outcome.Focus);
        Assert.IsEmpty(outcome.Placements);
        Assert.AreEqual(2, outcome.State.Clients.Count);
    }

    private static WindowId? FocusToward(WindowManager core, string direction) =>
        core.Handle(new Dispatch("focus", direction)).Focus;

    private static WindowManager TwoWindows()
    {
        var core = new WindowManager();
        Appear(core, 1);
        Appear(core, 2);
        return core;
    }

    private static WindowManager ThreeWindows()
    {
        var core = TwoWindows();
        Appear(core, 3);
        return core;
    }

    private const string Gapless = "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n";

    private static WindowManager Load(string text)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(Gapless + text)));
        return core;
    }

    private static WindowManager Floats(string extra) =>
        new(ConfigParser.Parse(Gapless + extra + "windowrule = float, class:box\n"));

    private static void ShowFloat(WindowManager core, long window, int x, int y)
    {
        var id = new WindowId(window);
        var bounds = new Rectangle(x, y, 200, 200);
        core.Handle(new WindowAppeared(id, Wide)
        {
            Bounds = bounds,
            Traits = WindowTraits.Normal with { Class = "box" },
        });
        core.Handle(new WindowMoveStarted(id, new Point(x, y)));
        core.Handle(new WindowMoveEnded(id, new Point(x, y), bounds));
    }
}
