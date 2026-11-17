using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class Foreground
{
    private const byte MenuKey = 0x12;

    private static readonly Lock Gate = new();
    private static WindowId? _expected;
    private static long _expectedUntil;

    internal static FocusCause Classify(WindowId window, uint eventTime)
    {
        lock (Gate)
        {
            if (_expected == window && Environment.TickCount64 <= _expectedUntil)
            {
                _expected = null;
                return FocusCause.Requested;
            }
        }

        return InputClock.Recent(eventTime) ? FocusCause.User : FocusCause.Activated;
    }

    internal static bool MoveTo(WindowId window)
    {
        var hwnd = new HWND((nint)window.Value);
        if (hwnd.IsNull)
            return false;

        if (PInvoke.GetForegroundWindow() == hwnd)
            return true;

        if (PInvoke.IsHungAppWindow(hwnd) || WindowAdapter.IsCloaked(hwnd))
            return false;

        Expect(window);
        if (TrySet(hwnd))
            return true;

        if (TryAttached(hwnd))
            return true;

        TapMenuKey();
        if (TrySet(hwnd))
            return true;

        Flash(hwnd);
        Console.Error.WriteLine(
            "Could not restore focus to window 0x"
            + window.Value.ToString("x", CultureInfo.InvariantCulture)
            + ".");
        return false;
    }

    internal static void ToDesktop()
    {
        var shell = PInvoke.GetShellWindow();
        if (!shell.IsNull && PInvoke.GetForegroundWindow() != shell)
            _ = MoveTo(new WindowId((nint)shell));
    }

    private static void Expect(WindowId window)
    {
        lock (Gate)
        {
            _expected = window;
            _expectedUntil = Environment.TickCount64 + 1_000;
        }
    }

    private static bool TrySet(HWND hwnd)
    {
        _ = PInvoke.SetForegroundWindow(hwnd);
        return PInvoke.GetForegroundWindow() == hwnd;
    }

    private static bool TryAttached(HWND hwnd)
    {
        var foreground = PInvoke.GetForegroundWindow();
        var foregroundThread = PInvoke.GetWindowThreadProcessId(foreground, out _);
        var currentThread = PInvoke.GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == currentThread)
            return TrySet(hwnd);

        if (PInvoke.IsHungAppWindow(foreground))
            return false;

        if (!PInvoke.AttachThreadInput(foregroundThread, currentThread, true))
            return false;

        try
        {
            return TrySet(hwnd);
        }
        finally
        {
            _ = PInvoke.AttachThreadInput(foregroundThread, currentThread, false);
        }
    }

    private static void TapMenuKey() => KeyboardHook.Send(MenuTap(altHeld: PInvoke.GetAsyncKeyState(MenuKey) < 0));

    internal static INPUT[] MenuTap(bool altHeld) =>
        altHeld
            ? [KeyboardHook.Key(KeyboardHook.MaskKey, up: false), KeyboardHook.Key(KeyboardHook.MaskKey, up: true)]
            :
            [
                KeyboardHook.Key(MenuKey, up: false),
                KeyboardHook.Key(KeyboardHook.MaskKey, up: false),
                KeyboardHook.Key(KeyboardHook.MaskKey, up: true),
                KeyboardHook.Key(MenuKey, up: true),
            ];

    private static unsafe void Flash(HWND hwnd)
    {
        var info = new FLASHWINFO
        {
            cbSize = (uint)sizeof(FLASHWINFO),
            hwnd = hwnd,
            dwFlags = FLASHWINFO_FLAGS.FLASHW_ALL,
            uCount = 3,
            dwTimeout = 0,
        };
        _ = PInvoke.FlashWindowEx(&info);
    }
}
