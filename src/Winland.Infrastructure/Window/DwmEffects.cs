namespace Winland.Infrastructure;

internal interface IDwmSurface
{
    bool TryReadBackdrop(WindowId window, out uint backdrop);

    bool TrySetBackdrop(WindowId window, uint backdrop);

    bool HasRegion(WindowId window);

    bool TryFrame(WindowId window, out Rectangle outer, out Rectangle visible);

    bool TrySetRegion(WindowId window, IReadOnlyList<Point> outline);
}

internal sealed class DwmEffects
{
    internal const uint AutoBackdrop = 0;
    internal const uint NoBackdrop = 1;
    internal const uint BlurBackdrop = 3;

    private readonly IDwmSurface _surface;
    private readonly Func<WindowId, bool> _hung;
    private readonly Dictionary<WindowId, Backdrop> _backdrops = [];
    private readonly Dictionary<WindowId, Cut> _regions = [];
    private readonly Dictionary<WindowId, Rectangle> _clips = [];
    private readonly HashSet<WindowId> _foreignRegions = [];
    private readonly Lock _gate = new();
    private bool _closed;

    internal DwmEffects(IDwmSurface surface, Func<WindowId, bool>? hung = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _hung = hung ?? (static _ => false);
    }

    internal bool CutsCorners(WindowId window)
    {
        lock (_gate)
            return _regions.ContainsKey(window);
    }

    internal void Apply(WindowId window, Decoration decoration)
    {
        lock (_gate)
        {
            if (_closed)
                return;

            ApplyBackdrop(window, decoration.Blur ? BlurBackdrop : NoBackdrop);
            if (decoration.Region is CornerRegion region && !decoration.Borderless)
                ApplyRegion(window, region);
            else
                ClearRegion(window);
        }
    }

    internal void Refit(WindowId window)
    {
        lock (_gate)
        {
            if (_regions.TryGetValue(window, out var cut))
                Fit(window, cut.Region, cut);
        }
    }

    internal void Clip(WindowId window, Rectangle? local)
    {
        lock (_gate)
        {
            if (_closed || _foreignRegions.Contains(window))
                return;

            if (local is not Rectangle cut)
            {
                if (!_clips.Remove(window))
                    return;

                if (_regions.TryGetValue(window, out var corner))
                    Fit(window, corner.Region, null);
                else
                    _surface.TrySetRegion(window, []);
                return;
            }

            if (_clips.TryGetValue(window, out var have) && have == cut)
                return;

            _clips[window] = cut;
            PaintClip(window, cut);
        }
    }

    internal void Reset(WindowId window)
    {
        lock (_gate)
        {
            var clipped = _clips.Remove(window);
            if (_backdrops.Remove(window, out var backdrop))
                _surface.TrySetBackdrop(window, backdrop.Original);
            if (_regions.ContainsKey(window) && _hung(window))
                Console.Error.WriteLine($"Window 0x{window.Value:x} is not answering. Its corners are put back at the next start.");
            else if (_regions.Remove(window) || clipped)
                _surface.TrySetRegion(window, []);
            _foreignRegions.Remove(window);
        }
    }

    internal void ResetAll()
    {
        HashSet<WindowId> windows;
        lock (_gate)
        {
            _closed = true;
            windows = [.. _backdrops.Keys];
            windows.UnionWith(_regions.Keys);
            windows.UnionWith(_foreignRegions);
            windows.UnionWith(_clips.Keys);
        }

        foreach (var window in windows)
            Reset(window);
    }

    internal RecoverySnapshot Stamp(RecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            var clients = new RecoveredClient[snapshot.Clients.Count];
            var changed = false;
            for (var i = 0; i < snapshot.Clients.Count; i++)
            {
                var client = snapshot.Clients[i];
                var cut = _regions.ContainsKey(client.Window);
                clients[i] = client.Region == cut ? client : client with { Region = cut };
                changed |= client.Region != cut;
            }

            return changed ? snapshot with { Clients = clients } : snapshot;
        }
    }

    internal void DropLeftoverRegion(WindowId window)
    {
        lock (_gate)
        {
            _clips.Remove(window);
            _regions.Remove(window);
            _foreignRegions.Remove(window);
            _surface.TrySetRegion(window, []);
        }
    }

    private void ApplyBackdrop(WindowId window, uint wanted)
    {
        if (_backdrops.TryGetValue(window, out var backdrop))
        {
            if (backdrop.Current != wanted && _surface.TrySetBackdrop(window, wanted))
                _backdrops[window] = backdrop with { Current = wanted };
            return;
        }

        var original = _surface.TryReadBackdrop(window, out var read) ? read : AutoBackdrop;
        if (_surface.TrySetBackdrop(window, wanted))
            _backdrops[window] = new Backdrop(original, wanted);
    }

    private void ApplyRegion(WindowId window, CornerRegion region)
    {
        if (_foreignRegions.Contains(window))
            return;

        if (_regions.TryGetValue(window, out var cut))
        {
            Fit(window, region, cut);
            return;
        }

        if (_surface.HasRegion(window) && !_clips.ContainsKey(window))
        {
            _foreignRegions.Add(window);
            return;
        }

        Fit(window, region, null);
    }

    private void Fit(WindowId window, CornerRegion region, Cut? previous)
    {
        if (!_surface.TryFrame(window, out var outer, out var visible) || visible.Width <= 0 || visible.Height <= 0)
        {
            RepaintClip(window);
            return;
        }

        var cut = new Cut(region, outer.Width, outer.Height, visible.X - outer.X, visible.Y - outer.Y, visible.Width, visible.Height);
        if (cut != previous)
        {
            var outline = CornerShape.Outline(visible.Width, visible.Height, region);
            var shifted = new Point[outline.Count];
            for (var i = 0; i < outline.Count; i++)
                shifted[i] = new Point(outline[i].X + cut.Left, outline[i].Y + cut.Top);

            if (_surface.TrySetRegion(window, shifted))
                _regions[window] = cut;
        }

        RepaintClip(window);
    }

    private void ClearRegion(WindowId window)
    {
        if (_regions.Remove(window))
            _surface.TrySetRegion(window, []);

        RepaintClip(window);
    }

    private void RepaintClip(WindowId window)
    {
        if (_clips.TryGetValue(window, out var local))
            PaintClip(window, local);
    }

    private void PaintClip(WindowId window, Rectangle local)
    {
        if (local.Width <= 0 || local.Height <= 0)
        {
            _surface.TrySetRegion(window, [new Point(0, 0)]);
            return;
        }

        var right = local.X + local.Width;
        var bottom = local.Y + local.Height;
        _surface.TrySetRegion(window,
        [
            new Point(local.X, local.Y),
            new Point(right, local.Y),
            new Point(right, bottom),
            new Point(local.X, bottom),
        ]);
    }

    private readonly record struct Backdrop(uint Original, uint Current);

    private readonly record struct Cut(CornerRegion Region, int OuterWidth, int OuterHeight, int Left, int Top, int Width, int Height);
}
