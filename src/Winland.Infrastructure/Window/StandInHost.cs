using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class StandInHost
{
    private const string HostClass = "WinlandStandInHost";
    private const string StandInClass = "WinlandStandIn";
    private const uint CreateMessage = 0x8000 + 0x48;

    private static readonly WNDPROC Proc = OnWindow;
    private static readonly Lock Gate = new();
    private static HWND _host;
    private static uint _thread;
    private static Thread? _worker;

    internal static HWND Create()
    {
        HWND host;
        lock (Gate)
        {
            if (_worker is null)
                Start();

            host = _host;
        }

        if (host.IsNull)
            return HWND.Null;

        return new HWND((nint)PInvoke.SendMessage(host, CreateMessage, default, default).Value);
    }

    internal static void Close(HWND standIn)
    {
        if (!standIn.IsNull)
            _ = PInvoke.SendMessage(standIn, PInvoke.WM_CLOSE, default, default);
    }

    internal static void Stop()
    {
        Thread? worker;
        uint thread;
        lock (Gate)
        {
            worker = _worker;
            thread = _thread;
            _worker = null;
            _thread = 0;
            _host = HWND.Null;
        }

        if (worker is null)
            return;

        if (thread != 0)
            _ = PInvoke.PostThreadMessage(thread, PInvoke.WM_QUIT, default, default);

        _ = worker.Join(TimeSpan.FromSeconds(2));
    }

    private static void Start()
    {
        using var ready = new ManualResetEventSlim();
        _worker = new Thread(() => Run(ready))
        {
            IsBackground = true,
            Name = "Winland stand-ins",
        };
        _worker.Start();

        if (ready.Wait(TimeSpan.FromSeconds(2)) && _host.IsNull)
            _worker = null;
    }

    private static void Run(ManualResetEventSlim ready)
    {
        try
        {
            WindowClass.Register(HostClass, Proc, "stand-in");
            WindowClass.Register(StandInClass, Proc, "stand-in");
            var host = CreateHost();
            if (host.IsNull)
                return;

            _host = host;
            _thread = PInvoke.GetCurrentThreadId();
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return;
        }
        finally
        {
            ready.Set();
        }

        MessageLoop.Run();
    }

    private static LRESULT OnWindow(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == CreateMessage)
            return new LRESULT((nint)CreateStandIn());

        if (message == PInvoke.WM_MOUSEACTIVATE)
            return new LRESULT((nint)PInvoke.MA_NOACTIVATE);

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static unsafe HWND CreateHost() =>
        PInvoke.CreateWindowEx(0, HostClass, "", 0, 0, 0, 0, 0, new HWND(-3), null, null, null);

    private static unsafe HWND CreateStandIn() =>
        PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP,
            StandInClass,
            "",
            WINDOW_STYLE.WS_POPUP,
            0,
            0,
            0,
            0,
            HWND.Null,
            null,
            null,
            null);
}
