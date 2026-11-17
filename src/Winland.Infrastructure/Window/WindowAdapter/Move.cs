using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    private const int HitTestTimeoutMs = 100;

    private WindowId? _moving;
    private WindowId? _sizing;

    private UndecidedDrag? _undecided;

    private void BeginMove(HWND hwnd, WindowId window)
    {
        if (_moving is not null || _sizing is not null || !PInvoke.GetCursorPos(out var cursor))
            return;

        var pointer = new Point(cursor.X, cursor.Y);
        var kind = GrabbedKind(hwnd, cursor);
        if (kind != DragKind.Unclear)
        {
            _ = StartDrag(window, kind == DragKind.Size, pointer);
            return;
        }

        if (TryOuter(window, out var outer))
            _undecided = new UndecidedDrag(window, outer, pointer);
        else
            _ = StartDrag(window, sizing: false, pointer);
    }

    private bool Decide(HWND hwnd, WindowId window)
    {
        if (_undecided is not { } drag || drag.Window != window)
            return false;

        if (!TryOuter(window, out var now))
            return true;

        var kind = SizingGrab.Resolve(drag.Outer, now);
        if (kind == DragKind.Unclear)
            return true;

        _undecided = null;
        _ = StartDrag(window, kind == DragKind.Size, drag.Pointer);
        if (kind == DragKind.Size)
            FollowSizing(window);

        return true;
    }

    private readonly record struct UndecidedDrag(WindowId Window, Rectangle Outer, Point Pointer);

    internal bool StartDrag(WindowId window, bool sizing, Point pointer)
    {
        if (_moving is not null || _sizing is not null)
            return false;


        if (sizing)
        {
            _sizing = window;
            var bounds = TryVisible(window, out var visible) ? visible : (Rectangle?)null;
            Apply(_core.Handle(new WindowSizeStarted(window, pointer, bounds)));
            return true;
        }

        _moving = window;
        Apply(_core.Handle(new WindowMoveStarted(window, pointer)));
        return true;
    }

    private void FollowSizing(WindowId window)
    {
        if (PInvoke.GetCursorPos(out var cursor))
        {
            var bounds = TryVisible(window, out var visible) ? visible : (Rectangle?)null;
            Apply(_core.Handle(new WindowSizing(window, new Point(cursor.X, cursor.Y), bounds)));
        }
    }

    private void EndMove(WindowId window)
    {
        if (_undecided is { } drag && drag.Window == window)
            _undecided = null;

        var pointer = PInvoke.GetCursorPos(out var cursor) ? new Point(cursor.X, cursor.Y) : default;
        EndDrag(window, pointer, TryVisible(window, out var dropped) ? dropped : null);
    }

    private void EndDrag(WindowId window, Point pointer, Rectangle? dropped)
    {
        if (_moving == window)
        {
            _moving = null;
            var hwnd = new HWND((nint)window.Value);
            RereadLimits(hwnd, window);
            Apply(_core.Handle(new WindowMoveEnded(window, pointer, dropped)));
        }
        else if (_sizing == window)
        {
            _sizing = null;
            Apply(_core.Handle(new WindowSizeEnded(window, pointer, dropped)));
        }
    }

    private void AbandonDrag(WindowId window)
    {
        if (_undecided is { } drag && drag.Window == window)
            _undecided = null;

        if (_moving != window && _sizing != window)
            return;

        var pointer = PInvoke.GetCursorPos(out var cursor) ? new Point(cursor.X, cursor.Y) : default;
        EndDrag(window, pointer, null);
    }

    private static unsafe DragKind GrabbedKind(HWND hwnd, System.Drawing.Point cursor)
    {
        nuint hit = 0;
        var packed = (nint)(((cursor.Y & 0xFFFF) << 16) | (cursor.X & 0xFFFF));
        var sent = PInvoke.SendMessageTimeout(
            hwnd,
            PInvoke.WM_NCHITTEST,
            default,
            new LPARAM(packed),
            SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ERRORONEXIT,
            HitTestTimeoutMs,
            &hit);
        return SizingGrab.Classify(sent == 0 ? null : (int)hit);
    }
}
