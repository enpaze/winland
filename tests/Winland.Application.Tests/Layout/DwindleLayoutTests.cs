namespace Winland.Application.Tests;

[TestClass]
public class DwindleLayoutTests
{
    [TestMethod]
    public void DwindleFloatClamp()
    {
        var desk = new TestDesktop();
        foreach (var name in new[] { "a", "b", "c" })
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:c");
        desk.Ok("focuswindow", "class:c");
        desk.Ok("togglefloating");
        desk.Place("c", 360, 90, 1200, 900);
        desk.Ok("focuswindow", "class:c");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:c");
        desk.Ok("togglefloating");

        Assert.AreEqual("1200,900", desk.PlaceOf("c").Size);
    }

    [TestMethod]
    [DataRow(300, true)]
    [DataRow(1600, false)]
    public void DwindleRetileSplitsAtThePointer(int x, bool left)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("focuswindow", "class:a");
        desk.Ok("togglefloating");
        desk.MovePointer(x, 540);

        desk.Ok("focuswindow", "class:a");
        desk.Ok("togglefloating");

        Assert.AreEqual(left, desk.Placed("a").X < desk.Placed("b").X);
    }

    [TestMethod]
    [DataRow("a", "-100 0", -100, 0)]
    [DataRow("a", "100 0", 100, 0)]
    [DataRow("b", "-100 0", -100, 0)]
    [DataRow("b", "100 0", 100, 0)]
    public void DwindleResizeActiveChangesTheActiveTilesOwnSize(string active, string delta, int width, int height)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("focuswindow", $"class:{active}");
        var before = desk.Placed(active);

        desk.Ok("resizeactive", delta);

        var after = desk.Placed(active);
        Assert.AreEqual((width, height), (after.Width - before.Width, after.Height - before.Height));
    }

    [TestMethod]
    public void DwindleResizeActiveExactGivesASecondTileThatSize()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("focuswindow", "class:b");
        var height = desk.Placed("b").Height;

        desk.Ok("resizeactive", "exact 600 " + height);

        Assert.AreEqual(600, desk.Placed("b").Width);
    }

    [TestMethod]
    public void DwindleIssue13349()
    {
        var desk = new TestDesktop();
        foreach (var name in new[] { "a", "b", "c" })
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:c");
        desk.ExpectActive("967,547", "931,511");

        desk.Ok("movewindow", "l");
        desk.ExpectActive("22,547", "931,511");

        desk.Ok("movewindow", "r");
        desk.ExpectActive("967,547", "931,511");
    }

    [TestMethod]
    public void DwindleSplit()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        Assert.AreNotEqual("ok", desk.Dispatch("layoutmsg", "togglesplit"));

        desk.Spawn("b");
        desk.Ok("focuswindow", "class:a");
        desk.ExpectActive("22,22", "931,1036");

        desk.Ok("layoutmsg", "togglesplit");
        desk.ExpectActive("22,22", "1876,511");
    }

    [TestMethod]
    public void DwindleMoveTowardTheEdgeItIsAtMovesNothing()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Spawn("c");
        var before = new[] { desk.Placed("a"), desk.Placed("b"), desk.Placed("c") };

        desk.Ok("focuswindow", "class:a");
        desk.Dispatch("movewindow", "l");

        Assert.AreSequenceEqual(before, new[] { desk.Placed("a"), desk.Placed("b"), desk.Placed("c") });
    }

    [TestMethod]
    public void DwindleMoveAcrossToggledSplit()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("layoutmsg", "togglesplit");

        var b = desk.ActivePlace();
        desk.Ok("focuswindow", "class:a");
        desk.Ok("movewindow", "d");

        Assert.AreEqual(b, desk.ActivePlace());
    }

    [TestMethod]
    public void DwindleFullscreenMaximiseDispatchers()
    {
        var desk = new TestDesktop();
        desk.Set("general:layout", "dwindle");
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");

        desk.Ok("focuswindow", "class:kitty_A");
        desk.Ok("fullscreen", "0 set");
        ExpectFullscreen(desk, 2);

        desk.Ok("fullscreen", "0 unset");
        ExpectFullscreen(desk, 0);

        desk.Ok("fullscreen", "1 toggle");
        ExpectFullscreen(desk, 1);

        desk.Ok("fullscreen", "1 toggle");
        ExpectFullscreen(desk, 0);
    }

    [TestMethod]
    public void DwindleTestFsFocusUnderFSWindow()
    {
        var desk = new TestDesktop();
        foreach (var name in new[] { "one", "two", "three" })
            desk.Spawn(name);

        desk.Ok("focuswindow", "class:one");
        desk.Ok("fullscreen", "1");
        desk.ExpectActive("22,22", "1876,1036");
        Assert.AreEqual("one", desk.ActiveClass());

        desk.Spawn("erstarrwashere");
        Assert.AreEqual("erstarrwashere", desk.ActiveClass());
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.ActiveWindow()));
    }

    [TestMethod]
    public void DwindleNewWindowTakesOverFullscreen()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Ok("fullscreen", "0");
        ExpectFullscreen(desk, 2);
        Assert.AreEqual("kitty_A", desk.ActiveClass());

        desk.Spawn("kitty_B");
        ExpectFullscreen(desk, 0);
        Assert.AreEqual("kitty_B", desk.ActiveClass());
    }

    [TestMethod]
    public void DwindleExitWindowDoesNotRetainFullscreen()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");

        desk.Ok("fullscreen", "0");
        ExpectFullscreen(desk, 2);

        desk.CloseActive();
        ExpectFullscreen(desk, 0);
    }

    [TestMethod]
    public void DwindleFullscreenNonInterference()
    {
        var desk = new TestDesktop();
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
    public void DefaultHandledFsfocusInDirection()
    {
        var desk = new TestDesktop();
        desk.Spawn("normal1");
        desk.Spawn("fs");
        desk.Spawn("normal2");

        desk.Ok("focuswindow", "class:fs");
        desk.Ok("fullscreen", "0 set");

        foreach (var direction in new[] { "r", "l", "u", "d" })
        {
            desk.Ok("movefocus", direction);
            Assert.AreEqual("fs", desk.ActiveClass());
            ExpectFullscreen(desk, 2);
        }
    }

    [TestMethod]
    public void DwindleFloatingOntopFullscreenWorkspaceFocusRetention()
    {
        foreach (var mode in new[] { "0", "1" })
        {
            var desk = new TestDesktop("windowrule = float, class:kitty_floated\n");
            desk.Ok("workspace", "1");
            desk.Spawn("kitty_tiled");

            desk.Ok("fullscreen", mode + " set");
            Assert.AreEqual("kitty_tiled", desk.ActiveClass());
            Assert.IsFalse(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, mode == "0" ? 2 : 1);

            desk.Spawn("kitty_floated");
            Assert.AreEqual("kitty_floated", desk.ActiveClass());
            Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, 0);

            desk.Ok("workspace", "2");
            desk.Ok("workspace", "1");
            Assert.AreEqual("kitty_floated", desk.ActiveClass());
            Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));
            ExpectFullscreen(desk, 0);
        }
    }

    private static void ExpectAny(TestDesktop desk, string at, string size)
    {
        var places = desk.Clients().Select(client => desk.PlaceOf(TestDesktop.ClassOf(client))).ToList();
        Assert.Contains(place => place.At == at, places);
        Assert.Contains(place => place.Size == size, places);
    }

    private static void ExpectFullscreen(TestDesktop desk, int fullscreen) =>
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(desk.ActiveWindow()));

    private static void ExpectUnmovedByMaximize(TestDesktop desk, string[] names)
    {
        var before = names.ToDictionary(name => name, desk.PlaceOf);
        desk.Ok("focuswindow", "class:red");
        desk.Ok("fullscreen", "1");
        desk.Ok("fullscreen", "1");
        foreach (var name in names)
            Assert.AreEqual(before[name], desk.PlaceOf(name));
    }
}
