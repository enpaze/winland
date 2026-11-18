using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class WorkspaceDispatchTests
{
    [TestMethod]
    public void WorkspacesCombined()
    {
        var desk = new TestDesktop();
        desk.ReloadWithoutExtra();

        desk.Ok("workspace", "1");
        desk.Spawn("a");
        desk.Ok("workspace", "3");
        desk.Spawn("b");
        desk.Ok("workspace", "1");

        desk.Ok("workspace", "+1");
        Assert.AreEqual("2 (2)", ActiveLabel(desk));
        Assert.Contains("3 (3)", Labels(desk));
        Assert.Contains("1 (1)", Labels(desk));

        desk.Ok("workspace", "1");
        Assert.DoesNotContain("2 (2)", Labels(desk));

        desk.Ok("workspace", "m+1");
        Assert.AreEqual("3 (3)", ActiveLabel(desk));

        desk.Ok("workspace", "-1");
        Assert.AreEqual("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "1");
        desk.Ok("workspace", "r+1");
        Assert.AreEqual("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "r+1");
        Assert.AreEqual("3 (3)", ActiveLabel(desk));

        desk.Ok("workspace", "r~1");
        Assert.AreEqual("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "empty");
        Assert.AreEqual("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "previous");
        Assert.AreEqual("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "1");

        desk.AddMonitor("HEADLESS-3", TestDesktop.Second);
        var monitors = Monitors(desk);
        Assert.AreEqual(2, monitors.Count(monitor => ShownOn(monitor) == "1 (1)"));
        Assert.Contains("HEADLESS-3", monitors.Select(NameOf));

        desk.Ok("focusmonitor", "HEADLESS-2");
        Assert.AreEqual("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "r+1");
        Assert.AreEqual("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "1");
        desk.Ok("workspace", "r~2");
        Assert.AreEqual("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "m+1");
        Assert.AreEqual("3 (3)", ActiveLabel(desk));

        desk.Dispatch("workspace", "1");
        Assert.AreEqual("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "special:HELLO");
        Assert.Contains("special:HELLO", Monitors(desk).Select(SpecialOn));

        desk.Dispatch("workspace", "3");
        Assert.AreEqual(2, Monitors(desk).Count(monitor => SpecialOn(monitor).Length == 0));

        desk.Ok("workspace", "1");
        desk.CloseAll();

        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");
        desk.Spawn("kitty_C");

        desk.Ok("focuswindow", "class:kitty_B");
        desk.Ok("resizeactive", "20 20");

        desk.Ok("focuswindow", "class:kitty_C");
        desk.Ok("focuswindow", "class:kitty_A");
        desk.Ok("movefocus", "r");
        Assert.AreEqual("kitty_C", desk.ActiveClass());

        desk.Ok("focuswindow", "class:kitty_A");
        desk.Ok("focusmonitor", "HEADLESS-3");
        desk.Spawn("kitty_D");
        Assert.AreEqual("kitty_D", desk.ActiveClass());

        desk.Ok("focusmonitor", "l");
        Assert.AreEqual("kitty_A", desk.ActiveClass());

        desk.Ok("fullscreen", "0");
        desk.Ok("movefocus", "r");
        Assert.AreEqual("kitty_D", desk.ActiveClass());

        desk.CloseAll();

        MultimonFocus(desk);
        WorkspaceHistoryMultiMon(desk);

        desk.RemoveMonitor("HEADLESS-3");

        SpecialWorkspaceFullscreen(desk);

        Assert.IsEmpty(desk.Clients());
    }

    [TestMethod]
    public void WorkspaceSwitchUnfocusesWindowFromOldWorkspace()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        for (var followMouse = 0; followMouse <= 1; followMouse++)
        {
            desk.Set("input:follow_mouse", followMouse.ToString(CultureInfo.InvariantCulture));

            var before = desk.Send("activewindow");
            Assert.AreNotEqual("", before);

            desk.Ok("workspace", "2");
            Assert.AreEqual("", desk.Send("activewindow"));

            desk.Ok("workspace", "previous");
            Assert.AreEqual(before, desk.Send("activewindow"));
        }
    }

    private static void MultimonFocus(TestDesktop desk)
    {
        desk.Set("input:follow_mouse", "0");
        desk.Ok("focusmonitor", "HEADLESS-3");
        desk.Ok("workspace", "8");
        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Ok("workspace", "7");

        desk.Spawn("a");
        desk.Spawn("b");

        desk.Ok("focuswindow", "class:a");
        desk.Ok("movefocus", "r");
        Assert.AreEqual("7 (7)", ActiveLabel(desk));
        Assert.AreEqual("b", desk.ActiveClass());

        desk.Ok("movefocus", "r");
        Assert.AreEqual("8 (8)", ActiveLabel(desk));

        desk.Spawn("c");
        Assert.AreEqual("c", desk.ActiveClass());
        Assert.AreEqual("8 (8)", ActiveLabel(desk));

        desk.Ok("movefocus", "l");
        Assert.AreEqual("b", desk.ActiveClass());
        Assert.AreEqual("7 (7)", ActiveLabel(desk));

        desk.Ok("movewindow", "r");
        Assert.AreEqual("b", desk.ActiveClass());
        Assert.AreEqual("8 (8)", ActiveLabel(desk));

        desk.Ok("movefocus", "r");
        Assert.AreEqual("c", desk.ActiveClass());
        Assert.AreEqual("8 (8)", ActiveLabel(desk));

        desk.Ok("movefocus", "l");

        Assert.AreEqual("ok", desk.Send("reload"));
        desk.CloseAll();
    }

    private static void WorkspaceHistoryMultiMon(TestDesktop desk)
    {
        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Ok("workspace", "10");
        desk.Spawn("a");
        desk.Ok("workspace", "11");
        desk.Spawn("a");

        desk.Ok("focusmonitor", "HEADLESS-3");
        desk.Ok("workspace", "12");
        desk.Spawn("a");

        desk.Ok("focusmonitor", "HEADLESS-2");
        Assert.AreEqual("11 (11)", ActiveLabel(desk));

        desk.Ok("workspace", "previous_per_monitor");
        Assert.AreEqual("10 (10)", ActiveLabel(desk));

        desk.CloseAll();
    }

    private static void SpecialWorkspaceFullscreen(TestDesktop desk)
    {
        desk.Dispatch("workspace", "1");
        Assert.IsEmpty(desk.Clients());

        desk.Ok("workspace", "special:test_fs_special");
        desk.Spawn("kitty_special");
        Assert.AreEqual("kitty_special", desk.ActiveClass());
        Assert.AreEqual("special:test_fs_special", WorkspaceNameOf(desk.ActiveWindow()));

        desk.Ok("fullscreen", "0");
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
        Assert.Contains("special:test_fs_special", Monitors(desk).Select(SpecialOn));

        desk.Ok("togglespecialworkspace", "test_fs_special");
        desk.Dispatch("workspace", "1");
        desk.Spawn("kitty_regular");

        desk.Ok("fullscreen", "0");
        Assert.AreEqual("kitty_regular", desk.ActiveClass());
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.ActiveWindow()));

        desk.Ok("togglespecialworkspace", "test_fs_special");
        desk.Ok("focuswindow", "class:kitty_special");
        Assert.AreEqual("kitty_special", desk.ActiveClass());

        desk.Ok("togglespecialworkspace", "test_fs_special");
        desk.Ok("focuswindow", "class:kitty_regular");
        Assert.AreEqual("kitty_regular", desk.ActiveClass());
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
        Assert.Contains("", Monitors(desk).Select(SpecialOn));

        desk.CloseAll();
        Assert.AreEqual("ok", desk.Send("reload"));
    }

    private static int X(TestDesktop desk) => desk.Placed(desk.ActiveId()).X;

    private static void ExpectActive(TestDesktop desk, string label, string monitor)
    {
        var active = desk.ActiveWorkspace();
        Assert.AreEqual(label, Label(active));
        Assert.AreEqual(monitor, active.GetProperty("monitor").GetString());
    }

    private static string ActiveLabel(TestDesktop desk) => Label(desk.ActiveWorkspace());

    private static string[] Labels(TestDesktop desk) => [.. desk.Workspaces().Select(Label)];

    private static string Label(JsonElement workspace) =>
        $"{workspace.GetProperty("address").GetString()} ({workspace.GetProperty("name").GetString()})";

    private static string WorkspaceNameOf(JsonElement client) =>
        client.GetProperty("workspace").GetProperty("name").GetString() ?? "";

    private static JsonElement[] Monitors(TestDesktop desk)
    {
        using var document = JsonDocument.Parse(desk.Send("j/monitors"));
        return [.. document.RootElement.Clone().EnumerateArray()];
    }

    private static string NameOf(JsonElement monitor) => monitor.GetProperty("name").GetString() ?? "";

    private static string ShownOn(JsonElement monitor)
    {
        var workspace = monitor.GetProperty("activeWorkspace");
        return $"{workspace.GetProperty("id").GetInt32()} ({workspace.GetProperty("name").GetString()})";
    }

    private static string SpecialOn(JsonElement monitor) => monitor.GetProperty("specialWorkspace").GetProperty("name").GetString() ?? "";

}
