using System.Collections.Concurrent;
using Windows.Win32.Foundation;

namespace Winland.Infrastructure;

internal static class StandFrames
{
    private static readonly ConcurrentDictionary<WindowId, (Rectangle Frame, HWND Overlay)> Shown = new();

    internal static bool TryGet(WindowId window, out Rectangle frame, out HWND overlay)
    {
        if (Shown.TryGetValue(window, out var shown))
        {
            (frame, overlay) = shown;
            return true;
        }

        frame = default;
        overlay = HWND.Null;
        return false;
    }

    internal static void Set(WindowId window, Rectangle frame, nint overlay) => Shown[window] = (frame, new HWND(overlay));

    internal static void Remove(WindowId window) => Shown.TryRemove(window, out _);

    internal static void Follow(WindowId window)
    {
        BorderHost.Follow(window);
    }
}
