using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal sealed class Win32Desktop : IDesktop
{

    private static readonly HWND Top = new(0);
    private static readonly HWND Bottom = new(1);
    private static readonly HWND Topmost = new(-1);
    private static readonly HWND NotTopmost = new(-2);

    private const long SlowMilliseconds = 250;
    private const long SlowForMilliseconds = 3000;
    private const uint ColorKeyFlag = 0x1;
    private const DWMWINDOWATTRIBUTE BackdropAttribute = (DWMWINDOWATTRIBUTE)38;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, long> _slowUntil = new();

    public bool TryRead(WindowId window, out WindowChrome chrome)
    {
        chrome = default;
        var hwnd = Handle(window);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return false;

        var exStyle = (uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var colorKey = false;
        byte alpha = 255;
        if ((exStyle & (uint)WINDOW_EX_STYLE.WS_EX_LAYERED) != 0)
            _ = TryReadLayered(hwnd, out colorKey, out alpha);

        chrome = new WindowChrome(exStyle, colorKey, alpha);
        return true;
    }

    public bool TryWriteExStyle(WindowId window, uint exStyle)
    {
        var hwnd = Handle(window);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return false;

        Marshal.SetLastPInvokeError(0);
        var previous = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
            return false;

        if ((exStyle & ~(uint)previous) == (uint)WINDOW_EX_STYLE.WS_EX_LAYERED)
            return true;

        _ = PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE
            | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
            | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
            | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
        return true;
    }

    public bool TrySetAlpha(WindowId window, byte alpha)
    {
        var hwnd = Handle(window);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return false;

        return PInvoke.SetLayeredWindowAttributes(hwnd, default, alpha, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
    }

    private static unsafe bool TryReadLayered(HWND hwnd, out bool colorKey, out byte alpha)
    {
        colorKey = false;
        alpha = 255;
        Span<byte> value = stackalloc byte[1];
        if (!PInvoke.GetLayeredWindowAttributes(hwnd, out _, value, out var flags))
            return false;

        colorKey = ((uint)flags & ColorKeyFlag) != 0;
        alpha = value[0];
        return true;
    }

    public bool TryReadStyle(WindowId window, out uint style)
    {
        style = 0;
        var hwnd = Handle(window);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return false;

        style = (uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        return true;
    }

    public bool TryWriteStyle(WindowId window, uint style)
    {
        var hwnd = Handle(window);
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return false;

        Marshal.SetLastPInvokeError(0);
        var previous = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
            return false;

        _ = PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE
            | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
            | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
            | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
        return true;
    }

    public unsafe bool TryReadBackdrop(WindowId window, out uint backdrop)
    {
        backdrop = 0;
        if (!TryWindow(window, out var hwnd))
            return false;

        Span<byte> value = stackalloc byte[sizeof(uint)];
        if (PInvoke.DwmGetWindowAttribute(hwnd, BackdropAttribute, value).Failed)
            return false;

        backdrop = MemoryMarshal.Read<uint>(value);
        return true;
    }

    public bool TrySetBackdrop(WindowId window, uint backdrop) => TrySet(window, BackdropAttribute, backdrop);

    public bool HasRegion(WindowId window)
    {
        if (!TryWindow(window, out var hwnd))
            return false;

        var probe = PInvoke.CreateRectRgn(0, 0, 0, 0);
        if (probe.IsNull)
            return false;

        try
        {
            return PInvoke.GetWindowRgn(hwnd, probe) != GDI_REGION_TYPE.RGN_ERROR;
        }
        finally
        {
            PInvoke.DeleteObject(probe);
        }
    }

    public bool TryFrame(WindowId window, out Rectangle outer, out Rectangle visible)
    {
        outer = default;
        visible = default;
        if (!TryWindow(window, out var hwnd) || PInvoke.IsIconic(hwnd) || !PInvoke.GetWindowRect(hwnd, out var rect))
            return false;

        outer = WindowRect.From(rect);
        visible = WindowRect.TryFrameBounds(hwnd, out var bounds) ? bounds : outer;
        return true;
    }

    public bool TrySetRegion(WindowId window, IReadOnlyList<Point> outline)
    {
        if (!TryWindow(window, out var hwnd) || PInvoke.IsHungAppWindow(hwnd))
            return false;

        if (outline.Count == 0)
            return PInvoke.SetWindowRgn(hwnd, HRGN.Null, true) != 0;

        if (outline.Count == 1)
        {
            var empty = PInvoke.CreateRectRgn(0, 0, 0, 0);
            if (empty.IsNull)
                return false;

            if (PInvoke.SetWindowRgn(hwnd, empty, true) == 0)
            {
                _ = PInvoke.DeleteObject(empty);
                return false;
            }

            return true;
        }

        var points = new System.Drawing.Point[outline.Count];
        for (var i = 0; i < outline.Count; i++)
            points[i] = new System.Drawing.Point(outline[i].X, outline[i].Y);

        using var region = PInvoke.CreatePolygonRgn(points, CREATE_POLYGON_RGN_MODE.WINDING);
        if (region.IsInvalid || PInvoke.SetWindowRgn(hwnd, (HRGN)region.DangerousGetHandle(), true) == 0)
            return false;

        region.SetHandleAsInvalid();
        return true;
    }

    private static unsafe bool TrySet(WindowId window, DWMWINDOWATTRIBUTE attribute, uint value) =>
        TryWindow(window, out var hwnd) && PInvoke.DwmSetWindowAttribute(hwnd, attribute, &value, sizeof(uint)).Succeeded;

    private static bool TryWindow(WindowId window, out HWND hwnd)
    {
        hwnd = Handle(window);
        return !hwnd.IsNull && PInvoke.IsWindow(hwnd);
    }

    public bool Exists(WindowId window) => PInvoke.IsWindow(Handle(window));

    public bool Shown(WindowId window) => PInvoke.IsWindowVisible(Handle(window));

    public bool Minimized(WindowId window) => PInvoke.IsIconic(Handle(window));

    public bool Hung(WindowId window)
    {
        if (PInvoke.IsHungAppWindow(Handle(window)))
            return true;

        if (!_slowUntil.TryGetValue(window, out var until))
            return false;

        if (Environment.TickCount64 < until)
            return true;

        _slowUntil.TryRemove(window, out _);
        return false;
    }

    public unsafe bool InMoveSize(WindowId window)
    {
        var thread = PInvoke.GetWindowThreadProcessId(Handle(window), out _);
        if (thread == 0)
            return false;

        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        return PInvoke.GetGUIThreadInfo(thread, &info) && info.flags.HasFlag(GUITHREADINFO_FLAGS.GUI_INMOVESIZE);
    }

    private void Timed(WindowId window, Action call)
    {
        var start = Environment.TickCount64;
        call();
        var now = Environment.TickCount64;
        if (now - start < SlowMilliseconds)
            return;

        foreach (var (slow, until) in _slowUntil)
        {
            if (now >= until)
                _slowUntil.TryRemove(slow, out _);
        }

        _slowUntil[window] = now + SlowForMilliseconds;
    }

    public int Dpi(WindowId window) => (int)PInvoke.GetDpiForWindow(Handle(window));

    public bool Repainting(WindowId window) => PInvoke.GetUpdateRect(Handle(window), out _, false);

    public bool TryOuter(WindowId window, out Rectangle outer)
    {
        outer = default;
        if (!PInvoke.GetWindowRect(Handle(window), out var rect))
            return false;

        outer = WindowRect.From(rect);
        return true;
    }

    public bool TryVisible(WindowId window, out Rectangle visible) => WindowRect.TryFrameBounds(Handle(window), out visible);

    public unsafe void SetAttribute(WindowId window, int attribute, uint value) =>
        _ = PInvoke.DwmSetWindowAttribute(Handle(window), (DWMWINDOWATTRIBUTE)attribute, &value, sizeof(uint));

    public void SetBounds(WindowId window, Rectangle outer, Delivery delivery)
    {
        var flags = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (delivery != Delivery.Wait)
            flags |= SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;
        if (delivery == Delivery.PostKeepingSize)
            flags |= SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

        if (delivery == Delivery.Wait)
            Timed(window, () => PInvoke.SetWindowPos(Handle(window), HWND.Null, outer.X, outer.Y, outer.Width, outer.Height, flags));
        else
            _ = PInvoke.SetWindowPos(Handle(window), HWND.Null, outer.X, outer.Y, outer.Width, outer.Height, flags);
    }

    public void Show(WindowId window, ShowAs show) => Timed(window, () => PInvoke.ShowWindow(Handle(window), Command(show)));

    public void ShowAsync(WindowId window, ShowAs show) => PInvoke.ShowWindowAsync(Handle(window), Command(show));

    public void Raise(WindowId window, bool top) => Timed(window, () => Restack(window, top, 0));

    public void RaiseAsync(WindowId window, bool top) => Restack(window, top, SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS);

    public unsafe bool Painted(WindowId window) => !PInvoke.GetUpdateRect(Handle(window), null, false);

    public void Under(WindowId window, WindowId above) =>
        _ = PInvoke.SetWindowPos(
            Handle(window),
            Handle(above),
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    public bool HoldOnTop(WindowId window)
    {
        if ((PInvoke.GetWindowLong(Handle(window), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0)
            return false;

        Band(window, Topmost);
        return true;
    }

    public void LetGo(WindowId window) => Band(window, NotTopmost);

    public bool Foreground(WindowId window) => Winland.Infrastructure.Foreground.MoveTo(window);

    public bool IsForeground(WindowId window) => PInvoke.GetForegroundWindow() == Handle(window);

    public void ForegroundDesktop() => Winland.Infrastructure.Foreground.ToDesktop();

    public void Close(WindowId window) => PInvoke.PostMessage(Handle(window), PInvoke.WM_CLOSE, default, default);

    public void Pace()
    {
        if (PresentClock.WaitForFrame())
            return;

        if (PInvoke.DwmFlush().Failed)
            Thread.Sleep(15);
    }

    public IStandInSurface? OpenStandIn(WindowId window)
    {
        var hwnd = Handle(window);
        var overlay = StandInHost.Create();
        if (overlay.IsNull)
            return null;

        if (PInvoke.DwmRegisterThumbnail(overlay, hwnd, out var thumbnail).Failed)
        {
            StandInHost.Close(overlay);
            return null;
        }

        return new Win32StandInSurface(hwnd, overlay, thumbnail);
    }

    public void StopStandIns() => StandInHost.Stop();

    private static void Restack(WindowId window, bool top, SET_WINDOW_POS_FLAGS flags) =>
        _ = PInvoke.SetWindowPos(
            Handle(window),
            top ? Top : Bottom,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | flags);

    private static void Band(WindowId window, HWND after) =>
        _ = PInvoke.SetWindowPos(
            Handle(window),
            after,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    private static SHOW_WINDOW_CMD Command(ShowAs show) => show switch
    {
        ShowAs.Hide => SHOW_WINDOW_CMD.SW_HIDE,
        ShowAs.ShowMinimized => SHOW_WINDOW_CMD.SW_SHOWMINNOACTIVE,
        _ => SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE,
    };

    private static HWND Handle(WindowId window) => new((nint)window.Value);
}

internal sealed class Win32StandInSurface(HWND window, HWND overlay, nint picture) : IStandInSurface
{
    private const uint ThumbnailParts = 0x1 | 0x2 | 0x4 | 0x8 | 0x10;
    private const uint ThumbnailOpacity = 0x4;

    private nint _right;
    private nint _bottom;
    private bool _closed;

    public nint Handle => (nint)overlay;

    public Size? PictureSize() =>
        PInvoke.DwmQueryThumbnailSourceSize(picture, out var size).Succeeded ? new Size(size.Width, size.Height) : null;

    public void Place(Rectangle bounds)
    {
        var above = PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_HWNDPREV);
        var placed = above == overlay ? SET_WINDOW_POS_FLAGS.SWP_NOZORDER : 0;
        _ = PInvoke.SetWindowPos(
            overlay,
            above,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | placed);
    }

    public void Show(StandInSlot slot, StandInPiece? piece, byte alpha)
    {
        if (slot == StandInSlot.Right && piece is not null && _right == 0)
            _right = EdgeThumbnail();
        if (slot == StandInSlot.Bottom && piece is not null && _bottom == 0)
            _bottom = EdgeThumbnail();

        var thumbnail = slot switch
        {
            StandInSlot.Right => _right,
            StandInSlot.Bottom => _bottom,
            _ => picture,
        };
        if (thumbnail == 0)
            return;

        var source = piece?.Source ?? default;
        var destination = piece?.Destination ?? default;
        var properties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = ThumbnailParts,
            rcDestination = new RECT(destination.X, destination.Y, destination.X + destination.Width, destination.Y + destination.Height),
            rcSource = new RECT(source.X, source.Y, source.X + source.Width, source.Y + source.Height),
            opacity = alpha,
            fVisible = piece is not null,
            fSourceClientAreaOnly = false,
        };
        _ = PInvoke.DwmUpdateThumbnailProperties(thumbnail, in properties);
    }

    public void SetAlpha(byte alpha)
    {
        var properties = new DWM_THUMBNAIL_PROPERTIES { dwFlags = ThumbnailOpacity, opacity = alpha };
        foreach (var thumbnail in (ReadOnlySpan<nint>)[picture, _right, _bottom])
        {
            if (thumbnail != 0)
                _ = PInvoke.DwmUpdateThumbnailProperties(thumbnail, in properties);
        }
    }

    public void Hide() => PInvoke.ShowWindow(overlay, SHOW_WINDOW_CMD.SW_HIDE);

    public void Close()
    {
        if (_closed)
            return;

        _closed = true;
        foreach (var thumbnail in (ReadOnlySpan<nint>)[picture, _right, _bottom])
        {
            if (thumbnail != 0)
                _ = PInvoke.DwmUnregisterThumbnail(thumbnail);
        }

        StandInHost.Close(overlay);
    }

    private nint EdgeThumbnail() =>
        PInvoke.DwmRegisterThumbnail(overlay, window, out var thumbnail).Succeeded ? thumbnail : 0;
}
