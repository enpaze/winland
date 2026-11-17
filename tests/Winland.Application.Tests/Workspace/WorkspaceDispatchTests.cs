using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

public class WorkspaceDispatchTests
{
    [Fact]
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
        Assert.Equal("2 (2)", ActiveLabel(desk));
        Assert.Contains("3 (3)", Labels(desk));
        Assert.Contains("1 (1)", Labels(desk));

        desk.Ok("workspace", "1");
        Assert.DoesNotContain("2 (2)", Labels(desk));

        desk.Ok("workspace", "m+1");
        Assert.Equal("3 (3)", ActiveLabel(desk));

        desk.Ok("workspace", "-1");
        Assert.Equal("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "1");
        desk.Ok("workspace", "r+1");
        Assert.Equal("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "r+1");
        Assert.Equal("3 (3)", ActiveLabel(desk));

        desk.Ok("workspace", "r~1");
        Assert.Equal("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "empty");
        Assert.Equal("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "previous");
        Assert.Equal("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "1");

        desk.AddMonitor("HEADLESS-3", TestDesktop.Second);
        var monitors = Monitors(desk);
        Assert.Equal(2, monitors.Count(monitor => ShownOn(monitor) == "1 (1)"));
        Assert.Contains("HEADLESS-3", monitors.Select(NameOf));

        desk.Ok("focusmonitor", "HEADLESS-2");
        Assert.Equal("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "r+1");
        Assert.Equal("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "1");
        desk.Ok("workspace", "r~2");
        Assert.Equal("2 (2)", ActiveLabel(desk));

        desk.Ok("workspace", "m+1");
        Assert.Equal("3 (3)", ActiveLabel(desk));

        desk.Dispatch("workspace", "1");
        Assert.Equal("1 (1)", ActiveLabel(desk));

        desk.Ok("workspace", "special:HELLO");
        Assert.Contains("special:HELLO", Monitors(desk).Select(SpecialOn));

        desk.Dispatch("workspace", "3");
        Assert.Equal(2, Monitors(desk).Count(monitor => SpecialOn(monitor).Length == 0));

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
        Assert.Equal("kitty_C", desk.ActiveClass());

        desk.Ok("focuswindow", "class:kitty_A");
        desk.Ok("focusmonitor", "HEADLESS-3");
        desk.Spawn("kitty_D");
        Assert.Equal("kitty_D", desk.ActiveClass());

        desk.Ok("focusmonitor", "l");
        Assert.Equal("kitty_A", desk.ActiveClass());

        desk.Ok("fullscreen", "0");
        desk.Ok("movefocus", "r");
        Assert.Equal("kitty_D", desk.ActiveClass());

        desk.CloseAll();

        MultimonFocus(desk);
        WorkspaceHistoryMultiMon(desk);

        desk.RemoveMonitor("HEADLESS-3");

        SpecialWorkspaceFullscreen(desk);

        Assert.Empty(desk.Clients());
    }

    [Fact]
    public void WorkspaceSwitchUnfocusesWindowFromOldWorkspace()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        for (var followMouse = 0; followMouse <= 1; followMouse++)
        {
            desk.Set("input:follow_mouse", followMouse.ToString(CultureInfo.InvariantCulture));

            var before = desk.Send("activewindow");
            Assert.NotEqual("Invalid", before);

            desk.Ok("workspace", "2");
            Assert.Equal("Invalid", desk.Send("activewindow"));

            desk.Ok("workspace", "previous");
            Assert.Equal(before, desk.Send("activewindow"));
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
        Assert.Equal("7 (7)", ActiveLabel(desk));
        Assert.Equal("b", desk.ActiveClass());

        desk.Ok("movefocus", "r");
        Assert.Equal("8 (8)", ActiveLabel(desk));

        desk.Spawn("c");
        Assert.Equal("c", desk.ActiveClass());
        Assert.Equal("8 (8)", ActiveLabel(desk));

        desk.Ok("movefocus", "l");
        Assert.Equal("b", desk.ActiveClass());
        Assert.Equal("7 (7)", ActiveLabel(desk));

        desk.Ok("movewindow", "r");
        Assert.Equal("b", desk.ActiveClass());
        Assert.Equal("8 (8)", ActiveLabel(desk));

        desk.Ok("movefocus", "r");
        Assert.Equal("c", desk.ActiveClass());
        Assert.Equal("8 (8)", ActiveLabel(desk));

        desk.Ok("movefocus", "l");

        Assert.Equal("ok", desk.Send("reload"));
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
        Assert.Equal("11 (11)", ActiveLabel(desk));

        desk.Ok("workspace", "previous_per_monitor");
        Assert.Equal("10 (10)", ActiveLabel(desk));

        desk.CloseAll();
    }

    private static void SpecialWorkspaceFullscreen(TestDesktop desk)
    {
        desk.Dispatch("workspace", "1");
        Assert.Empty(desk.Clients());

        desk.Ok("workspace", "special:test_fs_special");
        desk.Spawn("kitty_special");
        Assert.Equal("kitty_special", desk.ActiveClass());
        Assert.Equal("special:test_fs_special", WorkspaceNameOf(desk.ActiveWindow()));

        desk.Ok("fullscreen", "0");
        Assert.Equal(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
        Assert.Contains("special:test_fs_special", Monitors(desk).Select(SpecialOn));

        desk.Ok("togglespecialworkspace", "test_fs_special");
        desk.Dispatch("workspace", "1");
        desk.Spawn("kitty_regular");

        desk.Ok("fullscreen", "0");
        Assert.Equal("kitty_regular", desk.ActiveClass());
        Assert.Equal(2, TestDesktop.Fullscreen(desk.ActiveWindow()));

        desk.Ok("togglespecialworkspace", "test_fs_special");
        desk.Ok("focuswindow", "class:kitty_special");
        Assert.Equal("kitty_special", desk.ActiveClass());

        desk.Ok("togglespecialworkspace", "test_fs_special");
        desk.Ok("focuswindow", "class:kitty_regular");
        Assert.Equal("kitty_regular", desk.ActiveClass());
        Assert.Equal(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
        Assert.Contains("", Monitors(desk).Select(SpecialOn));

        desk.CloseAll();
        Assert.Equal("ok", desk.Send("reload"));
    }

    private static int X(TestDesktop desk) => desk.Placed(desk.ActiveId()).X;

    private static void ExpectActive(TestDesktop desk, string label, string monitor)
    {
        var active = desk.ActiveWorkspace();
        Assert.Equal(label, Label(active));
        Assert.Equal(monitor, active.GetProperty("monitor").GetString());
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
