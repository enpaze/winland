using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class GroupBehaviorTests
{
    [TestMethod]
    public void GroupDimensions()
    {
        var desk = new TestDesktop();
        desk.Ok("workspace", "2");
        desk.Spawn("kittyA");

        Assert.AreEqual(1, CountAt(desk, "22,22"));
        Assert.AreEqual(1, CountSize(desk, "1876,1036"));
        Assert.AreEqual(1, desk.Clients().Count(client => TestDesktop.Fullscreen(client) == 0));

        desk.Spawn("kittyB");
        desk.Ok("focus", "class:kittyB");
        desk.Ok("group");
        desk.Ok("focus", "class:kittyA");
        desk.Ok("group");

        Assert.AreEqual("931,1015", desk.ActivePlace().Size);
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.ActiveWindow()));

        desk.Spawn("kittyC");
        Assert.AreEqual("931,1015", desk.ActivePlace().Size);
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.ActiveWindow()));

        desk.CloseAll();
        desk.Spawn("a");
        Assert.ContainsSingle(desk.Clients());
        Assert.AreEqual(1, CountAt(desk, "22,22"));
        Assert.AreEqual(1, CountSize(desk, "1876,1036"));
        Assert.AreEqual(1, desk.Clients().Count(client => TestDesktop.Fullscreen(client) == 0));

        desk.Ok("group");
        Assert.AreEqual(("22,43", "1876,1015"), desk.PlaceOf("a"));
    }

    [TestMethod]
    public void GroupNext()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Ok("group");
        desk.Spawn("b");
        Assert.AreEqual(2, desk.Clients().Length);

        var last = desk.ActiveId();
        desk.Ok("tab", "next");
        Assert.AreNotEqual(last, desk.ActiveId());

        desk.Dispatch("tab", "next");
        Assert.AreEqual(last, desk.ActiveId());
    }

    [TestMethod]
    public void GroupsNoCrash()
    {
        var desk = new TestDesktop();
        desk.Spawn("kittyA");
        desk.Ok("group");
        desk.Ok("group");

        Assert.AreEqual("kittyA", desk.ActiveClass());
    }

    [TestMethod]
    public void AutoGroupFloatedIntoTiled()
    {
        var desk = new TestDesktop("""
            windowrule = tile, class:kitty_tiled
            windowrule = float, class:kitty_floated
            """);
        desk.Spawn("kitty_tiled");
        desk.Ok("group");
        desk.Spawn("kitty_floated");
        Assert.AreEqual(2, desk.Clients().Length);

        var floated = desk.Client("kitty_floated");
        Assert.IsTrue(TestDesktop.Floating(floated));
        Assert.AreEqual(0, Grouped(floated));
    }

    [TestMethod]
    public void AutoGroupFloatedintoFloated()
    {
        var desk = new TestDesktop("""
            windowrule = float, class:kitty_floated_A
            windowrule = float, class:kitty_floated_B
            """);
        desk.Spawn("kitty_floated_A");
        desk.Ok("group");
        desk.Spawn("kitty_floated_B");
        Assert.AreEqual(2, desk.Clients().Length);

        var b = desk.Client("kitty_floated_B");
        Assert.IsTrue(TestDesktop.Floating(b));
        Assert.AreNotEqual(0, Grouped(b));
    }

    [TestMethod]
    public void AutoGroupTiledIntoFloated()
    {
        var desk = new TestDesktop("""
            windowrule = float, class:kitty_floated_A
            windowrule = tile, class:kitty_tiled_B
            """);
        desk.Spawn("kitty_floated_A");
        Assert.IsTrue(TestDesktop.Floating(desk.ActiveWindow()));

        desk.Ok("group");
        desk.Spawn("kitty_tiled_B");
        Assert.AreEqual(2, desk.Clients().Length);

        var b = desk.Client("kitty_tiled_B");
        Assert.IsTrue(TestDesktop.Floating(b));
        Assert.AreNotEqual(0, Grouped(b));
    }

    [TestMethod]
    public void GroupedExitWindowDoesNotRetainFullscreen()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Ok("group");
        desk.Spawn("kitty_B");

        desk.Ok("fullscreen");
        ExpectFullscreen(desk, 2);

        desk.CloseActive();
        Assert.ContainsSingle(desk.Clients());
        ExpectFullscreen(desk, 0);
    }

    private static int CountAt(TestDesktop desk, string at) =>
        desk.Clients().Count(client => desk.PlaceOf(TestDesktop.ClassOf(client)).At == at);

    private static int CountSize(TestDesktop desk, string size) =>
        desk.Clients().Count(client => desk.PlaceOf(TestDesktop.ClassOf(client)).Size == size);

    private static int Grouped(JsonElement client) => client.GetProperty("grouped").GetArrayLength();


    private static void ExpectFullscreen(TestDesktop desk, int fullscreen) =>
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(desk.ActiveWindow()));
}
