using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    private void Restack(IReadOnlyList<ZOrderRequest> requests)
    {
        foreach (var request in requests)
        {
            if (_desktop.Hung(request.Window))
                _desktop.RaiseAsync(request.Window, request.Top);
            else
                _desktop.Raise(request.Window, request.Top);

            BorderHost.Follow(request.Window);
        }
    }

    private void KeepFloatingAbove()
    {
        var active = PInvoke.GetForegroundWindow();
        if (active.IsNull)
            return;

        var floating = new HashSet<WindowId>(_core.FloatingAbove(new WindowId(active)));
        if (floating.Count == 0)
            return;

        var lowest = HWND.Null;
        for (var window = PInvoke.GetWindow(active, GET_WINDOW_CMD.GW_HWNDNEXT); !window.IsNull; window = PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_HWNDNEXT))
        {
            if (floating.Contains(new WindowId(window)))
                lowest = window;
        }

        if (lowest.IsNull)
            return;

        var flags = SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (_desktop.Hung(new WindowId(active)))
            flags |= SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;

        _ = PInvoke.SetWindowPos(active, lowest, 0, 0, 0, 0, flags);
        BorderHost.Follow(new WindowId(active));
    }

    private void ReportResize(HWND hwnd, WindowId window)
    {
        if (_motions.Running(window) || !_core.AwaitsFirstResize(window) || !TryVisible(window, out var visible))
            return;

        Apply(_core.Handle(new WindowResized(window, visible)));
    }
}
