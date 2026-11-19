namespace Winland.Application.Tests;

[TestClass]
public class FocusAcrossWorkspacesTests
{
    private const string NoWindow = "null";

    [TestMethod]
    public void CrossMonitorFullscreenFocus()
    {
        var desk = new TestDesktop();
        desk.AddMonitor("HYPRTEST-2", TestDesktop.Second);

        desk.Ok("workspace", "1");
        desk.Spawn("1A");
        desk.Ok("focus", "class:1A");
        var source = desk.ActiveWindow().GetProperty("monitor").GetString();

        desk.Ok("focus-monitor", "HYPRTEST-2");
        desk.Ok("workspace", "2");
        desk.Spawn("2A");
        desk.Spawn("2B");
        desk.Ok("focus", "class:2B");
        var destination = desk.ActiveWindow().GetProperty("monitor").GetString();

        Assert.AreNotEqual(source, destination);

        desk.Ok("focus", "class:2B");
        desk.Ok("float");
        desk.Ok("fullscreen");

        desk.Ok("focus", "class:1A");
        desk.Ok("focus", "r");

        Assert.AreEqual("2B", desk.ActiveClass());
    }

    [TestMethod]
    public void AMoveThatStaysUnfocusesTheLastTiledWindow()
    {
        var desk = new TestDesktop();
        desk.Ok("workspace", "1");
        Assert.IsEmpty(desk.Clients());

        desk.Spawn("silent_move_last_tiled");
        Assert.ContainsSingle(desk.Clients());

        var active = desk.ActiveWindow();
        Assert.AreEqual("silent_move_last_tiled", TestDesktop.ClassOf(active));
        Assert.AreEqual(1, TestDesktop.WorkspaceId(active));
        Assert.IsFalse(TestDesktop.Floating(active));

        desk.Ok("move", "2 stay");

        Assert.AreEqual(1, desk.ActiveWorkspace().GetProperty("id").GetInt32());
        Assert.AreEqual(NoWindow, desk.Send("j/get window"));
        Assert.ContainsSingle(desk.Clients());

        var client = desk.Client("silent_move_last_tiled");
        Assert.AreEqual(2, TestDesktop.WorkspaceId(client));
    }

    [TestMethod]
    public void CrossMonitorEmptyWorkspaceUnfocusesWindow()
    {
        const string TestOutput = "HYPRTEST-UNFOCUS";
        var desk = new TestDesktop();
        desk.AddMonitor(TestOutput, TestDesktop.Second);

        desk.Ok("focus-monitor", "HEADLESS-2");
        desk.Ok("workspace", "1");
        Assert.IsEmpty(desk.Clients());

        desk.Ok("focus-monitor", TestOutput);
        desk.Ok("workspace", "2");
        desk.Spawn("cross_monitor_ws2");
        Assert.AreEqual("cross_monitor_ws2", desk.ActiveClass());

        desk.Ok("workspace", "1");
        Assert.AreEqual(1, desk.ActiveWorkspace().GetProperty("id").GetInt32());
        Assert.AreEqual(NoWindow, desk.Send("j/get window"));
    }
}
