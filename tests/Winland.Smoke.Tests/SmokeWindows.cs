using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Smoke.Tests;

internal sealed class SmokeWindows : IDisposable
{
    public const string ClassName = "WinlandSmokeWindow";

    private const uint InvokeMessage = 0x8001;
    private const int ClassAlreadyExists = 1410;

    private static readonly WNDPROC Proc = WindowProc;

    private readonly ConcurrentQueue<Action> _work = new();
    private readonly List<HWND> _opened = [];
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new(false);
    private uint _threadId;
    private bool _closed;

    public SmokeWindows()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "WinlandSmokeWindows",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_started.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("The test window thread did not start.");
    }

    public IReadOnlyList<HWND> Opened
    {
        get
        {
            lock (_opened)
                return [.. _opened];
        }
    }

    public HWND Open(string title, int width = 200, int height = 150, HWND owner = default)
    {
        var hwnd = Invoke(() =>
        {
            var origin = WorkArea();
            var created = Create(title, origin.left + 20, origin.top + 20, width, height, owner);
            _ = PInvoke.ShowWindow(created, SHOW_WINDOW_CMD.SW_SHOW);
            return created;
        });
        lock (_opened)
            _opened.Add(hwnd);

        return hwnd;
    }

    public void Invoke(Action work) => Invoke(() =>
    {
        work();
        return true;
    });

    public T Invoke<T>(Func<T> work)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        });
        if (!PInvoke.PostThreadMessage(_threadId, InvokeMessage, default, default))
            throw new InvalidOperationException($"The test window thread did not take work ({Marshal.GetLastPInvokeError()}).");

        if (!done.Task.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The test window thread did not finish its work.");

        return done.Task.GetAwaiter().GetResult();
    }

    public void Close()
    {
        if (_closed)
            return;

        try
        {
            Invoke(() =>
            {
                foreach (var hwnd in Opened)
                {
                    if (PInvoke.IsWindow(hwnd))
                        _ = PInvoke.DestroyWindow(hwnd);
                }

                PInvoke.PostQuitMessage(0);
            });
        }
        finally
        {
            _closed = true;
            _thread.Join(TimeSpan.FromSeconds(5));
            _started.Dispose();
        }
    }

    public void Dispose() => Close();

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();
        _ = PInvoke.PeekMessage(out _, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        Register();
        _started.Set();
        while (PInvoke.GetMessage(out var message, default, 0, 0) > 0)
        {
            if (message.message == InvokeMessage && message.hwnd.IsNull)
            {
                while (_work.TryDequeue(out var work))
                    work();

                continue;
            }

            PInvoke.TranslateMessage(in message);
            PInvoke.DispatchMessage(in message);
        }
    }

    private static unsafe void Register()
    {
        var module = PInvoke.GetModuleHandle((string?)null);
        fixed (char* name = ClassName)
        {
            var info = new WNDCLASSEXW
            {
                cbSize = IntPtr.Size == 8 ? 80u : 48u,
                lpfnWndProc = Proc,
                hInstance = (HINSTANCE)module.DangerousGetHandle(),
                hbrBackground = (HBRUSH)(nint)6,
                lpszClassName = name,
            };
            if (PInvoke.RegisterClassEx(in info) != 0)
                return;

            if (Marshal.GetLastPInvokeError() != ClassAlreadyExists)
                throw new InvalidOperationException($"The test window class was not registered ({Marshal.GetLastPInvokeError()}).");
        }
    }

    private static unsafe HWND Create(string title, int x, int y, int width, int height, HWND owner)
    {
        var hwnd = PInvoke.CreateWindowEx(
            owner.IsNull ? WINDOW_EX_STYLE.WS_EX_APPWINDOW : default,
            ClassName,
            title,
            WINDOW_STYLE.WS_OVERLAPPEDWINDOW | WINDOW_STYLE.WS_VISIBLE,
            x,
            y,
            width,
            height,
            owner,
            null,
            null,
            null);
        if (hwnd.IsNull)
            throw new InvalidOperationException($"The test window was not created ({Marshal.GetLastPInvokeError()}).");

        return hwnd;
    }

    private static unsafe RECT WorkArea()
    {
        var area = default(RECT);
        if (!PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA, 0, &area, 0))
            area = new RECT { left = 40, top = 40, right = 800, bottom = 600 };

        return area;
    }

    private static LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam) =>
        PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
}
