using Windows.Win32;

namespace Winland.Infrastructure;

internal static class Magnifier
{
    private static readonly Lock Gate = new();
    private static readonly AutoResetEvent Wake = new(false);
    private static bool _cursorHidden;
    private static bool _cursorShownHidden;
    private static Thread? _worker;
    private static bool _stopping;
    private static bool _started;

    internal static void HideCursor(bool hidden)
    {
        lock (Gate)
        {
            if (hidden == _cursorHidden || _stopping)
                return;

            _cursorHidden = hidden;
            Start();
        }

        _ = Wake.Set();
    }

    internal static void Stop()
    {
        Thread? worker;
        lock (Gate)
        {
            worker = _worker;
            _stopping = true;
        }

        if (worker is null)
            return;

        _ = Wake.Set();
        _ = worker.Join(TimeSpan.FromSeconds(2));
    }

    private static void Start()
    {
        if (_worker is not null)
            return;

        _worker = new Thread(Run) { IsBackground = true, Name = "Winland magnifier" };
        _worker.Start();
    }

    private static void Run()
    {
        try
        {
            while (true)
            {
                _ = Wake.WaitOne();
                bool hidden;
                lock (Gate)
                {
                    if (_stopping)
                        break;

                    hidden = _cursorHidden;
                }

                ShowCursor(hidden);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(exception);
        }
        finally
        {
            if (_started)
            {
                _ = PInvoke.MagShowSystemCursor(true);
                _ = PInvoke.MagUninitialize();
                _started = false;
            }
        }
    }

    private static void ShowCursor(bool hidden)
    {
        if (hidden == _cursorShownHidden || !Started())
            return;

        if (PInvoke.MagShowSystemCursor(!hidden))
            _cursorShownHidden = hidden;
    }

    private static bool Started()
    {
        if (!_started && PInvoke.MagInitialize())
            _started = true;

        return _started;
    }
}
