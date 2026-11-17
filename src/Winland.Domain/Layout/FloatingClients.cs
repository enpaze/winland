namespace Winland.Domain;

internal sealed class FloatingClients
{
    private const int DefaultWidth = 640;
    private const int DefaultHeight = 400;

    private readonly Dictionary<ClientId, Rectangle> _boxes = [];
    private readonly Dictionary<ClientId, (int Width, int Height)> _lastSize = [];

    public bool Contains(ClientId id) => _boxes.ContainsKey(id);

    public bool TryGet(ClientId id, out Rectangle box) => _boxes.TryGetValue(id, out box);

    public void Set(ClientId id, Rectangle box) => _boxes[id] = box;

    public bool Remove(ClientId id) => _boxes.Remove(id);

    public void Remember(ClientId id, Rectangle bounds)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
            _lastSize[id] = (bounds.Width, bounds.Height);
    }

    public bool Forget(ClientId id)
    {
        _lastSize.Remove(id);
        return _boxes.Remove(id);
    }

    public bool Sink(ClientId id)
    {
        if (!_boxes.Remove(id, out var box))
            return false;

        Remember(id, box);
        return true;
    }

    public Rectangle Around(ClientId id, Rectangle tile, OnScreen onScreen)
    {
        var width = DefaultWidth;
        var height = DefaultHeight;
        if (_lastSize.TryGetValue(id, out var last) && last.Width >= 5 && last.Height >= 5)
        {
            width = last.Width;
            height = last.Height;
        }

        if (Math.Abs(width - tile.Width) < 5 && Math.Abs(height - tile.Height) < 5)
        {
            width += 10;
            height += 10;
        }

        var floated = new Rectangle(
            tile.X + (tile.Width / 2) - (width / 2),
            tile.Y + (tile.Height / 2) - (height / 2),
            width,
            height);
        return onScreen.Keep(floated);
    }

    public void Hand(ClientId from, ClientId to)
    {
        SlotHandover.Move(_boxes, from, to);
        SlotHandover.Move(_lastSize, from, to);
    }

    public void Nudge(int dx, int dy)
    {
        if ((dx == 0 && dy == 0) || _boxes.Count == 0)
            return;

        foreach (var id in _boxes.Keys.ToArray())
        {
            var box = _boxes[id];
            _boxes[id] = box with { X = box.X + dx, Y = box.Y + dy };
        }
    }

    public IReadOnlyList<Placement> Clamp(ClientId id, SizeLimits limits)
    {
        if (!_boxes.TryGetValue(id, out var box))
            return [];

        var clamped = limits.Clamp(box);
        if (clamped == box)
            return [];

        _boxes[id] = clamped;
        return [new Placement(id, clamped)];
    }

    public IReadOnlyList<Placement> MoveToEdge(ClientId id, Side side, Rectangle area, int border)
    {
        if (!_boxes.TryGetValue(id, out var box))
            return [];

        var frame = new Rectangle(area.X + border, area.Y + border, area.Width - (2 * border), area.Height - (2 * border));

        var moved = side switch
        {
            Side.Left => box with { X = frame.X },
            Side.Right => box with { X = frame.X + frame.Width - box.Width },
            Side.Up => box with { Y = frame.Y },
            Side.Down => box with { Y = frame.Y + frame.Height - box.Height },
            _ => box,
        };
        if (moved == box)
            return [];

        _boxes[id] = moved;
        return [new Placement(id, moved)];
    }

    public IReadOnlyList<Placement> Resize(ClientId id, int x, int y, bool exact, OnScreen onScreen, SizeLimits limits)
    {
        if (!_boxes.TryGetValue(id, out var box))
            return [];

        var deltaX = exact ? x - box.Width : x;
        var deltaY = exact ? y - box.Height : y;
        var resized = new Rectangle(box.X - (deltaX / 2), box.Y - (deltaY / 2), box.Width + deltaX, box.Height + deltaY);
        if (resized.Width < 1 || resized.Height < 1)
            return [];

        resized = onScreen.Keep(limits.Clamp(resized));
        _boxes[id] = resized;
        return [new Placement(id, resized)];
    }

    public bool Place(ClientId id, Rectangle bounds, OnScreen onScreen, SizeLimits limits)
    {
        if (!_boxes.ContainsKey(id) || bounds.Width < 1 || bounds.Height < 1)
            return false;

        _boxes[id] = onScreen.Keep(limits.Clamp(bounds));
        return true;
    }

    public void AddShown(List<Placement> placements, IReadOnlyList<Client> clients)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(clients);
        foreach (var client in clients)
        {
            if (!client.Floating || client.Minimized)
                continue;

            if (_boxes.TryGetValue(client.Id, out var box))
                placements.Add(new Placement(client.Id, box));
        }
    }
}
