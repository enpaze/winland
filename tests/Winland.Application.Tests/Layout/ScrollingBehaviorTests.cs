using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class ScrollingBehaviorTests
{
    private const string Scrolling = "layout = scrolling\n";

    [TestMethod]
    public void ScrollFocusCycling()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "a", "b", "c", "d" })
            desk.Spawn(name);

        desk.Ok("focus", "class:a");
        desk.Ok("focus", "r");
        Assert.AreEqual("b", desk.ActiveClass());

        desk.Ok("focus", "r");
        Assert.AreEqual("c", desk.ActiveClass());

        desk.Ok("focus", "r");
        Assert.AreEqual("d", desk.ActiveClass());

        desk.Ok("move", "l");
        Assert.AreEqual("d", desk.ActiveClass());

        desk.Ok("focus", "u");
        Assert.AreEqual("c", desk.ActiveClass());
    }

    [TestMethod]
    public void ScrollFocusWrapping()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "a", "b", "c", "d" })
            desk.Spawn(name);

        desk.Ok("focus", "class:a");
        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("d", desk.ActiveClass());

        desk.Ok("layoutmsg", "focus r");
        Assert.AreEqual("a", desk.ActiveClass());
    }

    [TestMethod]
    public void ScrollSwapcolWrapping()
    {
        var desk = new TestDesktop(Scrolling);
        string[] names = ["a", "b", "c", "d"];
        foreach (var name in names)
            desk.Spawn(name);

        desk.Ok("focus", "class:a");
        desk.Ok("layoutmsg", "swapcol l");
        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("c", desk.ActiveClass());

        CloseAll(desk);
        foreach (var name in names)
            desk.Spawn(name);

        desk.Ok("focus", "class:d");
        desk.Ok("layoutmsg", "swapcol r");
        desk.Ok("layoutmsg", "focus r");
        Assert.AreEqual("b", desk.ActiveClass());
    }

    [TestMethod]
    public void ScrollLAYOUTHANDLEDFullscreen()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "kitty_scroll_A", "kitty_scroll_B", "kitty_scroll_C" })
            desk.Spawn(name);

        desk.Ok("focus", "class:kitty_scroll_B");
        desk.Ok("fullscreen");
        Assert.AreEqual("1920,1080", desk.ActivePlace().Size);
        Assert.AreEqual("kitty_scroll_B", desk.ActiveClass());

        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("kitty_scroll_A", desk.ActiveClass());

        desk.Ok("layoutmsg", "focus r");
        desk.Ok("layoutmsg", "focus r");
        Assert.AreEqual("kitty_scroll_C", desk.ActiveClass());

        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("1920,1080", desk.ActivePlace().Size);
        Assert.AreEqual("kitty_scroll_B", desk.ActiveClass());
    }

    [TestMethod]
    public void ScrollLAYOUTHANDLEDMaximized()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "kitty_scroll_A", "kitty_scroll_B", "kitty_scroll_C" })
            desk.Spawn(name);

        desk.Ok("focus", "class:kitty_scroll_B");
        desk.Ok("maximize");
        Assert.AreEqual("1866,1036", desk.ActivePlace().Size);
        Assert.AreEqual("kitty_scroll_B", desk.ActiveClass());
        Assert.AreEqual(1, TestDesktop.Fullscreen(desk.ActiveWindow()));

        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("kitty_scroll_A", desk.ActiveClass());

        desk.Ok("layoutmsg", "focus r");
        desk.Ok("layoutmsg", "focus r");
        Assert.AreEqual("kitty_scroll_C", desk.ActiveClass());

        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("1866,1036", desk.ActivePlace().Size);
        Assert.AreEqual("kitty_scroll_B", desk.ActiveClass());
        Assert.AreEqual(1, TestDesktop.Fullscreen(desk.ActiveWindow()));
    }

    [TestMethod]
    public void ScrollLAYOUTHANDLEDFloatingWindowHiding()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("under");
        desk.Ok("focus", "class:under");
        desk.Ok("float");

        desk.Spawn("tiledOne");
        desk.Ok("fullscreen");
        ExpectClient(desk, "under", floating: true, visible: false, fullscreen: 0, over: false);

        desk.Spawn("tiledTwo");
        desk.Ok("maximize");

        desk.Ok("focus", "class:tiledOne");
        ExpectClient(desk, "tiledOne", floating: false, visible: true, fullscreen: 2);
        ExpectClient(desk, "under", floating: true, visible: false, fullscreen: 0, over: false);

        desk.Set("windowrule", "float, class:floating*");
        desk.Set("windowrule", "size 950 500, class:floating*");
        desk.Spawn("floatingOne");
        desk.Spawn("floatingTwo");
        ExpectClient(desk, "floatingOne", floating: true, visible: true, fullscreen: 0, over: true);
        ExpectClient(desk, "floatingTwo", floating: true, visible: true, fullscreen: 0, over: true);

        desk.Ok("focus", "class:floatingOne");
        desk.Ok("maximize");
        ExpectClient(desk, "floatingOne", floating: true, visible: true, fullscreen: 1, over: true);
        ExpectClient(desk, "floatingTwo", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "tiledOne", floating: false, visible: false, fullscreen: 2);
        ExpectClient(desk, "tiledTwo", floating: false, visible: false, fullscreen: 1);
        ExpectClient(desk, "under", floating: true, visible: false, fullscreen: 0, over: false);

        desk.Spawn("floatingThree");
        ExpectClient(desk, "floatingThree", floating: true, visible: true, fullscreen: 0, over: true);

        desk.Ok("focus", "class:floatingOne");
        desk.Ok("maximize", "off");
        ExpectClient(desk, "floatingOne", floating: true, visible: true, fullscreen: 0, over: true);
        ExpectClient(desk, "floatingTwo", floating: true, visible: true, fullscreen: 0, over: true);
        ExpectClient(desk, "floatingThree", floating: true, visible: true, fullscreen: 0, over: true);
        ExpectClient(desk, "tiledOne", floating: false, visible: true, fullscreen: 2);
        ExpectClient(desk, "tiledTwo", floating: false, visible: false, fullscreen: 1);
        ExpectClient(desk, "under", floating: true, visible: false, fullscreen: 0, over: false);

        desk.Ok("focus", "class:tiledTwo");
        ExpectClient(desk, "tiledTwo", floating: false, visible: true, fullscreen: 1);
        ExpectClient(desk, "floatingOne", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingTwo", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingThree", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "tiledOne", floating: false, visible: false, fullscreen: 2);
        ExpectClient(desk, "under", floating: true, visible: false, fullscreen: 0, over: false);

        desk.Ok("focus", "class:tiledOne");
        ExpectClient(desk, "tiledOne", floating: false, visible: true, fullscreen: 2);
        ExpectClient(desk, "floatingOne", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingTwo", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingThree", floating: true, visible: false, fullscreen: 0, over: false);
        ExpectClient(desk, "tiledTwo", floating: false, visible: false, fullscreen: 1);
        ExpectClient(desk, "under", floating: true, visible: false, fullscreen: 0, over: false);
    }

    [TestMethod]
    public void ScrollLAYOUTHANDLEDFocusInDirectionFocusFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("normal1");
        desk.Spawn("fs");
        desk.Spawn("normal2");

        desk.Ok("focus", "class:fs");
        desk.Ok("fullscreen", "on");
        ExpectActiveFullscreen(desk, "fs", 2);

        foreach (var direction in new[] { "r", "l", "u", "d" })
        {
            desk.Ok("focus", direction);
            ExpectActiveFullscreen(desk, "fs", 2);
        }
    }

    [TestMethod]
    public void ScrollLAYOUTHANDLEDFloatingOntopFullscreenWorkspaceFocusRetention() =>
        FloatingOntopFullscreenWorkspaceFocusRetention("");

    [TestMethod]
    public void ScrollDEFAULTHANDLEDFullscreenMaximiseDispatchers()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");

        desk.Ok("focus", "class:kitty_A");
        desk.Ok("fullscreen", "on");
        ExpectFullscreen(desk, 2);

        desk.Ok("fullscreen", "off");
        ExpectFullscreen(desk, 0);

        desk.Ok("maximize");
        ExpectFullscreen(desk, 1);

        desk.Ok("maximize");
        ExpectFullscreen(desk, 0);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDTestFsFocusUnderFSWindow()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "one", "two", "three" })
            desk.Spawn(name);

        desk.Ok("focus", "class:one");
        desk.Ok("maximize", "default");
        desk.ExpectActive("22,22", "1876,1036");
        Assert.AreEqual("one", desk.ActiveClass());

        desk.Spawn("erstarrwashere");
        Assert.AreEqual("erstarrwashere", desk.ActiveClass());
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.ActiveWindow()));
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDNewWindowTakesOverFullscreen()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("kitty_A");
        desk.Ok("fullscreen", "default");
        ExpectActiveFullscreen(desk, "kitty_A", 2);

        desk.Spawn("kitty_B");
        ExpectActiveFullscreen(desk, "kitty_B", 0);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDExitWindowDoesNotRetainFullscreen()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");

        desk.Ok("fullscreen", "default");
        ExpectFullscreen(desk, 2);

        desk.CloseActive();
        ExpectFullscreen(desk, 0);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDFullscreenNonInterference()
    {
        var desk = new TestDesktop(Scrolling);
        string[] names = ["red", "crimson", "blue", "cyan", "azure", "green"];
        foreach (var name in names)
            desk.Spawn(name);

        desk.Ok("focus", "class:red");
        ExpectUnmovedByMaximize(desk, names);

        desk.Ok("focus", "class:red");
        desk.Ok("float");
        ExpectUnmovedByMaximize(desk, names);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDFloatingOntopFullscreenWorkspaceFocusRetention() =>
        FloatingOntopFullscreenWorkspaceFocusRetention(" default");

    [TestMethod]
    public void TestScrollingViewBehaviourDispatchFocusSelectorFollowFocustrue()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");

        desk.Ok("focus", "class:a");

        Assert.IsTrue(ActiveX(desk) >= 0);
    }

    [TestMethod]
    public void TestScrollingViewBehaviourMoveWindowInGroupFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling + "");
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");
        desk.Spawn("c");
        desk.Ok("focus", "class:a");
        desk.Ok("group");

        desk.Ok("focus", "class:b");
        desk.Ok("join", "l");

        Assert.IsTrue(ActiveX(desk) >= 0);
    }

    [TestMethod]
    public void TestScrollingViewBehaviourFocusFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");

        desk.Ok("focus", "l");

        Assert.IsTrue(ActiveX(desk) >= 0);
    }

    [TestMethod]
    public void TestScrollingViewBehaviourFocusInGroupFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling + "binds:movefocus_cycles_groupfirst = true\n");
        desk.Spawn("a");
        desk.Ok("group");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");
        desk.Spawn("c");

        desk.Ok("focus", "class:b");
        desk.Ok("join", "l");
        desk.Ok("focus", "l");

        Assert.IsTrue(ActiveX(desk) >= 0);
    }

    [TestMethod]
    public void TestScrollInhibitor()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");

        desk.Ok("layoutmsg", "inhibit_scroll 1");
        desk.Ok("layoutmsg", "focus l");

        Assert.IsTrue(ActiveX(desk) < 0);
    }

    [TestMethod]
    public void LayoutmsgFitIntoView()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");

        desk.Ok("focus", "class:a");
        desk.Ok("layoutmsg", "fit_into_view");

        Assert.AreEqual("22,22", desk.ActivePlace().At);
    }

    [TestMethod]
    public void LayoutRuleExpand()
    {
        var desk = new TestDesktop(Scrolling + """
            gaps_in = 0
            border_size = 0
            gaps_out = 0
            column_width = 0.5
            """);
        desk.Spawn("a");
        var single = X(desk.ActivePlace().Size);

        desk.Spawn("b");
        desk.Spawn("c");
        desk.Ok("focus", "class:a");
        desk.Ok("resize", "exact 100 500");
        desk.Ok("focus", "class:c");
        desk.Ok("resize", "exact 100 500");
        desk.Ok("focus", "class:b");
        desk.Ok("layoutmsg", "fit expand");

        Assert.IsTrue(X(desk.ActivePlace().Size) >= single - 200);
    }

    [TestMethod]
    public void ProperFocusBehvaior()
    {
        var desk = new TestDesktop(Scrolling);
        desk.AddMonitor("HEADLESS-3", TestDesktop.Second);

        desk.Ok("focus-monitor", "HEADLESS-2");
        desk.Spawn("a");

        desk.Ok("focus-monitor", "HEADLESS-3");
        foreach (var name in new[] { "b", "c", "d" })
        {
            desk.Spawn(name);
            desk.Ok("focus", "class:" + name);
        }

        Assert.AreEqual("d", desk.ActiveClass());
        foreach (var (direction, expected) in new[] { ("l", "c"), ("l", "b"), ("l", "a"), ("r", "b"), ("r", "c"), ("r", "d"), ("l", "c") })
        {
            desk.Ok("focus", direction);
            Assert.AreEqual(expected, desk.ActiveClass());
        }

        desk.Ok("focus-monitor", "HEADLESS-2");
        Assert.AreEqual("a", desk.ActiveClass());

        desk.Ok("focus-monitor", "HEADLESS-3");
        Assert.AreEqual("c", desk.ActiveClass());

        desk.Ok("focus", "l");
        Assert.AreEqual("b", desk.ActiveClass());

        CloseAll(desk);
        Assert.IsEmpty(desk.Clients());
    }

    private static void FloatingOntopFullscreenWorkspaceFocusRetention(string handler)
    {
        var desk = new TestDesktop(Scrolling + "windowrule = float, class:kitty_floated\n");
        desk.Ok("workspace", "1");

        foreach (var mode in new[] { "fullscreen", "maximize" })
        {
            var expected = mode == "fullscreen" ? 2 : 1;
            desk.Spawn("kitty_tiled");
            desk.Ok(mode, "on" + handler);
            Assert.AreEqual("kitty_tiled", desk.ActiveClass());
            Assert.IsFalse(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, expected);

            desk.Spawn("kitty_floated");
            Assert.AreEqual("kitty_floated", desk.ActiveClass());
            Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, 0);

            desk.Ok("workspace", "2");
            desk.Ok("workspace", "1");
            Assert.AreEqual("kitty_floated", desk.ActiveClass());
            Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, 0);

            CloseAll(desk);
            Assert.IsEmpty(desk.Clients());
        }
    }

    private static void CloseAll(TestDesktop desk)
    {
        foreach (var client in desk.Clients())
        {
            var address = client.GetProperty("address").GetString() ?? "";
            desk.Handle(new WindowClosed(new WindowId(Convert.ToInt64(address[2..], 16))));
        }
    }

    private static int ActiveX(TestDesktop desk) => X(desk.ActivePlace().At);

    private static int X(string pair) => int.Parse(pair[..pair.IndexOf(',', StringComparison.Ordinal)], CultureInfo.InvariantCulture);

    private static void ExpectActive(TestDesktop desk, string windowClass, string at, string size)
    {
        Assert.AreEqual(windowClass, desk.ActiveClass());
        desk.ExpectActive(at, size);
    }

    private static void ExpectPlace(TestDesktop desk, string windowClass, string at, string size) =>
        Assert.AreEqual((at, size), desk.PlaceOf(windowClass));

    private static void ExpectFullscreen(TestDesktop desk, int fullscreen) =>
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(desk.ActiveWindow()));

    private static void ExpectActiveFullscreen(TestDesktop desk, string windowClass, int mode)
    {
        Assert.AreEqual(windowClass, desk.ActiveClass());
        ExpectFullscreen(desk, mode);
    }

    private static void ExpectClient(TestDesktop desk, string windowClass, bool floating, bool visible, int fullscreen, bool? over = null)
    {
        var client = desk.Client(windowClass);
        Assert.AreEqual(floating, TestDesktop.Floating(client));
        if (over is bool allowed)
            Assert.AreEqual(allowed, client.GetProperty("allowedOverFullscreen").GetBoolean());
        Assert.AreEqual(visible, client.GetProperty("visible").GetBoolean());
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(client));
    }

    private static void ExpectUnmovedByMaximize(TestDesktop desk, string[] names)
    {
        var before = new Dictionary<string, (string At, string Size)>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            desk.Ok("focus", "class:" + name);
            before[name] = desk.ActivePlace();
        }

        desk.Ok("focus", "class:red");
        desk.Ok("maximize", "default");
        desk.Ok("maximize", "default");

        foreach (var name in names)
        {
            desk.Ok("focus", "class:" + name);
            Assert.AreEqual(before[name], desk.ActivePlace());
        }
    }
}
