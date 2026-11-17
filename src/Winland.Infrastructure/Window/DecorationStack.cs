using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class DecorationStack
{
    internal const string BorderClass = "WinlandBorder";
    internal const string GroupBarClass = "WinlandGroupBar";

    internal static HWND Above(HWND target, List<HWND> chain)
    {
        var above = PInvoke.GetWindow(target, GET_WINDOW_CMD.GW_HWNDPREV);
        while (!above.IsNull && IsDecoration(above))
        {
            chain.Add(above);
            above = PInvoke.GetWindow(above, GET_WINDOW_CMD.GW_HWNDPREV);
        }

        return above;
    }

    private static bool IsDecoration(HWND hwnd) => WindowAdapter.ClassName(hwnd) is BorderClass or GroupBarClass;
}
