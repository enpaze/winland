namespace Winland.Domain;

internal sealed partial class ScrollingLayout
{
    private readonly Dictionary<ClientId, double> _restoreWidths = [];
    private bool _syncing;

    public IReadOnlyList<Placement> SetFullscreen(WindowId window, FullscreenMode mode) =>
        SetFullscreen(window, mode, layoutAware: true);

    public IReadOnlyList<Placement> SetFullscreen(WindowId window, FullscreenMode mode, bool layoutAware)
    {
        var id = new ClientId(window.Value);
        var index = IndexOf(id);
        if (index < 0)
            return [];

        var client = _clients[index];
        if (client.Floating || !layoutAware || CellFor(window) is not { Column: Column column } cell)
            return DefaultFullscreen(window, client, mode);

        var held = client.LayoutFullscreen ? client.Fullscreen : FullscreenMode.None;
        if (held != mode && mode != FullscreenMode.None)
        {
            _restoreWidths[id] = held != FullscreenMode.None && _restoreWidths.TryGetValue(id, out var before) ? before : column.Width;
            if (column.Cells.Count > 1)
            {
                Expel(cell, _columns.IndexOf(column));
                column = cell.Column!;
            }

            column.Width = mode == FullscreenMode.Fullscreen ? FullscreenWidth() : 1;
        }
        else if (held != mode && held == FullscreenMode.Fullscreen)
        {
            AdjustOffset(Viewport().X - Usable().X);
        }

        _clients[index] = client with
        {
            Fullscreen = mode,
            LayoutFullscreen = mode != FullscreenMode.None,
        };
        if (mode == FullscreenMode.None && _restoreWidths.Remove(id, out var width))
            column.Width = width;

        if (held != mode)
            FitColumn(column);

        Refit();
        return Visible();
    }

    public bool FullscreenCovers(WindowId window)
    {
        if (CellFor(window) is not { Column: Column column } cell || HeldFullscreen(cell.Id) is null)
            return true;

        return column.Cells.Count == 1 && TryStrip(column, out var start, out var size, out _) && Covers(start, size);
    }

    private IReadOnlyList<Placement> DefaultFullscreen(WindowId window, Client client, FullscreenMode mode)
    {
        var id = client.Id;
        if (client.LayoutFullscreen && _restoreWidths.Remove(id, out var width) && CellFor(window)?.Column is Column column)
            column.Width = width;

        if (!FullscreenClients.Assign(_clients, window, mode))
            return [];

        if (client.LayoutFullscreen)
            Refit();

        var placements = new List<Placement>(Visible());
        if (mode != FullscreenMode.None && !placements.Exists(placement => placement.Client == id) && TryBounds(window, out var bounds))
            placements.Add(new Placement(id, bounds));

        return placements;
    }

    private FullscreenMode? HeldFullscreen(ClientId id)
    {
        foreach (var client in _clients)
        {
            if (client.Id == id)
                return client is { LayoutFullscreen: true, Fullscreen: not FullscreenMode.None } ? client.Fullscreen : null;
        }

        return null;
    }

    private bool Covers(double start, double size) =>
        start <= _offset + 1 && start + size >= _offset + PrimaryLength() - 1;

    private double FullscreenWidth()
    {
        var usable = Usable();
        var monitor = Viewport();
        return usable.Width <= 0 ? 1 : Math.Max(1, monitor.Width / (double)usable.Width);
    }

    private void PlaceFullscreen(Cell cell, FullscreenMode mode, int index, double start, double size, double primaryPos)
    {
        var usable = Usable();
        var cover = mode == FullscreenMode.Fullscreen ? Viewport() : usable;
        double x = cover.X;
        double y = cover.Y;
        double width = cover.Width;
        double height = cover.Height;
        if (!Covers(start, size))
        {
            x = usable.X + primaryPos;
            width = size;
        }

        if (mode == FullscreenMode.Maximized)
        {
            var gaps = GapsFor(index, 0, 1);
            var border = _options.BorderFor(cell.Id);
            x += gaps.Left + border;
            y += gaps.Top + border;
            width = Math.Max(1, width - gaps.Left - gaps.Right - (border * 2.0));
            height = Math.Max(1, height - gaps.Top - gaps.Bottom - (border * 2.0));
        }

        var box = Pixels(x, y, width, height);
        cell.Logical = box;
        cell.Box = box;
    }

    private void SyncFullscreen()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            for (var i = 0; i < _clients.Count; i++)
            {
                var client = _clients[i];
                if (!client.LayoutFullscreen || _suspended.ContainsKey(client.Id))
                    continue;

                if (client.Floating || CellFor(client.Window) is not { Column: Column column })
                {
                    _restoreWidths.Remove(client.Id);
                    _clients[i] = client with { LayoutFullscreen = false };
                }
                else if (client.Fullscreen != FullscreenMode.None && column.Cells.Count != 1)
                {
                    _ = SetFullscreen(client.Window, FullscreenMode.None, layoutAware: true);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }
}
