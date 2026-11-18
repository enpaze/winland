using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Verify.Tests;

internal static class Desktop
{
    public static void Chord(VIRTUAL_KEY key, params VIRTUAL_KEY[] modifiers)
    {
        foreach (var modifier in modifiers)
            Key(modifier, up: false);

        Key(key, up: false);
        Key(key, up: true);
        foreach (var modifier in modifiers.Reverse())
            Key(modifier, up: true);
    }

    public static void Key(VIRTUAL_KEY key, bool up)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0;
        Send(input);
        Thread.Sleep(20);
    }

    public static void MoveMouse(int x, int y)
    {
        var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dx = (int)Math.Round((x - left) * 65535.0 / Math.Max(1, width - 1));
        input.Anonymous.mi.dy = (int)Math.Round((y - top) * 65535.0 / Math.Max(1, height - 1));
        input.Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;
        Send(input);
        Thread.Sleep(15);
    }

    public static void Glide(int fromX, int fromY, int toX, int toY, int steps = 20)
    {
        for (var step = 0; step <= steps; step++)
            MoveMouse(fromX + ((toX - fromX) * step / steps), fromY + ((toY - fromY) * step / steps));
    }

    public static void Button(bool right, bool up)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dwFlags = (right, up) switch
        {
            (false, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
            (false, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
            (true, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
            (true, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
        };
        Send(input);
        Thread.Sleep(30);
    }

    public static (int X, int Y) Cursor() => PInvoke.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);

    public static List<HWND> WindowsOf(int processId, string className)
    {
        var found = new List<HWND>();
        _ = PInvoke.EnumWindows(
            (hwnd, parameter) =>
            {
                _ = PInvoke.GetWindowThreadProcessId(hwnd, out var owner);
                if (owner == (uint)processId && ClassOf(hwnd) == className)
                    found.Add(hwnd);

                return true;
            },
            default);
        return found;
    }

    public static unsafe string ClassOf(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        fixed (char* chars = buffer)
        {
            var length = PInvoke.GetClassName(hwnd, chars, buffer.Length);
            return new string(buffer[..Math.Max(0, length)]);
        }
    }

    public static Box Rect(HWND hwnd) => PInvoke.GetWindowRect(hwnd, out var rect) ? Box.From(rect) : default;

    public static byte? Alpha(HWND hwnd)
    {
        var extended = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (!extended.HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED))
            return null;

        Span<byte> alpha = stackalloc byte[1];
        return PInvoke.GetLayeredWindowAttributes(hwnd, out _, alpha, out var flags) && flags.HasFlag(LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA)
            ? alpha[0]
            : (byte)255;
    }

    public static unsafe List<string> MenuItems(HMENU menu)
    {
        var items = new List<string>();
        var count = PInvoke.GetMenuItemCount(menu);
        Span<char> buffer = stackalloc char[128];
        for (var i = 0; i < count; i++)
        {
            fixed (char* chars = buffer)
            {
                var length = PInvoke.GetMenuString(menu, (uint)i, chars, buffer.Length, MENU_ITEM_FLAGS.MF_BYPOSITION);
                items.Add(new string(buffer[..Math.Max(0, length)]));
            }
        }

        return items;
    }

    private static void Send(INPUT input)
    {
        if (PInvoke.SendInput([input], Marshal.SizeOf<INPUT>()) != 1)
            throw new InvalidOperationException($"SendInput was refused ({Marshal.GetLastPInvokeError()}).");
    }
}
