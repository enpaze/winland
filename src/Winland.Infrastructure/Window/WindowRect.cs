using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Winland.Infrastructure;

internal static class WindowRect
{
    internal static Rectangle From(RECT rect) =>
        new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);

    internal static unsafe bool TryFrameBounds(HWND hwnd, out Rectangle bounds)
    {
        bounds = default;
        RECT rect;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed)
            return false;

        bounds = From(rect);
        return true;
    }
}
