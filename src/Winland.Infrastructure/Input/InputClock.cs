using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winland.Infrastructure;

internal static class InputClock
{
    internal const uint RecentMilliseconds = 250;

    private static int _watching;

    private static long _lastClickOrKey = -1;

    private static nint _clicked;

    internal static void Watch() => Volatile.Write(ref _watching, 1);

    internal static void NoteKey(uint time, int key, bool injected)
    {
        if (!injected || key is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5))
            NoteClickOrKey(time);
    }

    internal static void NoteClickOrKey(uint time, nint clicked = 0)
    {
        Volatile.Write(ref _clicked, clicked);
        Volatile.Write(ref _lastClickOrKey, time);
    }

    internal static WindowId? Clicked => Volatile.Read(ref _clicked) is var clicked and not 0 ? new WindowId(clicked) : null;

    internal static bool Recent(uint eventTime)
    {
        if (Volatile.Read(ref _watching) != 0)
        {
            var last = Volatile.Read(ref _lastClickOrKey);
            if (last < 0)
                return false;

            return unchecked(eventTime - (uint)last) <= RecentMilliseconds;
        }

        var info = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>() };
        if (!PInvoke.GetLastInputInfo(ref info))
            return false;

        return unchecked(eventTime - info.dwTime) <= RecentMilliseconds;
    }
}
