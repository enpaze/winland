using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    /// <summary>
    /// inspect's read of a Window, afresh: <paramref name="window"/>, or with null the top-level Window under the pointer.
    /// Null when there is none, or it closed before the read finished. It changes nothing.
    /// </summary>
    /// <remarks>
    /// The core waits while this reads. Neither a Client nor the window under the pointer is ever one of Winland's own,
    /// which would answer on a thread that may be waiting for the core.
    /// </remarks>
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
