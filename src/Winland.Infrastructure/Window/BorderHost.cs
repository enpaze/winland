using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure;

internal static class BorderHost
{
    private const string ClassName = DecorationStack.BorderClass;
    private const uint SyncMessage = 0x8000 + 0x4B;
    private static readonly HWND Top = new(0);
    private static readonly HWND Topmost = new(-1);
    private static readonly HWND NotTopmost = new(-2);

    private static readonly WNDPROC Proc = OnWindow;
    private static readonly Lock Gate = new();
    private static readonly Dictionary<WindowId, Entry> Entries = [];
    private static readonly HashSet<WindowId> Moved = [];
    private static readonly Dictionary<WindowId, BorderColor?> Tints = [];
    private static Dictionary<WindowId, BorderRing>? _pending;
    private static HashSet<WindowId> _keep = [];
    private static bool _restack;
    private static bool _posted;
    private static uint _thread;
    private static Thread? _worker;

    internal static void Update(IReadOnlyDictionary<WindowId, BorderRing> rings, IEnumerable<WindowId>? keep = null)
    {
        ArgumentNullException.ThrowIfNull(rings);
        lock (Gate)
        {
            if (_worker is null && rings.Count == 0)
                return;

            _pending = new Dictionary<WindowId, BorderRing>(rings);
            _keep = keep is null ? [] : [.. keep];
            Post();
        }
    }

    internal static void Restack()
    {
        lock (Gate)
        {
            if (_worker is null || Entries.Count == 0)
                return;

            _restack = true;
            Post();
        }
    }

    internal static void Follow(WindowId window)
    {
        lock (Gate)
        {
            if (_worker is null || !Entries.ContainsKey(window) && _pending?.ContainsKey(window) != true)
                return;

            Moved.Add(window);
            Post();
        }
    }

    internal static void Tint(WindowId window, BorderColor? color)
    {
        lock (Gate)
        {
            if (_worker is null)
                return;

            if (color is null && !Entries.ContainsKey(window) && _pending?.ContainsKey(window) != true)
            {
                Tints.Remove(window);
                return;
            }

            Tints[window] = color;
            Moved.Add(window);
            Post();
        }
    }

    internal static void Forget(WindowId window)
    {
        lock (Gate)
            Tints.Remove(window);
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
            _pending = null;
            _keep = [];
            _restack = false;
            _posted = false;
            Moved.Clear();
            Tints.Clear();
        }

        if (worker is null)
            return;

        if (thread != 0)
            _ = PInvoke.PostThreadMessage(thread, PInvoke.WM_QUIT, default, default);

        _ = worker.Join(TimeSpan.FromSeconds(2));
    }

    private static void Post()
    {
        if (_worker is null)
        {
            _worker = new Thread(Run) { IsBackground = true, Name = "Winland borders" };
            _worker.Start();
        }

        if (_posted || _thread == 0)
            return;

        _posted = true;
        _ = PInvoke.PostThreadMessage(_thread, SyncMessage, default, default);
    }

    private static void Run()
    {
        _ = PInvoke.PeekMessage(out _, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        try
        {
            WindowClass.Register(ClassName, Proc, "border");
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return;
        }

        lock (Gate)
        {
            if (!ReferenceEquals(_worker, Thread.CurrentThread))
                return;

            _thread = PInvoke.GetCurrentThreadId();
            _posted = true;
            _ = PInvoke.PostThreadMessage(_thread, SyncMessage, default, default);
        }

        while (PInvoke.GetMessage(out var message, default, 0, 0) > 0)
        {
            try
            {
                if (message.hwnd.IsNull && message.message == SyncMessage)
                {
                    Sync();
                    continue;
                }

                PInvoke.TranslateMessage(in message);
                PInvoke.DispatchMessage(in message);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine(exception);
            }
        }

        lock (Gate)
        {
            foreach (var entry in Entries.Values)
                entry.Dispose();

            Entries.Clear();
        }
    }

    private static void Sync()
    {
        Dictionary<WindowId, BorderRing>? wanted;
        HashSet<WindowId> keep;
        bool restack;
        WindowId[] moved;
        Dictionary<WindowId, BorderColor?> tints;
        lock (Gate)
        {
            wanted = _pending;
            _pending = null;
            keep = _keep;
            _keep = [];
            restack = _restack;
            _restack = false;
            moved = [.. Moved];
            Moved.Clear();
            tints = new Dictionary<WindowId, BorderColor?>(Tints);
            _posted = false;
        }

        var touched = new HashSet<WindowId>(moved);
        List<Entry> gone = [];
        if (wanted is not null)
        {
            lock (Gate)
            {
                foreach (var window in Entries.Keys.ToArray())
                {
                    if (wanted.ContainsKey(window) || keep.Contains(window))
                        continue;

                    gone.Add(Entries[window]);
                    Entries.Remove(window);
                    Tints.Remove(window);
                }

                foreach (var (window, ring) in wanted)
                {
                    if (!Entries.TryGetValue(window, out var entry))
                    {
                        entry = new Entry(window);
                        Entries[window] = entry;
                    }
                    else if (entry.Ring.Equals(ring))
                    {
                        continue;
                    }

                    entry.Ring = ring;
                    touched.Add(window);
                }
            }
        }

        foreach (var window in touched)
        {
            Entry? entry;
            lock (Gate)
                Entries.TryGetValue(window, out entry);

            if (entry is null)
                continue;

            entry.Tint = tints.GetValueOrDefault(window);
            entry.Place();
        }

        if (restack)
        {
            Entry[] entries;
            lock (Gate)
                entries = [.. Entries.Where(pair => !touched.Contains(pair.Key)).Select(pair => pair.Value)];

            foreach (var entry in entries)
                entry.Restack();
        }

        foreach (var entry in gone)
            entry.Dispose();
    }

    private static LRESULT OnWindow(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam) =>
        message == PInvoke.WM_MOUSEACTIVATE ? new LRESULT((nint)PInvoke.MA_NOACTIVATE) : PInvoke.DefWindowProc(hwnd, message, wParam, lParam);

    private static unsafe HWND Create() =>
        PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE
                | WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
            ClassName,
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

    private sealed class Entry(WindowId window) : IDisposable
    {
        private readonly HWND[] _pieces = new HWND[4];

        private readonly bool[] _drawn = new bool[4];
        private Rectangle _box;
        private BorderRing? _painted;
        private bool _shown;
        private HWND _on;
        private byte _alpha = 255;
        private byte _paintedAlpha = 255;

        public BorderRing Ring { get; set; } = new(default, 0, 0, 0);

        public BorderColor? Tint { get; set; }

        private BorderRing Drawn(bool fading)
        {
            var ring = Tint is BorderColor tint ? Ring with { Color = tint } : Ring;
            return fading ? ring with { Alpha = 255 } : ring;
        }

        public void Place()
        {
            var target = new HWND((nint)window.Value);
            var frame = default(Rectangle);

            var standing = StandFrames.TryGet(window, out var shown, out var overlay);
            if (standing)
                frame = shown;
            var fading = MotionAlpha.TryGet(window, out _alpha);
            if (!fading)
                _alpha = 255;
            if (_alpha == 0 || !Visible(target) || !(standing ? frame.Width > 0 && frame.Height > 0 : TryFrame(target, out frame)) || Ring.Thickness <= 0)
            {
                Hide();
                return;
            }

            var thickness = Ring.Thickness;
            var box = new Rectangle(frame.X - thickness, frame.Y - thickness, frame.Width + (2 * thickness), frame.Height + (2 * thickness));
            var drawn = Drawn(fading);
            var pieces = BorderPainter.Pieces(box.Width, box.Height, drawn);
            if (box.Width != _box.Width || box.Height != _box.Height || !drawn.Equals(_painted) || !_shown)
            {
                PaintAll(box, pieces, drawn);
                _painted = drawn;
                _paintedAlpha = _alpha;
            }
            else
            {
                if (_alpha != _paintedAlpha)
                {
                    for (var i = 0; i < pieces.Length; i++)
                        Fade(i, pieces[i]);

                    _paintedAlpha = _alpha;
                }

                if (box != _box)
                    MoveAll(box, pieces);
            }

            _box = box;
            _shown = true;
            _on = standing ? overlay : target;
            Stack(_on);
        }

        public void Restack()
        {
            if (_shown && !_on.IsNull)
                Stack(_on);
        }

        public void Dispose()
        {
            for (var i = 0; i < _pieces.Length; i++)
            {
                if (!_pieces[i].IsNull)
                    _ = PInvoke.DestroyWindow(_pieces[i]);

                _pieces[i] = HWND.Null;
            }
        }

        private void Hide()
        {
            if (!_shown)
                return;

            foreach (var piece in _pieces)
            {
                if (!piece.IsNull)
                    _ = PInvoke.ShowWindow(piece, SHOW_WINDOW_CMD.SW_HIDE);
            }

            _shown = false;
        }

        private unsafe void PaintAll(Rectangle box, Rectangle[] pieces, BorderRing ring)
        {
            var memories = new HDC[pieces.Length];
            var bitmaps = new HBITMAP[pieces.Length];
            var olds = new HGDIOBJ[pieces.Length];
            var screen = PInvoke.GetDC(HWND.Null);
            try
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    var piece = pieces[i];
                    _drawn[i] = false;
                    if (piece.Width <= 0 || piece.Height <= 0)
                    {
                        if (!_pieces[i].IsNull)
                            _ = PInvoke.ShowWindow(_pieces[i], SHOW_WINDOW_CMD.SW_HIDE);

                        continue;
                    }

                    if (_pieces[i].IsNull)
                        _pieces[i] = Create();

                    if (_pieces[i].IsNull)
                        continue;

                    memories[i] = PInvoke.CreateCompatibleDC(screen);
                    var info = new BITMAPINFO();
                    info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
                    info.bmiHeader.biWidth = piece.Width;
                    info.bmiHeader.biHeight = -piece.Height;
                    info.bmiHeader.biPlanes = 1;
                    info.bmiHeader.biBitCount = 32;
                    void* bits;
                    bitmaps[i] = PInvoke.CreateDIBSection(memories[i], &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
                    if (bitmaps[i].IsNull || bits is null)
                        continue;

                    olds[i] = PInvoke.SelectObject(memories[i], bitmaps[i]);
                    BorderPainter.Paint(new Span<uint>(bits, piece.Width * piece.Height), ring, box.Width, box.Height, piece);
                }

                var origin = new System.Drawing.Point(0, 0);
                var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = _alpha, AlphaFormat = 1 };
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (olds[i].IsNull)
                        continue;

                    var size = new SIZE(pieces[i].Width, pieces[i].Height);
                    var position = new System.Drawing.Point(box.X + pieces[i].X, box.Y + pieces[i].Y);
                    _drawn[i] = PInvoke.UpdateLayeredWindow(_pieces[i], screen, &position, &size, memories[i], &origin, new COLORREF(0), &blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA);
                }
            }
            finally
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (!olds[i].IsNull)
                        _ = PInvoke.SelectObject(memories[i], olds[i]);
                    if (!bitmaps[i].IsNull)
                        _ = PInvoke.DeleteObject(bitmaps[i]);
                    if (!memories[i].IsNull)
                        _ = PInvoke.DeleteDC(memories[i]);
                }

                _ = PInvoke.ReleaseDC(HWND.Null, screen);
            }
        }

        private unsafe void Fade(int index, Rectangle piece)
        {
            if (_pieces[index].IsNull || piece.Width <= 0 || piece.Height <= 0)
                return;

            var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = _alpha, AlphaFormat = 1 };
            _ = PInvoke.UpdateLayeredWindow(_pieces[index], HDC.Null, null, null, HDC.Null, null, new COLORREF(0), &blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA);
        }

        private void MoveAll(Rectangle box, Rectangle[] pieces)
        {
            var batch = PInvoke.BeginDeferWindowPos(pieces.Length);
            for (var i = 0; i < pieces.Length && !batch.IsNull; i++)
            {
                if (_pieces[i].IsNull || pieces[i].Width <= 0 || pieces[i].Height <= 0)
                    continue;

                batch = PInvoke.DeferWindowPos(
                    batch,
                    _pieces[i],
                    HWND.Null,
                    box.X + pieces[i].X,
                    box.Y + pieces[i].Y,
                    0,
                    0,
                    SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
            }

            if (!batch.IsNull && PInvoke.EndDeferWindowPos(batch))
                return;

            for (var i = 0; i < pieces.Length; i++)
                Move(i, box, pieces[i]);
        }

        private void Move(int index, Rectangle box, Rectangle piece)
        {
            if (_pieces[index].IsNull || piece.Width <= 0 || piece.Height <= 0)
                return;

            _ = PInvoke.SetWindowPos(
                _pieces[index],
                HWND.Null,
                box.X + piece.X,
                box.Y + piece.Y,
                0,
                0,
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        private void Stack(HWND target)
        {
            var chain = new List<HWND>();
            var above = DecorationStack.Above(target, chain);
            var topmost = IsTopmost(target);
            var settled = true;
            for (var i = 0; i < _pieces.Length && settled; i++)
            {
                var piece = _pieces[i];
                settled = piece.IsNull
                    || (chain.Contains(piece) && IsTopmost(piece) == topmost && (!_drawn[i] || PInvoke.IsWindowVisible(piece)));
            }

            if (settled)
                return;

            var after = !above.IsNull && IsTopmost(above) == topmost ? above : topmost ? Topmost : Top;
            for (var i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                if (piece.IsNull)
                    continue;

                var flags = SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

                if (!topmost && IsTopmost(piece))
                    _ = PInvoke.SetWindowPos(piece, NotTopmost, 0, 0, 0, 0, flags);

                if (_drawn[i])
                    flags |= SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;

                _ = PInvoke.SetWindowPos(piece, after, 0, 0, 0, 0, flags);
            }
        }

        private static bool IsTopmost(HWND hwnd) => (PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;

        private static bool Visible(HWND target) =>
            PInvoke.IsWindowVisible(target) && !PInvoke.IsIconic(target) && !WindowAdapter.IsCloaked(target);

        private static bool TryFrame(HWND target, out Rectangle frame) =>
            WindowRect.TryFrameBounds(target, out frame) && frame.Width > 0 && frame.Height > 0;
    }
}
