using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "notification-area")]
public class NotificationArea(ITestOutputHelper output)
{
    private const uint Callback = 0x8000 + 0x71;
    private const uint ContextMenu = 0x007B;
    private const uint IconId = 1;
    private const uint GetMenu = 0x01E1;
    private const uint KeyDown = 0x0100;
    private const uint KeyUp = 0x0101;

    [VerifyFact]
    public void OpensTheIconMenuAndRunsReloadAndQuit() => VerifyRun.Drive(output, run =>
    {
        run.FollowEvents();
        var window = run.OpenManaged("1");
        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides the window", () => !VerifyRun.Visible(window));

        HWND tray = default;
        run.Until("winland has its notification-area icon window", () =>
        {
            tray = Desktop.WindowsOf(run.Daemon.Id, "WinlandTray").FirstOrDefault();
            return !tray.IsNull;
        });

        var items = OpenMenu(run, tray);
        Assert.Contains("&Reload", items);
        Assert.Contains("&Edit config", items);
        Assert.Contains("&Quit", items);
        Press(menu: run, 'R');
        run.UntilEvent("configreloaded");

        OpenMenu(run, tray);
        Press(menu: run, 'Q');
        run.Until("Quit stopped winland", () => run.Daemon.Exited);
        run.Until("Quit put the hidden window back", () => VerifyRun.Visible(window));
    });

    private static void Press(VerifyRun menu, char key)
    {
        var window = Desktop.WindowsOf(menu.Daemon.Id, "#32768").First(hwnd => PInvoke.IsWindowVisible(hwnd));
        Assert.True(PInvoke.PostMessage(window, KeyDown, key, 0), "The menu did not take the key.");
        Assert.True(PInvoke.PostMessage(window, KeyUp, key, 0), "The menu did not take the key.");
        menu.Say($"pressed {key} in the menu");
    }

    private static List<string> OpenMenu(VerifyRun run, HWND tray)
    {
        Thread.Sleep(500);
        var (x, y) = Desktop.Cursor();
        var at = (nuint)(((uint)(ushort)y << 16) | (ushort)x);
        Assert.True(PInvoke.PostMessage(tray, Callback, at, (nint)((IconId << 16) | ContextMenu)), "The icon window did not take the click.");
        run.Say("right-clicked the notification-area icon");
        HWND menu = default;
        run.Until("the icon's menu opens", () =>
        {
            menu = Desktop.WindowsOf(run.Daemon.Id, "#32768").FirstOrDefault(hwnd => PInvoke.IsWindowVisible(hwnd));
            return !menu.IsNull;
        });
        var handle = new HMENU((nint)PInvoke.SendMessage(menu, GetMenu, default, default).Value);
        var items = Desktop.MenuItems(handle);
        run.Say("menu items: " + string.Join(" | ", items));
        return items;
    }
}
