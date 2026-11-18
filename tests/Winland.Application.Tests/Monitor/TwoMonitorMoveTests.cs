using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class TwoMonitorMoveTests
{
    private const string Left = "HEADLESS-2";

    private const string Right = "HYPRTEST-REFACTOR-R";

    [TestMethod]
    public void WorkspacePlacementActiveMove()
    {
        var desk = Prepare(new TestDesktop());

        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "270");
        desk.Spawn("workspace_move_active");

        desk.Ok("focus-monitor", Right);
        desk.Ok("workspace", "271");

        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "270");

        desk.Ok("move-workspace", Right);

        desk.Ok("focus-monitor", Right);
        ExpectActiveWorkspace(desk, 270, Right);

        desk.Ok("focus-monitor", Left);
        var active = desk.ActiveWorkspace();
        Assert.AreNotEqual(270, active.GetProperty("id").GetInt32());
        Assert.AreEqual(Left, active.GetProperty("monitor").GetString());
    }

    [TestMethod]
    public void WorkspacePlacementWindowState()
    {
        var desk = Prepare(new TestDesktop());
        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "280");

        desk.Spawn("workspace_move_pinned");
        desk.Ok("float");
        desk.Ok("pin");

        desk.Spawn("workspace_move_float");
        desk.Ok("float");
        desk.Place(desk.ActiveId(), 200, 200, 400, 300);

        desk.Spawn("workspace_move_fullscreen");
        desk.Ok("fullscreen", "on");

        desk.Ok("move-workspace", Right);

        desk.Ok("focus-monitor", Left);
        var pinned = desk.Client("workspace_move_pinned");
        Assert.IsTrue(TestDesktop.Pinned(pinned));
        Assert.IsTrue(TestDesktop.Floating(pinned));
        Assert.IsTrue(pinned.GetProperty("visible").GetBoolean());

        desk.Ok("focus-monitor", Right);
        desk.Ok("focus", "class:workspace_move_float");
        Assert.AreEqual("workspace_move_float", desk.ActiveClass());
        Assert.AreEqual("2120,200", desk.ActivePlace().At);

        desk.Ok("focus", "class:workspace_move_fullscreen");
        Assert.AreEqual("workspace_move_fullscreen", desk.ActiveClass());
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
        desk.ExpectActive("1920,0", "1920,1080");
    }

    [TestMethod]
    public void GlobalWindowMoveState()
    {
        var desk = Prepare(new TestDesktop());

        desk.Ok("focus-monitor", Right);
        desk.Ok("workspace", "291");

        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "290");
        desk.Spawn("global_move_fullscreen");
        desk.Ok("fullscreen", "on");
        desk.Ok("move", $"mon:{Right} stay");

        desk.Ok("focus-monitor", Right);
        desk.Ok("focus", "class:global_move_fullscreen");
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.ActiveWindow()));

        desk.Ok("workspace", "294");

        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "292");
        desk.Spawn("global_move_float");
        desk.Ok("float");
        desk.Place(desk.ActiveId(), 200, 200, 400, 300);

        desk.Ok("move", $"mon:{Right}");
        Assert.AreEqual("global_move_float", desk.ActiveClass());
        Assert.AreEqual("2120,200", desk.ActivePlace().At);

        desk.Ok("workspace", "293");
        desk.Spawn("global_move_group_anchor");
        desk.Ok("group");

        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "295");
        desk.Spawn("global_move_group_member");
        desk.Ok("move", $"mon:{Right}");
        Assert.AreEqual("global_move_group_member", desk.ActiveClass());
    }

    [TestMethod]
    public void PointerWithoutWarpFocusesMonitorOnly()
    {
        var desk = Prepare(new TestDesktop("warp_cursor = false\n"));

        desk.Ok("focus-monitor", Right);
        desk.Ok("workspace", "301");
        desk.Ok("focus-monitor", Left);
        desk.Ok("workspace", "300");

        desk.MovePointer(100, 100);
        var original = CursorPos(desk);

        desk.Ok("focus-monitor", Right);
        Assert.AreEqual(original, CursorPos(desk));
        ExpectActiveWorkspace(desk, 301, Right);

        desk.MovePointer(2000, 10);
        Assert.AreNotEqual(original, CursorPos(desk));
    }

    private static TestDesktop Prepare(TestDesktop desk)
    {
        desk.AddMonitor(Right, TestDesktop.Second);
        return desk;
    }

    private static void ExpectActiveWorkspace(TestDesktop desk, int id, string monitor)
    {
        var active = desk.ActiveWorkspace();
        Assert.AreEqual(id, active.GetProperty("id").GetInt32());
        Assert.AreEqual(monitor, active.GetProperty("monitor").GetString());
    }

    private static string CursorPos(TestDesktop desk) => $"{desk.Pointer.X}, {desk.Pointer.Y}";
}
