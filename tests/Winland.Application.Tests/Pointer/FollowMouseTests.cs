namespace Winland.Application.Tests;

[TestClass]
public class FollowMouseTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void FollowMouseDefaultsToTheManual()
    {
        var input = InputOptions.From(ConfigParser.Parse(""));

        Assert.IsTrue(input.FollowMouse);
    }

    [TestMethod]
    public void MovingOntoAnotherWindowFocusesIt()
    {
        var (core, laid) = Tiled();

        var seeded = Move(core, Center(laid, 2), 1_000);
        var followed = Move(core, Center(laid, 1), 1_100);

        Assert.IsNull(seeded.Focus);
        Assert.AreEqual(new WindowId(1), followed.Focus);
        Assert.Contains(item => item is FocusChanged changed && changed.Window == new WindowId(1), followed.Events);
    }

    [TestMethod]
    public void CrossingTheClientAPanelOpenedOverLeavesThePanelTheFocus()
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 1), 1_000);
        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));
        core.Handle(new WindowFocused(new WindowId(99), FocusCause.User));

        var crossed = Move(core, Center(laid, 1) with { X = Center(laid, 1).X + 40 }, 1_100);
        var left = Move(core, Center(laid, 2), 1_200);

        Assert.IsNull(crossed.Focus);
        Assert.AreEqual(new WindowId(2), left.Focus);
    }

    [TestMethod]
    public void AMenuOverAnotherTileKeepsTheFocusWhereItIs()
    {
        var (core, laid) = Tiled();
        Move(core, Center(laid, 2), 1_000);

        var overMenu = core.Handle(new PointerMoved(Center(laid, 1)) { TimeMs = 1_100, Under = new WindowId(99) });
        var offMenu = core.Handle(new PointerMoved(Center(laid, 1) with { X = Center(laid, 1).X + 40 }) { TimeMs = 1_200, Under = new WindowId(1) });

        Assert.IsNull(overMenu.Focus);
        Assert.AreEqual(new WindowId(1), offMenu.Focus);
    }

    [TestMethod]
    public void DisabledFollowLeavesTheFocusedClient()
    {
        var (core, laid) = Tiled("input:follow_mouse = false\n");
        core.Handle(new Dispatch("focus", "address:0x2"));

        Move(core, Center(laid, 2), 1_000);
        var followed = Move(core, Center(laid, 1), 1_100);

        Assert.IsNull(followed.Focus);
        Assert.DoesNotContain(item => item is FocusChanged, followed.Events);
    }

    [TestMethod]
    public void DisabledFollowLeavesANewBackgroundWindowUnfocusedUntilAClick()
    {
        var core = new WindowManager(ConfigParser.Parse("input:follow_mouse = false\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        core.Handle(new WindowAppeared(new WindowId(2), Area));

        Assert.AreEqual("\"address\":\"0x1\"", Address(core));
        core.Handle(new PointerButtonPressed("mouse:left"));
        core.Handle(new WindowFocused(new WindowId(2), FocusCause.User));
        Assert.AreEqual("\"address\":\"0x2\"", Address(core));
    }

    [TestMethod]
    public void DisabledFollowKeepsTheForegroundWindowWhenItIsAdopted()
    {
        var core = new WindowManager(ConfigParser.Parse("input:follow_mouse = false\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        core.Handle(new WindowAppeared(new WindowId(2), Area) { Foreground = true });

        Assert.AreEqual("\"address\":\"0x2\"", Address(core));
    }

    [TestMethod]
    public void APauseBeforeCrossingLeavesTheFocus()
    {
        var core = Floats("");
        core.Handle(Box(1, 0, 0));
        core.Handle(Box(2, 100, 0));

        Move(core, new Point(101, 50), 0);
        var paused = Move(core, new Point(40, 50), 700);

        Assert.IsNull(paused.Focus);
        Assert.AreEqual("\"address\":\"0x2\"", Address(core));
    }

    [TestMethod]
    public void DisabledFollowLeavesFocusAcrossTiledAndFloating()
    {
        var core = Floats("input:follow_mouse = 0\n");
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(Box(2, 100, 100, 200, 200));
        core.Handle(new Dispatch("focus", "address:0x2"));
        var switched = Move(core, new Point(700, 700), 1_000);

        Assert.IsNull(switched.Focus);
    }

    [TestMethod]
    public void DisabledFollowLeavesTheFocusedMonitor()
    {
        var core = new WindowManager(ConfigParser.Parse("input:follow_mouse = 0\n"));
        var left = Display(@"\\.\DISPLAY1", 0, 800, 600);
        var right = Display(@"\\.\DISPLAY2", 800, 800, 600);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var moved = Move(core, new Point(1_000, 100), 1_000);

        Assert.AreEqual(new MonitorId(0), moved.State.ActiveMonitor);
        Assert.IsNull(moved.Focus);
        Assert.DoesNotContain(item => item is FocusChanged, moved.Events);
    }

    [TestMethod]
    public void FollowMouseFocusesTheClientOnTheMonitorUnderThePointer()
    {
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"));
        var left = Display(@"\\.\DISPLAY1", 0, 800, 600);
        var right = Display(@"\\.\DISPLAY2", 800, 800, 600);
        core.Handle(new MonitorsChanged([left, right]));
        var first = core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        var second = core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        Move(core, Center(first, 1), 1_000);
        var followed = Move(core, Center(second, 2), 1_100);

        Assert.AreEqual(new WindowId(2), followed.Focus);
        Assert.AreEqual(new MonitorId(1), followed.State.ActiveMonitor);
    }

    [TestMethod]
    public void ClosingFocusesTheNearestClient()
    {
        var nearest = TiledThree("");
        var next = nearest.Handle(new WindowClosed(new WindowId(3)));

        Assert.AreEqual(new WindowId(2), next.Focus);
    }

    [TestMethod]
    public void ClosingPrefersATileOverAFloatOnTop()
    {
        var core = Floats("");
        core.Handle(Box(3, 1390, 490));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new Dispatch("focus", "address:0x2"));

        var closed = core.Handle(new WindowClosed(new WindowId(2)));
        var last = core.Handle(new WindowClosed(new WindowId(1)));

        Assert.AreEqual(new WindowId(1), closed.Focus);
        Assert.AreEqual(new WindowId(3), last.Focus);
    }

    [TestMethod]
    public void HoverReachesTheTilesOfAnOpenSpecialWorkspaceBeforeTheTilesUnderIt()
    {
        var (core, _) = Tiled();
        core.Handle(new Dispatch("scratchpad", "scratch"));
        core.Handle(new WindowAppeared(new WindowId(3), Area));
        var special = core.Handle(new WindowAppeared(new WindowId(4), Area));
        Assert.AreEqual(new WorkspaceId("special:scratch"), Assert.ContainsSingle(client => client.Window == new WindowId(3), special.State.Clients).Workspace);

        Move(core, Center(special, 4), 1_000);
        var hovered = Move(core, Center(special, 3), 1_100);

        Assert.AreEqual(new WindowId(3), hovered.Focus);
        Assert.AreEqual(new WorkspaceId("special:scratch"), hovered.State.SpecialWorkspace);
    }

    [TestMethod]
    public void HoverOnAMonitorWithAnOpenSpecialWorkspaceReachesItsTiles()
    {
        var core = new WindowManager(ConfigParser.Parse(FlatConfig("")));
        var left = Display(@"\\.\DISPLAY1", 0, 1920, 1080);
        var right = Display(@"\\.\DISPLAY2", 1920, 1920, 1080);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), left.WorkArea));
        core.Handle(new Dispatch("scratchpad", "scratch"));
        core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea));
        var special = core.Handle(new WindowAppeared(new WindowId(4), left.WorkArea));

        Move(core, Center(special, 4), 1_000);
        var hovered = Move(core, Center(special, 3), 1_100);

        Assert.AreEqual(new WindowId(3), hovered.Focus);
    }

    private static (WindowManager WindowManager, Outcome Laid) Tiled(string extra = "")
    {
        var core = new WindowManager(ConfigParser.Parse(FlatConfig(extra)));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        return (core, core.Handle(new WindowAppeared(new WindowId(2), Area)));
    }

    private static WindowManager TiledThree(string extra)
    {
        var core = new WindowManager(ConfigParser.Parse(FlatConfig(extra)));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new WindowAppeared(new WindowId(3), Area));
        return core;
    }

    private static WindowManager Floats(string extra) =>
        new(ConfigParser.Parse(FlatConfig(extra) + "windowrule = float, class:box\n"));

    private static WindowAppeared Box(long window, int x, int y, int width = 100, int height = 100) =>
        new(new WindowId(window), Area)
        {
            Bounds = new Rectangle(x, y, width, height),
            Traits = WindowTraits.Normal with { Class = "box" },
        };

    private static Outcome Move(WindowManager core, Point point, long time) =>
        core.Handle(new PointerMoved(point) { TimeMs = time });

    private static Point Center(Outcome outcome, long window)
    {
        var bounds = Assert.ContainsSingle(client => client.Window == new WindowId(window), outcome.State.Clients).Bounds;
        return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }

    private static string Address(WindowManager core)
    {
        var text = core.Execute("j/get window").Text;
        var at = text.IndexOf("\"address\":", StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, text);
        return text[at..(at + "\"address\":\"0x0\"".Length)];
    }
}
