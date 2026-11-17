namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    private const int BorderColorAttribute = 34;
    private const int CornerAttribute = 33;
    private const int TransitionsOffAttribute = 3;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<WindowId, uint> _dwmPainted = new();

    private const uint DefaultBorderColor = 0xFFFFFFFFu;
    private const uint NoneBorderColor = 0xFFFFFFFEu;
    private const uint DefaultCorners = 0;
    private const uint SquareCorners = 1;
    private const uint RoundCorners = 2;
    private const uint SmallCorners = 3;

    private void Place(WindowId window, Rectangle target)
    {
        var trails = TryOuter(window, out var before) && _quick.Trails(window, before);

        if (!trails && TryVisible(window, out var shown) && shown == target)
            return;

        if (PlaceDirectly(window, target) && trails && TryOuter(window, out var landed))
            SendQuick(window, landed, keepSize: false);
    }

    private bool PlaceDirectly(WindowId window, Rectangle target)
    {
        var dpi = _desktop.Dpi(window);
        var insets = InsetsFor(window, dpi);
        var requested = insets.OuterFor(target);
        if (!Move(window, requested))
            return false;

        var arrived = _desktop.Dpi(window);
        if (arrived != dpi)
        {
            dpi = arrived;
            insets = InsetsFor(window, dpi);
            requested = insets.OuterFor(target);
            if (!Move(window, requested))
                return false;
        }

        if (!TryOuter(window, out var landedOuter) || !TryVisible(window, out var landedVisible))
        {
            Remember(window, dpi, insets);
            return true;
        }

        var correction = FrameInsets.Correct(target, requested, landedOuter, landedVisible);
        if (correction.Outer != landedOuter && !Move(window, correction.Outer))
            return false;

        if (correction.Clamped)
        {
            Console.Error.WriteLine(
                "Window 0x" + window.Value.ToString("x", System.Globalization.CultureInfo.InvariantCulture)
                + " kept a clamped size "
                + landedVisible.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "x"
                + landedVisible.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ".");
        }

        if (TryOuter(window, out var finalOuter) && TryVisible(window, out var finalVisible))
            Remember(window, dpi, FrameInsets.Between(finalOuter, finalVisible));
        else
            Remember(window, dpi, insets);

        return true;
    }

    private FrameInsets InsetsFor(WindowId window, int dpi)
    {
        lock (_frameGate)
        {
            if (_frames.TryGetValue(window, out var cached) && FrameInsets.AppliesAt(cached.Dpi, dpi))
                return cached.Insets;
        }

        var insets = TryMeasure(window, out var measured) ? measured : FrameInsets.None;
        Remember(window, dpi, insets);
        return insets;
    }

    private void Remember(WindowId window, int dpi, FrameInsets insets)
    {
        lock (_frameGate)
            _frames[window] = new CachedFrame(dpi, insets);
    }

    private void DropClosedFrames(IReadOnlyList<Client> clients)
    {
        lock (_frameGate)
        {
            if (_frames.Count == 0)
                return;

            var live = new HashSet<WindowId>(clients.Count);
            foreach (var client in clients)
                live.Add(client.Window);

            if (live.Count == 0)
            {
                _frames.Clear();
                return;
            }

            List<WindowId>? stale = null;
            foreach (var window in _frames.Keys)
            {
                if (!live.Contains(window))
                    (stale ??= []).Add(window);
            }

            if (stale is null)
                return;

            foreach (var window in stale)
                _frames.Remove(window);
        }
    }

    private bool TryMeasure(WindowId window, out FrameInsets insets)
    {
        insets = FrameInsets.None;
        if (!TryOuter(window, out var outer) || !TryVisible(window, out var visible))
            return false;

        insets = FrameInsets.Between(outer, visible);
        return true;
    }

    private bool TryOuter(WindowId window, out Rectangle outer) => _desktop.TryOuter(window, out outer);

    private bool TryVisible(WindowId window, out Rectangle visible) => _desktop.TryVisible(window, out visible);

    private bool Move(WindowId window, Rectangle bounds)
    {
        if (_desktop.Hung(window))
        {
            SendQuick(window, bounds, keepSize: false);
            return false;
        }

        _desktop.SetBounds(window, bounds, Delivery.Wait);
        _quick.Note(window, bounds, waiting: false);
        return true;
    }

    internal void QuickPlace(WindowId window, Rectangle target, bool borderless)
    {
        if (!TryOuter(window, out var current))
            return;

        _ = _quick.Request(window, OuterOf(window, target, borderless), current, (bounds, keepSize) => SendQuick(window, bounds, keepSize));
    }

    internal void PlaceAsync(WindowId window, Rectangle target, bool borderless) =>
        SendQuick(window, OuterOf(window, target, borderless), keepSize: false);

    private Rectangle OuterOf(WindowId window, Rectangle target, bool borderless) =>
        borderless ? target : InsetsFor(window, _desktop.Dpi(window)).OuterFor(target);

    private void SendQuick(WindowId window, Rectangle bounds, bool keepSize)
    {
        _desktop.SetBounds(window, bounds, keepSize ? Delivery.PostKeepingSize : Delivery.Post);
        _quick.Note(window, bounds, waiting: true);
    }

    private readonly record struct CachedFrame(int Dpi, FrameInsets Insets);

    internal void ResetDecorations() => ClearDecorations();

    private void ClearDecorations()
    {
        GroupBarHost.Stop();
        TrayHost.Stop();
        BorderHost.Stop();
        EndStandIns();
        _desktop.StopStandIns();
        PutBackFrames();
    }

    private void PutBackFrames()
    {
        WindowId[] painted;
        lock (_paintGate)
        {
            painted = [.. _painted.Keys];
            _painted.Clear();
        }

        foreach (var window in painted)
            ResetDecoration(window);

        _borderless.ReleaseAll();
        _effects.ResetAll();
        _opacity.ResetAll();
    }

    private bool Paint(WindowId window, Decoration decoration)
    {
        lock (_motions.For(window))
        {
            var held = _motions.Paint(window, decoration.Alpha, decoration.Border);
            if (!held.Border)
                BorderHost.Tint(window, null);
            var corners = decoration.Corners switch
            {
                CornerPreference.Round => RoundCorners,
                CornerPreference.RoundSmall => SmallCorners,
                _ => SquareCorners,
            };

            if (!_dwmPainted.TryGetValue(window, out var given) || given != corners)
            {
                _desktop.SetAttribute(window, BorderColorAttribute, NoneBorderColor);
                _desktop.SetAttribute(window, CornerAttribute, corners);
                _desktop.SetAttribute(window, TransitionsOffAttribute, 1);
                _dwmPainted[window] = corners;
            }

            _effects.Apply(window, decoration);
            if (!held.Alpha)
                _opacity.Apply(window, decoration.Alpha);

            return held == default;
        }
    }

    private void ForgetDwmPainted(IEnumerable<WindowId> clients)
    {
        if (_dwmPainted.IsEmpty)
            return;

        var live = new HashSet<WindowId>(clients);
        foreach (var window in _dwmPainted.Keys)
        {
            if (!live.Contains(window))
                _dwmPainted.TryRemove(window, out _);
        }
    }

    private void ResetDecoration(WindowId window)
    {
        _dwmPainted.TryRemove(window, out _);
        _desktop.SetAttribute(window, BorderColorAttribute, DefaultBorderColor);
        _desktop.SetAttribute(window, CornerAttribute, DefaultCorners);
        _desktop.SetAttribute(window, TransitionsOffAttribute, 0);
        _effects.Reset(window);
        _opacity.Reset(window);
    }
}
