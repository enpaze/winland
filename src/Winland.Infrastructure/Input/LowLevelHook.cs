using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class LowLevelHook
{
    internal static bool Reinstall(ref HHOOK current, WINDOWS_HOOK_ID id, HOOKPROC callback, string what)
    {
        var hook = PInvoke.SetWindowsHookEx(id, callback, default(HINSTANCE), 0);
        if (hook.IsNull)
        {
            Console.Error.WriteLine($"The {what} hook was not reinstalled ({Marshal.GetLastPInvokeError()}).");
            return false;
        }

        var old = current;
        current = hook;
        if (!old.IsNull)
            _ = PInvoke.UnhookWindowsHookEx(old);

        return true;
    }
}
