namespace Winland.Application.Tests;

[TestClass]
public class LayoutChangeTests
{
    [TestMethod]
    public void LayoutRefreshDuringMonitorTeardown()
    {
        var desk = new TestDesktop();

        desk.AddMonitor("HEADLESS-5", TestDesktop.Second);

        desk.Ok("focus-monitor", "HEADLESS-5");
        desk.Ok("workspace", "30");
        desk.Spawn("layout_teardown_a");
        desk.Ok("workspace", "31");
        desk.Spawn("layout_teardown_b");

        desk.Ok("focus-monitor", "HEADLESS-2");
        desk.Ok("workspace", "1");

        desk.Set("layout", "scrolling");

        Assert.Contains("HEADLESS-5", desk.Send("get monitors all"), StringComparison.Ordinal);
    }

    [TestMethod]
    public void PosPreserve()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty");

        desk.Ok("focus", "class:kitty");
        desk.Ok("float");
        desk.Place("kitty", 420, 420, 1337, 69);
        desk.ExpectActive("420,420", "1337,69");

        desk.Ok("fullscreen");
        desk.Ok("fullscreen");
        Assert.AreEqual("1337,69", desk.ActivePlace().Size);

        desk.Ok("move", "r");
        desk.ExpectActive("581,420", "1337,69");

        desk.Ok("fullscreen");
        desk.Ok("fullscreen");
        desk.ExpectActive("581,420", "1337,69");

        desk.Ok("workspace", "1");
        desk.Ok("focus", "class:kitty");
        desk.Ok("pin", "on");
        desk.Ok("workspace", "2");
        desk.Ok("fullscreen");
        desk.Ok("fullscreen");
        desk.ExpectActive("581,420", "1337,69");
    }

    [TestMethod]
    public void FocusPreservedLayoutChange()
    {
        var desk = new TestDesktop("layout = dwindle\n");
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");
        desk.Spawn("kitty_C");
        desk.Spawn("kitty_D");

        desk.Ok("focus", "class:kitty_C");
        desk.Set("layout", "scrolling");

        Assert.AreEqual("kitty_C", desk.ActiveClass());
    }
}
