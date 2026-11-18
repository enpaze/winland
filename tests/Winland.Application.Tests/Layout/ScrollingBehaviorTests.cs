using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class ScrollingBehaviorTests
{
    private const string Scrolling = "general:layout = scrolling\n";

    [TestMethod]
    public void ScrollFocusCycling()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "a", "b", "c", "d" })
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:a");
        desk.Ok("movefocus", "r");
        Assert.AreEqual("b", desk.ActiveClass());

        desk.Ok("movefocus", "r");
        Assert.AreEqual("c", desk.ActiveClass());

        desk.Ok("movefocus", "r");
        Assert.AreEqual("d", desk.ActiveClass());

        desk.Ok("movewindow", "l");
        Assert.AreEqual("d", desk.ActiveClass());

        desk.Ok("movefocus", "u");
        Assert.AreEqual("c", desk.ActiveClass());
    }

    [TestMethod]
    public void ScrollFocusWrapping()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "a", "b", "c", "d" })
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:a");
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

        desk.Ok("focuswindow", "class:a");
        desk.Ok("layoutmsg", "swapcol l");
        desk.Ok("layoutmsg", "focus l");
        Assert.AreEqual("c", desk.ActiveClass());

        CloseAll(desk);
        foreach (var name in names)
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:d");
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

        desk.Ok("focuswindow", "class:kitty_scroll_B");
        desk.Ok("fullscreen", "0");
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

        desk.Ok("focuswindow", "class:kitty_scroll_B");
        desk.Ok("fullscreen", "1");
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
        desk.Ok("focuswindow", "class:under");
        desk.Ok("togglefloating");

        desk.Spawn("tiledOne");
        desk.Ok("fullscreen", "0");
        ExpectClient(desk, "under", floating: true, acceptsInput: false, fullscreen: 0, over: false, visible: false);

        desk.Spawn("tiledTwo");
        desk.Ok("fullscreen", "1");

        desk.Ok("focuswindow", "class:tiledOne");
        ExpectClient(desk, "tiledOne", floating: false, acceptsInput: true, fullscreen: 2, visible: true);
        ExpectClient(desk, "under", floating: true, acceptsInput: false, fullscreen: 0, over: false);

        desk.Set("windowrule", "float, class:floating*");
        desk.Set("windowrule", "size 950 500, class:floating*");
        desk.Spawn("floatingOne");
        desk.Spawn("floatingTwo");
        ExpectClient(desk, "floatingOne", floating: true, acceptsInput: true, fullscreen: 0, over: true);
        ExpectClient(desk, "floatingTwo", floating: true, acceptsInput: true, fullscreen: 0, over: true);

        desk.Ok("focuswindow", "class:floatingOne");
        desk.Ok("fullscreen", "1");
        ExpectClient(desk, "floatingOne", floating: true, acceptsInput: true, fullscreen: 1, over: true);
        ExpectClient(desk, "floatingTwo", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "tiledOne", floating: false, acceptsInput: false, fullscreen: 2);
        ExpectClient(desk, "tiledTwo", floating: false, acceptsInput: false, fullscreen: 1);
        ExpectClient(desk, "under", floating: true, acceptsInput: false, fullscreen: 0, over: false);

        desk.Spawn("floatingThree");
        ExpectClient(desk, "floatingThree", floating: true, acceptsInput: true, fullscreen: 0, over: true);

        desk.Ok("focuswindow", "class:floatingOne");
        desk.Ok("fullscreen", "1 unset");
        ExpectClient(desk, "floatingOne", floating: true, acceptsInput: true, fullscreen: 0, over: true);
        ExpectClient(desk, "floatingTwo", floating: true, acceptsInput: true, fullscreen: 0, over: true);
        ExpectClient(desk, "floatingThree", floating: true, acceptsInput: true, fullscreen: 0, over: true);
        ExpectClient(desk, "tiledOne", floating: false, acceptsInput: true, fullscreen: 2);
        ExpectClient(desk, "tiledTwo", floating: false, acceptsInput: false, fullscreen: 1);
        ExpectClient(desk, "under", floating: true, acceptsInput: false, fullscreen: 0, over: false);

        desk.Ok("focuswindow", "class:tiledTwo");
        ExpectClient(desk, "tiledTwo", floating: false, acceptsInput: true, fullscreen: 1);
        ExpectClient(desk, "floatingOne", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingTwo", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingThree", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "tiledOne", floating: false, acceptsInput: false, fullscreen: 2);
        ExpectClient(desk, "under", floating: true, acceptsInput: false, fullscreen: 0, over: false);

        desk.Ok("focuswindow", "class:tiledOne");
        ExpectClient(desk, "tiledOne", floating: false, acceptsInput: true, fullscreen: 2);
        ExpectClient(desk, "floatingOne", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingTwo", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "floatingThree", floating: true, acceptsInput: false, fullscreen: 0, over: false);
        ExpectClient(desk, "tiledTwo", floating: false, acceptsInput: false, fullscreen: 1);
        ExpectClient(desk, "under", floating: true, acceptsInput: false, fullscreen: 0, over: false);
    }

    [TestMethod]
    public void ScrollLAYOUTHANDLEDFocusInDirectionFocusFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("normal1");
        desk.Spawn("fs");
        desk.Spawn("normal2");

        desk.Ok("focuswindow", "class:fs");
        desk.Ok("fullscreen", "0 set");
        ExpectActiveFullscreen(desk, "fs", 2);

        foreach (var direction in new[] { "r", "l", "u", "d" })
        {
            desk.Ok("movefocus", direction);
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

        desk.Ok("focuswindow", "class:kitty_A");
        desk.Ok("fullscreen", "0 set");
        ExpectFullscreen(desk, 2, 2);

        desk.Ok("fullscreen", "0 unset");
        ExpectFullscreen(desk, 0, 0);

        desk.Ok("fullscreen", "1 toggle");
        ExpectFullscreen(desk, 1, 1);

        desk.Ok("fullscreen", "1 toggle");
        ExpectFullscreen(desk, 0, 0);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDTestFsFocusUnderFSWindow()
    {
        var desk = new TestDesktop(Scrolling);
        foreach (var name in new[] { "one", "two", "three" })
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:one");
        desk.Ok("fullscreen", "1 default");
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
        desk.Ok("fullscreen", "0 default");
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

        desk.Ok("fullscreen", "0 default");
        ExpectFullscreen(desk, 2, 2);

        desk.CloseActive();
        ExpectFullscreen(desk, 0, 0);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDFullscreenNonInterference()
    {
        var desk = new TestDesktop(Scrolling);
        string[] names = ["red", "crimson", "blue", "cyan", "azure", "green"];
        foreach (var name in names)
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:red");
        ExpectUnmovedByMaximize(desk, names);

        desk.Ok("focuswindow", "class:red");
        desk.Ok("togglefloating");
        ExpectUnmovedByMaximize(desk, names);
    }

    [TestMethod]
    public void ScrollDEFAULTHANDLEDFloatingOntopFullscreenWorkspaceFocusRetention() =>
        FloatingOntopFullscreenWorkspaceFocusRetention(" default");

    [TestMethod]
    public void TestScrollingViewBehaviourDispatchFocusWindowFollowFocustrue()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");

        desk.Ok("focuswindow", "class:a");

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
        desk.Ok("focuswindow", "class:a");
        desk.Ok("togglegroup");

        desk.Ok("focuswindow", "class:b");
        desk.Ok("moveintogroup", "l");

        Assert.IsTrue(ActiveX(desk) >= 0);
    }

    [TestMethod]
    public void TestScrollingViewBehaviourMoveFocusFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling);
        desk.Spawn("a");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");

        desk.Ok("movefocus", "l");

        Assert.IsTrue(ActiveX(desk) >= 0);
    }

    [TestMethod]
    public void TestScrollingViewBehaviourMoveFocusInGroupFollowFocusTrue()
    {
        var desk = new TestDesktop(Scrolling + "binds:movefocus_cycles_groupfirst = true\n");
        desk.Spawn("a");
        desk.Ok("togglegroup");
        desk.Ok("layoutmsg", "colresize 0.8");
        desk.Spawn("b");
        desk.Spawn("c");

        desk.Ok("focuswindow", "class:b");
        desk.Ok("moveintogroup", "l");
        desk.Ok("movefocus", "l");

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

        desk.Ok("focuswindow", "class:a");
        desk.Ok("layoutmsg", "fit_into_view");

        Assert.AreEqual("22,22", desk.ActivePlace().At);
    }

    [TestMethod]
    public void LayoutRuleExpand()
    {
        var desk = new TestDesktop(Scrolling + """
            general:gaps_in = 0
            general:border_size = 0
            general:gaps_out = 0
            scrolling:column_width = 0.5
            """);
        desk.Spawn("a");
        var single = X(desk.ActivePlace().Size);

        desk.Spawn("b");
        desk.Spawn("c");
        desk.Ok("focuswindow", "class:a");
        desk.Ok("resizeactive", "exact 100 500");
        desk.Ok("focuswindow", "class:c");
        desk.Ok("resizeactive", "exact 100 500");
        desk.Ok("focuswindow", "class:b");
        desk.Ok("layoutmsg", "fit expand");

        Assert.IsTrue(X(desk.ActivePlace().Size) >= single - 200);
    }

    [TestMethod]
    public void ProperFocusBehvaior()
    {
        var desk = new TestDesktop(Scrolling);
        desk.AddMonitor("HEADLESS-3", TestDesktop.Second);

        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Spawn("a");

        desk.Ok("focusmonitor", "HEADLESS-3");
        foreach (var name in new[] { "b", "c", "d" })
        {
            desk.Spawn(name);
            desk.Ok("focuswindow", "class:" + name);
        }

        Assert.AreEqual("d", desk.ActiveClass());
        foreach (var (direction, expected) in new[] { ("l", "c"), ("l", "b"), ("l", "a"), ("r", "b"), ("r", "c"), ("r", "d"), ("l", "c") })
        {
            desk.Ok("movefocus", direction);
            Assert.AreEqual(expected, desk.ActiveClass());
        }

        desk.Ok("focusmonitor", "HEADLESS-2");
        Assert.AreEqual("a", desk.ActiveClass());

        desk.Ok("focusmonitor", "HEADLESS-3");
        Assert.AreEqual("c", desk.ActiveClass());

        desk.Ok("movefocus", "l");
        Assert.AreEqual("b", desk.ActiveClass());

        CloseAll(desk);
        Assert.IsEmpty(desk.Clients());
    }

    private static void FloatingOntopFullscreenWorkspaceFocusRetention(string handler)
    {
        var desk = new TestDesktop(Scrolling + "windowrule = float, class:kitty_floated\n");
        desk.Ok("workspace", "1");

        foreach (var mode in new[] { "0", "1" })
        {
            var expected = mode == "0" ? 2 : 1;
            desk.Spawn("kitty_tiled");
            desk.Ok("fullscreen", mode + " set" + handler);
            Assert.AreEqual("kitty_tiled", desk.ActiveClass());
            Assert.IsFalse(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, expected, expected);

            desk.Spawn("kitty_floated");
            Assert.AreEqual("kitty_floated", desk.ActiveClass());
            Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, 0, 0);

            desk.Ok("workspace", "2");
            desk.Ok("workspace", "1");
            Assert.AreEqual("kitty_floated", desk.ActiveClass());
            Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, 0, 0);

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

    private static void ExpectFullscreen(TestDesktop desk, int fullscreen, int fullscreenClient)
    {
        var active = desk.ActiveWindow();
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(active));
        Assert.AreEqual(fullscreenClient, TestDesktop.FullscreenClient(active));
    }

    private static void ExpectActiveFullscreen(TestDesktop desk, string windowClass, int mode)
    {
        Assert.AreEqual(windowClass, desk.ActiveClass());
        ExpectFullscreen(desk, mode, mode);
    }

    private static void ExpectClient(TestDesktop desk, string windowClass, bool floating, bool acceptsInput, int fullscreen, bool? over = null, bool? visible = null)
    {
        var client = desk.Client(windowClass);
        Assert.AreEqual(floating, TestDesktop.Floating(client));
        if (over is bool allowed)
            Assert.AreEqual(allowed, client.GetProperty("allowedOverFullscreen").GetBoolean());
        if (visible is bool shown)
            Assert.AreEqual(shown, client.GetProperty("visible").GetBoolean());
        Assert.AreEqual(acceptsInput, client.GetProperty("acceptsInput").GetBoolean());
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(client));
        Assert.AreEqual(fullscreen, TestDesktop.FullscreenClient(client));
    }

    private static void ExpectUnmovedByMaximize(TestDesktop desk, string[] names)
    {
        var before = new Dictionary<string, (string At, string Size)>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            desk.Ok("focuswindow", "class:" + name);
            before[name] = desk.ActivePlace();
        }

        desk.Ok("focuswindow", "class:red");
        desk.Ok("fullscreen", "1 default");
        desk.Ok("fullscreen", "1 default");

        foreach (var name in names)
        {
            desk.Ok("focuswindow", "class:" + name);
            Assert.AreEqual(before[name], desk.ActivePlace());
        }
    }
}
