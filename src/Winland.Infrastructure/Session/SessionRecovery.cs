using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class SessionRecovery
{
    internal static void Restore(WindowAdapter windows, RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Clients.Count == 0)
            return;

        ShowMatched(snapshot);
        windows.Recover(snapshot);
    }

    private static void ShowMatched(RecoverySnapshot snapshot)
    {
        foreach (var client in snapshot.Clients)
        {
            if (client.Hidden || client.ProcessId == 0 || !WindowIdentity.Matches(client))
                continue;

            var hwnd = new HWND((nint)client.Window.Value);
            if (PInvoke.IsIconic(hwnd))
                continue;

            _ = PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }
    }
}
