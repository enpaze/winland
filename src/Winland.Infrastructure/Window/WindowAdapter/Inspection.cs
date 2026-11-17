using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    internal WindowAppeared? Inspect(WindowId? window)
    {
        var handle = window ?? (PInvoke.GetCursorPos(out var cursor) ? KeyboardHook.TopLevelUnder(new Point(cursor.X, cursor.Y)) : null);
        if (handle is not WindowId read)
            return null;

        var hwnd = new HWND((nint)read.Value);
        return PInvoke.IsWindow(hwnd) && TryDescribe(hwnd, read, out var appeared, inspecting: true) && PInvoke.IsWindow(hwnd)
            ? appeared
            : null;
    }
}
