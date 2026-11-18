using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class WindowClass
{
    private const int ClassAlreadyExists = 1410;

    internal static unsafe void Register(string name, WNDPROC proc, string what)
    {
        var module = PInvoke.GetModuleHandle((string?)null);
        var instance = (HINSTANCE)module.DangerousGetHandle();
        module.SetHandleAsInvalid();
        fixed (char* className = name)
        {
            var info = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = proc,
                hInstance = instance,
                lpszClassName = className,
            };
            if (PInvoke.RegisterClassEx(in info) != 0)
                return;

            if (Marshal.GetLastPInvokeError() != ClassAlreadyExists)
                throw new InvalidOperationException($"The {what} window class was not registered ({Marshal.GetLastPInvokeError()}).");
        }
    }
}
