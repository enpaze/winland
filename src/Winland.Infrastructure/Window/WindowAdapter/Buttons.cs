using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    internal void UndoMinimize(nint handle)
    {
        var hwnd = new HWND(handle);
        var window = new WindowId(handle);
        if (_core.Manages(window) && PInvoke.IsWindowVisible(hwnd) && PInvoke.IsIconic(hwnd))
        {
            _ = _desktop.Hung(window)
                ? PInvoke.ShowWindowAsync(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE)
                : PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }
    }

    private void AdoptShown(HWND hwnd, WindowId window)
    {
        if (!_core.Manages(window) && !CanBeClient(hwnd))
            return;

        if (!TryDescribe(hwnd, window, out var appeared))
            return;

        if (_core.Manages(window))
            Apply(_core.Handle(new WindowClassChanged(window, appeared.Traits.Class)));

        appeared = appeared with
        {
            ShownMaximized = PInvoke.IsZoomed(hwnd),
            Opened = true,
            Dragging = PInvoke.GetAsyncKeyState(1) < 0 && appeared.Pointer is Point pointer
                && pointer.X >= appeared.Bounds.X && pointer.X < appeared.Bounds.X + appeared.Bounds.Width
                && pointer.Y >= appeared.Bounds.Y && pointer.Y < appeared.Bounds.Y + appeared.Bounds.Height,
        };
        var opened = !_core.Manages(window);
        var outcome = _core.Handle(appeared);
        NoteManaged(window, outcome, appeared);
        Apply(outcome);

        if (opened && outcome.State.Clients.Any(client => client.Window == window && !client.Floating))
            _settling[window] = Environment.TickCount64 + SettleMilliseconds;
    }

    internal Outcome RequestMaximize(nint handle)
    {
        var window = new WindowId(handle);
        if (!_core.Manages(window))
            return _core.Handle(new WindowMaximizeRequested(window));

        var hwnd = new HWND(handle);
        if (PInvoke.IsZoomed(hwnd))
            _ = _desktop.Hung(window) ? PInvoke.ShowWindowAsync(hwnd, SHOW_WINDOW_CMD.SW_RESTORE) : PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);

        if (_settling.TryGetValue(window, out var until) && Environment.TickCount64 < until
            && !InputClock.Recent(unchecked((uint)Environment.TickCount)) && TryVisible(window, out var landed))
        {
            var drifted = _core.Handle(new WindowDrifted(window, landed));
            Apply(drifted);
            return drifted;
        }

        var outcome = _core.Handle(new WindowMaximizeRequested(window));
        Apply(outcome);
        _settling[window] = Environment.TickCount64 + SettleMilliseconds;
        return outcome;
    }

    private void ReportFullscreen(HWND hwnd, WindowId window)
    {
        if (!_core.Manages(window) || _motions.Running(window) || PInvoke.IsIconic(hwnd) || !TryVisible(window, out var visible))
            return;

        var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
            return;

        var covers = visible == WindowRect.From(info.rcMonitor);
        if (covers == _ownFullscreen.ContainsKey(window))
            return;

        if (covers)
            _ownFullscreen[window] = 0;
        else
            _ownFullscreen.TryRemove(window, out _);

        Apply(_core.Handle(new WindowFullscreenRequested(window, covers)));
    }

    private void PutBackIfDrifted(HWND hwnd, WindowId window)
    {
        var settling = _settling.TryGetValue(window, out var until) && Environment.TickCount64 <= until;
        if (!settling)
            _settling.TryRemove(window, out _);

        if (_moving == window || _sizing == window || _motions.Running(window) || _ownFullscreen.ContainsKey(window)
            || PInvoke.IsIconic(hwnd) || !TryVisible(window, out var visible))
            return;

        Apply(_core.Handle(new WindowDrifted(window, visible) { TiledOnly = !settling }));
    }
}
