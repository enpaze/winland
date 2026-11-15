using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

/// <summary>
/// Windows, driven through pipe lines on a <see cref="TestDesktop"/>: swapping, fullscreen, pins, rules, exec rules and floating moves.
/// </summary>
public class WindowBehaviorTests
{
    [Fact]
    public void SwapWindow()
    {
        var desk = new TestDesktop();
        desk.Ok("workspace", "2");
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");
        Assert.Equal(2, desk.Clients().Length);

        desk.Dispatch("focuswindow", "class:kitty_A");
        var at = desk.ActivePlace().At;
        desk.Ok("swapwindow", "r");
        desk.Ok("focuswindow", "class:kitty_B");
        Assert.Equal(at, desk.ActivePlace().At);

        desk.Ok("fullscreen", "0");
        Assert.Contains("Can't swap fullscreen window", desk.Dispatch("swapwindow", "l"), StringComparison.Ordinal);
        desk.Ok("fullscreen", "0");
    }

    [Fact]
    public void FocusFloatingOrTilingUnderFullscreen()
    {
        var desk = new TestDesktop("""
            misc:focus_on_activate = true
            """);
        desk.Spawn("kitty_a");
        desk.Spawn("kitty_b");

        ExpectFocused(desk, "kitty_b", 0);
        desk.Ok("togglefloating");

        // A floating window over a fullscreen tile.
        desk.Ok("focuswindow", "class:kitty_a");
        desk.Ok("fullscreen", "0 set");
        ExpectFocused(desk, "kitty_a", 2);

        desk.Ok("focuswindow", "floating");
        Assert.Contains("kitty_b", desk.ActiveClass(), StringComparison.Ordinal);
        Assert.Equal(2, TestDesktop.Fullscreen(desk.Client("kitty_a")));
        Assert.Equal(2, TestDesktop.FullscreenClient(desk.Client("kitty_a")));

        // A tile under a fullscreen floating window.
        desk.Ok("fullscreen", "0 set");
        desk.Ok("focuswindow", "tiled");
        Assert.Contains("kitty_a", desk.ActiveClass(), StringComparison.Ordinal);
        Assert.Equal(0, TestDesktop.Fullscreen(desk.Client("kitty_b")));
    }

    [Fact]
    public void WindowMaximizeSize()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");

        Assert.Equal(("22,22", "1876,1036"), desk.PlaceOf("kitty_A"));
        Assert.Equal(0, TestDesktop.Fullscreen(desk.Client("kitty_A")));

        desk.Ok("fullscreen", "1");

        Assert.Equal(("22,22", "1876,1036"), desk.PlaceOf("kitty_A"));
        Assert.Equal(1, TestDesktop.Fullscreen(desk.Client("kitty_A")));
    }

    [Fact]
    public void GroupFallbackFocus()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Ok("togglegroup");
        desk.Spawn("kitty_B");
        desk.Spawn("kitty_C");
        desk.Spawn("kitty_D");

        Assert.Equal("kitty_D", desk.ActiveClass());

        desk.Ok("focuswindow", "class:kitty_B");
        desk.Ok("focuswindow", "class:kitty_D");
        desk.CloseActive();
        Assert.Equal(3, desk.Clients().Length);

        Assert.Equal("kitty_B", desk.ActiveClass());
    }

    [Fact]
    public void PinnedWorkspacesValid()
    {
        var desk = new TestDesktop();
        desk.Dispatch("workspace", "1337");
        desk.Spawn("kitty");

        desk.Ok("focuswindow", "class:kitty");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:kitty");
        desk.Ok("pin");

        var active = desk.ActiveWindow();
        Assert.Equal(1337, TestDesktop.WorkspaceId(active));
        Assert.True(TestDesktop.Pinned(active));

        desk.Dispatch("workspace", "1338");

        active = desk.ActiveWindow();
        Assert.Equal(1338, TestDesktop.WorkspaceId(active));
        Assert.True(TestDesktop.Pinned(active));

        desk.Ok("focuswindow", "class:kitty");
        desk.Ok("togglefloating");

        active = desk.ActiveWindow();
        Assert.Equal(1338, TestDesktop.WorkspaceId(active));
        Assert.False(TestDesktop.Pinned(active));
    }

    [Fact]
    public void WindowruleWorkspaceEmpty()
    {
        var desk = new TestDesktop("""
            windowrule = workspace empty, class:kitty_A
            windowrule = workspace emptyn, class:kitty_B
            """);

        desk.Dispatch("workspace", "3");
        desk.Spawn("kitty");
        Assert.Equal(3, TestDesktop.WorkspaceId(desk.ActiveWindow()));

        desk.Spawn("kitty_A");
        Assert.Equal(1, TestDesktop.WorkspaceId(desk.ActiveWindow()));

        desk.Dispatch("workspace", "3");
        desk.Spawn("kitty_B");
        Assert.Equal(4, TestDesktop.WorkspaceId(desk.ActiveWindow()));
    }

    [Fact]
    public void Issue14038()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_14038");

        desk.Ok("focuswindow", "class:kitty_14038");
        desk.Ok("movetoworkspacesilent", "special:a");
        desk.Ok("focuswindow", "class:kitty_14038");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:kitty_14038");
        desk.Ok("pin");
        desk.Ok("focuswindow", "class:kitty_14038");
        desk.Ok("togglefloating");
    }

    [Fact]
    public void Issue14134()
    {
        var desk = new TestDesktop();
        desk.AddMonitor("HEADLESS-4", TestDesktop.Second);
        desk.Ok("focusmonitor", "HEADLESS-4");

        desk.Spawn("kitty_14134");

        desk.Ok("focuswindow", "class:kitty_14134");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:kitty_14134");
        desk.Ok("pin");

        // Disabling the monitor takes it away, as unplugging it does.
        desk.RemoveMonitor("HEADLESS-4");

        // The reference answers an error here and only checks that it is still alive.
        desk.Ok("focuswindow", "class:kitty_14134");
        desk.Dispatch("pin");

        Assert.Equal("ok", desk.Send("reload"));
        CloseAll(desk);
        Assert.Empty(desk.Clients());
    }

    [Fact]
    public void ExactWindowSelectors()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");

        desk.Dispatch("focuswindow", "class:kitty_B");
        var target = desk.ActiveWindow();
        var address = Address(target);
        var pid = target.GetProperty("pid").GetInt64().ToString(CultureInfo.InvariantCulture);
        var stableId = target.GetProperty("stableId").GetString();

        desk.Dispatch("focuswindow", "class:kitty_A");
        desk.Ok("focuswindow", "address:" + address);
        Assert.Equal("kitty_B", desk.ActiveClass());

        desk.Dispatch("focuswindow", "class:kitty_A");
        desk.Ok("focuswindow", "pid:" + pid);
        Assert.Equal("kitty_B", desk.ActiveClass());

        desk.Dispatch("focuswindow", "class:kitty_A");
        desk.Ok("focuswindow", "stableid:" + stableId);
        Assert.Equal("kitty_B", desk.ActiveClass());
    }

    [Fact]
    public void PinnedRetainsPositionOnWorkspaceChange()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        desk.Ok("workspace", "1");
        desk.Ok("togglefloating");
        desk.Ok("pin");
        Assert.Contains(desk.Clients(), TestDesktop.Pinned);

        desk.MoveTo(desk.ActiveId(), 500, 300);

        desk.Ok("workspace", "2");
        Assert.Contains(desk.Workspaces(), workspace => workspace.GetProperty("id").GetInt32() == 2);

        desk.Ok("workspace", "1");
        Assert.Contains(desk.Workspaces(), workspace => workspace.GetProperty("id").GetInt32() == 1);

        Assert.Contains(desk.Clients(), TestDesktop.Pinned);
        Assert.Equal("500,300", desk.PlaceOf("a").At);
    }

    [Fact]
    public void Monitorrule()
    {
        var desk = new TestDesktop("windowrule = monitor HEADLESS-3, class:monitor_kitty\n");
        desk.AddMonitor("HEADLESS-3", TestDesktop.Second);
        var third = MonitorId(desk, "HEADLESS-3");

        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Spawn("monitor_kitty");
        Assert.Single(desk.Clients());
        Assert.Equal(third, desk.ActiveWindow().GetProperty("monitor").GetInt32());
        Assert.Equal("HEADLESS-3", desk.ActiveWorkspace().GetProperty("monitor").GetString());

        CloseAll(desk);
        Assert.Equal("ok", desk.Send("reload"));

        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Set("windowrule", "monitor HEADLESS-3 silent, class:silent_kitty");

        desk.Spawn("silent_kitty");
        Assert.Equal(third, desk.Clients()[0].GetProperty("monitor").GetInt32());
        Assert.Equal("HEADLESS-2", desk.ActiveWorkspace().GetProperty("monitor").GetString());
    }

    [Fact]
    public void MouseResize()
    {
        var desk = new TestDesktop("windowrule = float, class:kitty\n");
        desk.Spawn("kitty");
        Assert.Single(desk.Clients());
        desk.Ok("focuswindow", "class:kitty");

        ResetWindow(desk);
        desk.Set("bindm", ", mouse:273, resizewindow");
        Press(desk, "mouse:273");
        desk.MovePointer(700, 200);
        Release(desk, "mouse:273");
        Assert.Equal("700,200", desk.PlaceOf("kitty").Size);

        ResetWindow(desk);
        desk.Set("bindm", ", mouse:273, resizewindow 1");
        Press(desk, "mouse:273");
        desk.MovePointer(1280, 100);
        Release(desk, "mouse:273");
        Assert.Equal("1280,800", desk.PlaceOf("kitty").Size);
    }

    [Fact]
    public void GetFullscreenHandler()
    {
        var desk = new TestDesktop();
        desk.Set("general:layout", "dwindle");

        desk.Spawn("cat");

        desk.Ok("fullscreen", "0");
        Assert.Equal("default", Handler(desk));
        desk.Ok("fullscreen", "0");

        desk.Ok("togglefloating");

        desk.Ok("fullscreen", "0");
        Assert.Equal("default", Handler(desk));
        desk.Ok("fullscreen", "0");

        desk.Set("general:layout", "scrolling");

        desk.Ok("fullscreen", "0");
        Assert.Equal("default", Handler(desk));
        desk.Ok("fullscreen", "0");

        desk.Ok("togglefloating");

        // The reference turns layout_aware off here.
        desk.Ok("fullscreen", "0 default");
        Assert.Equal("default", Handler(desk));
        desk.Ok("fullscreen", "0 default");

        desk.Ok("fullscreen", "0");
        Assert.Equal("scrolling", Handler(desk));
        desk.Ok("fullscreen", "0");
    }

    [Fact]
    public void FloatingForceOnscreen()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Ok("togglefloating");
        // Fully on screen.
        desk.Place(desk.ActiveId(), -10, 0, 100, 100);
        Assert.Equal("2,2", desk.ActivePlace().At);
    }

    [Fact]
    public void SpecialWorkspacePinnedWindowUnmapFallback()
    {
        var desk = new TestDesktop();
        CloseAll(desk);
        Assert.Empty(desk.Clients());

        desk.Ok("workspace", "1");
        desk.Spawn("kitty_normal");

        desk.Spawn("kitty_pinned");
        desk.Ok("focuswindow", "class:kitty_pinned");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:kitty_pinned");
        desk.Ok("pin", "enable");

        desk.Ok("togglespecialworkspace", "fallback");

        desk.Spawn("kitty_sp_normal");
        Assert.Equal(3, desk.Clients().Length);

        desk.Ok("focuswindow", "class:kitty_pinned");
        desk.CloseActive();
        Assert.Equal(2, desk.Clients().Length);

        Assert.Equal("special:fallback", WorkspaceName(desk.ActiveWindow()));

        desk.Ok("togglespecialworkspace", "fallback");
        CloseAll(desk);
    }

    private static string Address(JsonElement client) => client.GetProperty("address").GetString() ?? "";

    private static string WorkspaceName(JsonElement client) =>
        client.GetProperty("workspace").GetProperty("name").GetString() ?? "";

    private static string Handler(TestDesktop desk) => desk.ActiveWindow().GetProperty("fullscreenHandler").GetString() ?? "";

    private static WindowId IdOf(JsonElement client) => new(Convert.ToInt64(Address(client)[2..], 16));

    private static (string At, string Size) Place(TestDesktop desk, WindowId window)
    {
        var bounds = desk.Placed(window);
        return ($"{bounds.X},{bounds.Y}", $"{bounds.Width},{bounds.Height}");
    }

    // The active window's class and fullscreen mode, as the reference's isActiveWindow checks them.
    private static void ExpectFocused(TestDesktop desk, string windowClass, int fullscreen)
    {
        var active = desk.ActiveWindow();
        Assert.Equal(windowClass, TestDesktop.ClassOf(active));
        Assert.Equal(fullscreen, TestDesktop.Fullscreen(active));
    }

    // The test plugin's click with a button held down, and with it let go, where the pointer is.
    private static void Press(TestDesktop desk, string button) =>
        desk.Handle(new PointerButtonPressed(button) { Position = desk.Pointer });

    private static void Release(TestDesktop desk, string button) =>
        desk.Handle(new PointerButtonReleased(button) { Position = desk.Pointer });

    private static void CloseAll(TestDesktop desk)
    {
        foreach (var client in desk.Clients())
            desk.Handle(new WindowClosed(IdOf(client)));
    }

    private static int MonitorId(TestDesktop desk, string name)
    {
        using var document = JsonDocument.Parse(desk.Send("j/monitors"));
        return document.RootElement.EnumerateArray()
            .Single(monitor => monitor.GetProperty("name").GetString() == name)
            .GetProperty("id").GetInt32();
    }

    // Reloads, unbinds the button and puts the window back, as the reference's RESET_WINDOW does.
    private static void ResetWindow(TestDesktop desk)
    {
        Assert.Equal("ok", desk.Send("reload"));
        desk.Set("unbind", ", mouse:273");
        desk.Place("kitty", 0, 0, 640, 400);
        desk.MovePointer(640, 400);
        Assert.Equal(("2,2", "640,400"), desk.PlaceOf("kitty"));
    }

    private static void ExpectNear(string size, int width, int height, int delta)
    {
        var parts = size.Split(',');
        Assert.InRange(int.Parse(parts[0], CultureInfo.InvariantCulture), width - delta, width + delta);
        Assert.InRange(int.Parse(parts[1], CultureInfo.InvariantCulture), height - delta, height + delta);
    }

    private static void ExpectSideBySide(TestDesktop desk, WindowId first, WindowId second)
    {
        var places = new[] { Place(desk, first), Place(desk, second) };
        Assert.All(places, place => Assert.Equal("931,1036", place.Size));
        Assert.Contains(places, place => place.At == "22,22");
        Assert.Contains(places, place => place.At == "967,22");
    }
}
