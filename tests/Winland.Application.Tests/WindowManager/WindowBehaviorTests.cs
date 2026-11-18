using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class WindowBehaviorTests
{
    [TestMethod]
    public void SwapWindow()
    {
        var desk = new TestDesktop();
        desk.Ok("workspace", "2");
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");
        Assert.AreEqual(2, desk.Clients().Length);

        desk.Dispatch("focuswindow", "class:kitty_A");
        var at = desk.ActivePlace().At;
        desk.Ok("swapwindow", "r");
        desk.Ok("focuswindow", "class:kitty_B");
        Assert.AreEqual(at, desk.ActivePlace().At);

        desk.Ok("fullscreen", "0");
        Assert.Contains("Can't swap fullscreen window", desk.Dispatch("swapwindow", "l"), StringComparison.Ordinal);
        desk.Ok("fullscreen", "0");
    }

    [TestMethod]
    public void FocusFloatingOrTilingUnderFullscreen()
    {
        var desk = new TestDesktop("""
            misc:focus_on_activate = true
            """);
        desk.Spawn("kitty_a");
        desk.Spawn("kitty_b");

        ExpectFocused(desk, "kitty_b", 0);
        desk.Ok("togglefloating");

        desk.Ok("focuswindow", "class:kitty_a");
        desk.Ok("fullscreen", "0 set");
        ExpectFocused(desk, "kitty_a", 2);

        desk.Ok("focuswindow", "floating");
        Assert.Contains("kitty_b", desk.ActiveClass(), StringComparison.Ordinal);
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.Client("kitty_a")));

        desk.Ok("fullscreen", "0 set");
        desk.Ok("focuswindow", "tiled");
        Assert.Contains("kitty_a", desk.ActiveClass(), StringComparison.Ordinal);
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.Client("kitty_b")));
    }

    [TestMethod]
    public void WindowMaximizeSize()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");

        Assert.AreEqual(("22,22", "1876,1036"), desk.PlaceOf("kitty_A"));
        Assert.AreEqual(0, TestDesktop.Fullscreen(desk.Client("kitty_A")));

        desk.Ok("fullscreen", "1");

        Assert.AreEqual(("22,22", "1876,1036"), desk.PlaceOf("kitty_A"));
        Assert.AreEqual(1, TestDesktop.Fullscreen(desk.Client("kitty_A")));
    }

    [TestMethod]
    public void GroupFallbackFocus()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Ok("togglegroup");
        desk.Spawn("kitty_B");
        desk.Spawn("kitty_C");
        desk.Spawn("kitty_D");

        Assert.AreEqual("kitty_D", desk.ActiveClass());

        desk.Ok("focuswindow", "class:kitty_B");
        desk.Ok("focuswindow", "class:kitty_D");
        desk.CloseActive();
        Assert.AreEqual(3, desk.Clients().Length);

        Assert.AreEqual("kitty_B", desk.ActiveClass());
    }

    [TestMethod]
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
        Assert.AreEqual(1337, TestDesktop.WorkspaceId(active));
        Assert.IsTrue(TestDesktop.Pinned(active));

        desk.Dispatch("workspace", "1338");

        active = desk.ActiveWindow();
        Assert.AreEqual(1338, TestDesktop.WorkspaceId(active));
        Assert.IsTrue(TestDesktop.Pinned(active));

        desk.Ok("focuswindow", "class:kitty");
        desk.Ok("togglefloating");

        active = desk.ActiveWindow();
        Assert.AreEqual(1338, TestDesktop.WorkspaceId(active));
        Assert.IsFalse(TestDesktop.Pinned(active));
    }

    [TestMethod]
    public void WindowruleWorkspaceEmpty()
    {
        var desk = new TestDesktop("""
            windowrule = workspace empty, class:kitty_A
            windowrule = workspace emptyn, class:kitty_B
            """);

        desk.Dispatch("workspace", "3");
        desk.Spawn("kitty");
        Assert.AreEqual(3, TestDesktop.WorkspaceId(desk.ActiveWindow()));

        desk.Spawn("kitty_A");
        Assert.AreEqual(1, TestDesktop.WorkspaceId(desk.ActiveWindow()));

        desk.Dispatch("workspace", "3");
        desk.Spawn("kitty_B");
        Assert.AreEqual(4, TestDesktop.WorkspaceId(desk.ActiveWindow()));
    }

    [TestMethod]
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

    [TestMethod]
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

        desk.RemoveMonitor("HEADLESS-4");

        desk.Ok("focuswindow", "class:kitty_14134");
        desk.Dispatch("pin");

        Assert.AreEqual("ok", desk.Send("reload"));
        CloseAll(desk);
        Assert.IsEmpty(desk.Clients());
    }

    [TestMethod]
    public void ExactWindowSelectors()
    {
        var desk = new TestDesktop();
        desk.Spawn("kitty_A");
        desk.Spawn("kitty_B");

        desk.Dispatch("focuswindow", "class:kitty_B");
        var target = desk.ActiveWindow();
        var address = Address(target);
        var pid = target.GetProperty("pid").GetInt64().ToString(CultureInfo.InvariantCulture);

        desk.Dispatch("focuswindow", "class:kitty_A");
        desk.Ok("focuswindow", "address:" + address);
        Assert.AreEqual("kitty_B", desk.ActiveClass());

        desk.Dispatch("focuswindow", "class:kitty_A");
        desk.Ok("focuswindow", "pid:" + pid);
        Assert.AreEqual("kitty_B", desk.ActiveClass());
    }

    [TestMethod]
    public void PinnedRetainsPositionOnWorkspaceChange()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        desk.Ok("workspace", "1");
        desk.Ok("togglefloating");
        desk.Ok("pin");
        Assert.Contains(TestDesktop.Pinned, desk.Clients());

        desk.MoveTo(desk.ActiveId(), 500, 300);

        desk.Ok("workspace", "2");
        Assert.Contains(workspace => workspace.GetProperty("id").GetInt32() == 2, desk.Workspaces());

        desk.Ok("workspace", "1");
        Assert.Contains(workspace => workspace.GetProperty("id").GetInt32() == 1, desk.Workspaces());

        Assert.Contains(TestDesktop.Pinned, desk.Clients());
        Assert.AreEqual("500,300", desk.PlaceOf("a").At);
    }

    [TestMethod]
    public void Monitorrule()
    {
        var desk = new TestDesktop("windowrule = monitor HEADLESS-3, class:monitor_kitty\n");
        desk.AddMonitor("HEADLESS-3", TestDesktop.Second);

        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Spawn("monitor_kitty");
        Assert.ContainsSingle(desk.Clients());
        Assert.AreEqual("HEADLESS-3", desk.ActiveWindow().GetProperty("monitor").GetString());
        Assert.AreEqual("HEADLESS-3", desk.ActiveWorkspace().GetProperty("monitor").GetString());

        CloseAll(desk);
        Assert.AreEqual("ok", desk.Send("reload"));

        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Set("windowrule", "monitor HEADLESS-3 silent, class:silent_kitty");

        desk.Spawn("silent_kitty");
        Assert.AreEqual("HEADLESS-3", desk.Clients()[0].GetProperty("monitor").GetString());
        Assert.AreEqual("HEADLESS-2", desk.ActiveWorkspace().GetProperty("monitor").GetString());
    }

    [TestMethod]
    public void MouseResize()
    {
        var desk = new TestDesktop("windowrule = float, class:kitty\n");
        desk.Spawn("kitty");
        Assert.ContainsSingle(desk.Clients());
        desk.Ok("focuswindow", "class:kitty");

        ResetWindow(desk);
        desk.Set("bind", ", mouse:right, resizewindow");
        Press(desk, "mouse:right");
        desk.MovePointer(700, 200);
        Release(desk, "mouse:right");
        Assert.AreEqual("700,200", desk.PlaceOf("kitty").Size);

        ResetWindow(desk);
        desk.Set("bind", ", mouse:right, resizewindow, 1");
        Press(desk, "mouse:right");
        desk.MovePointer(1280, 100);
        Release(desk, "mouse:right");
        Assert.AreEqual("1280,800", desk.PlaceOf("kitty").Size);
    }

    [TestMethod]
    public void GetFullscreenHandler()
    {
        var desk = new TestDesktop();
        desk.Set("general:layout", "dwindle");

        desk.Spawn("cat");

        desk.Ok("fullscreen", "0");
        Assert.AreEqual("default", Handler(desk));
        desk.Ok("fullscreen", "0");

        desk.Ok("togglefloating");

        desk.Ok("fullscreen", "0");
        Assert.AreEqual("default", Handler(desk));
        desk.Ok("fullscreen", "0");

        desk.Set("general:layout", "scrolling");

        desk.Ok("fullscreen", "0");
        Assert.AreEqual("default", Handler(desk));
        desk.Ok("fullscreen", "0");

        desk.Ok("togglefloating");

        desk.Ok("fullscreen", "0 default");
        Assert.AreEqual("default", Handler(desk));
        desk.Ok("fullscreen", "0 default");

        desk.Ok("fullscreen", "0");
        Assert.AreEqual("scrolling", Handler(desk));
        desk.Ok("fullscreen", "0");
    }

    [TestMethod]
    public void FloatingForceOnscreen()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Ok("togglefloating");
        desk.Place(desk.ActiveId(), -10, 0, 100, 100);
        Assert.AreEqual("2,2", desk.ActivePlace().At);
    }

    [TestMethod]
    public void SpecialWorkspacePinnedWindowUnmapFallback()
    {
        var desk = new TestDesktop();
        CloseAll(desk);
        Assert.IsEmpty(desk.Clients());

        desk.Ok("workspace", "1");
        desk.Spawn("kitty_normal");

        desk.Spawn("kitty_pinned");
        desk.Ok("focuswindow", "class:kitty_pinned");
        desk.Ok("togglefloating");
        desk.Ok("focuswindow", "class:kitty_pinned");
        desk.Ok("pin", "enable");

        desk.Ok("togglespecialworkspace", "fallback");

        desk.Spawn("kitty_sp_normal");
        Assert.AreEqual(3, desk.Clients().Length);

        desk.Ok("focuswindow", "class:kitty_pinned");
        desk.CloseActive();
        Assert.AreEqual(2, desk.Clients().Length);

        Assert.AreEqual("special:fallback", WorkspaceName(desk.ActiveWindow()));

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

    private static void ExpectFocused(TestDesktop desk, string windowClass, int fullscreen)
    {
        var active = desk.ActiveWindow();
        Assert.AreEqual(windowClass, TestDesktop.ClassOf(active));
        Assert.AreEqual(fullscreen, TestDesktop.Fullscreen(active));
    }

    private static void Press(TestDesktop desk, string button) =>
        desk.Handle(new PointerButtonPressed(button) { Position = desk.Pointer });

    private static void Release(TestDesktop desk, string button) =>
        desk.Handle(new PointerButtonReleased(button) { Position = desk.Pointer });

    private static void CloseAll(TestDesktop desk)
    {
        foreach (var client in desk.Clients())
            desk.Handle(new WindowClosed(IdOf(client)));
    }

    private static void ResetWindow(TestDesktop desk)
    {
        Assert.AreEqual("ok", desk.Send("reload"));
        desk.Set("unbind", ", mouse:right");
        desk.Place("kitty", 0, 0, 640, 400);
        desk.MovePointer(640, 400);
        Assert.AreEqual(("2,2", "640,400"), desk.PlaceOf("kitty"));
    }

    private static void ExpectNear(string size, int width, int height, int delta)
    {
        var parts = size.Split(',');
        Assert.IsInRange(width - delta, width + delta, int.Parse(parts[0], CultureInfo.InvariantCulture));
        Assert.IsInRange(height - delta, height + delta, int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static void ExpectSideBySide(TestDesktop desk, WindowId first, WindowId second)
    {
        var places = new[] { Place(desk, first), Place(desk, second) };
        foreach (var place in places)
{
    Assert.AreEqual("931,1036", place.Size);
}
        Assert.Contains(place => place.At == "22,22", places);
        Assert.Contains(place => place.At == "967,22", places);
    }
}
