using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winland.Infrastructure;

public static class WindowIdentity
{
    public static bool Matches(RecoveredClient client) => Recognize(client) == Recognition.SameClient;

    internal static Recognition Recognize(RecoveredClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return TryRead(client.Window, out var processId, out var windowClass, out var title)
            ? Recognize(client, processId, windowClass, title)
            : Recognition.Other;
    }

    internal static Recognition Recognize(RecoveredClient client, long processId, string windowClass, string title)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (processId != client.ProcessId || windowClass != client.Class)
            return Recognition.Other;

        return client.Hidden || title == client.Title ? Recognition.SameClient : Recognition.SameWindow;
    }

    public static RecoverySnapshot Annotate(RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var clients = new List<RecoveredClient>(snapshot.Clients.Count);
        foreach (var client in snapshot.Clients)
        {
            if (!TryRead(client.Window, out var processId, out var windowClass, out var title))
                continue;

            clients.Add(client with
            {
                Class = windowClass,
                Title = title,
                ProcessId = processId,
            });
        }

        var held = new List<HeldWindow>(snapshot.Held.Count);
        foreach (var window in snapshot.Held)
        {
            if (TryRead(window.Window, out var processId, out var windowClass, out _))
                held.Add(window with { Class = windowClass, ProcessId = processId });
        }

        return snapshot with { Clients = clients, Held = held };
    }

    internal static bool Recognize(HeldWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return TryRead(window.Window, out var processId, out var windowClass, out _)
            && processId == window.ProcessId
            && windowClass == window.Class;
    }

    internal enum Recognition
    {
        Other,

        SameWindow,

        SameClient,
    }

    private static bool TryRead(WindowId window, out long processId, out string windowClass, out string title)
    {
        processId = 0;
        windowClass = "";
        title = "";
        var hwnd = new HWND((nint)window.Value);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return false;

        if (PInvoke.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
            return false;

        processId = pid;
        windowClass = WindowAdapter.ClassName(hwnd);
        title = WindowAdapter.WindowTitle(hwnd);
        return true;
    }
}
