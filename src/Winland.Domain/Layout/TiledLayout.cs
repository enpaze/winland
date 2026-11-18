namespace Winland.Domain;

internal abstract class TiledLayout
{
    private protected readonly List<Client> _clients = [];
    private protected readonly FloatingClients _floating = new();
    private protected readonly Dictionary<ClientId, Rectangle> _natural = [];
    private protected readonly Dictionary<ClientId, SizeLimits> _limits = [];
    private protected readonly MouseDrag _drag = new();
    private protected LayoutOptions _options = LayoutOptions.Unconfigured;
    private protected Rectangle _area;
    private protected Rectangle _monitor;

    public IReadOnlyList<Client> Clients => _clients;

    public MouseDrag MouseDrag => _drag;

    public IReadOnlyList<Placement> Apply(LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        Refit();
        return Placed();
    }

    public void Fit(Rectangle area, Rectangle monitor)
    {
        _area = area;
        if (monitor.Width > 0 && monitor.Height > 0)
            _monitor = monitor;
        Refit();
    }

    public IReadOnlyList<Placement> Minimize(WindowId window)
    {
        var id = new ClientId(window.Value);
        if (_floating.Contains(id))
        {
            SetMinimized(id, true);
            return [];
        }

        return MinimizeTiled(id);
    }

    public IReadOnlyList<Placement> Restore(WindowId window)
    {
        var id = new ClientId(window.Value);
        if (_floating.TryGet(id, out var bounds))
        {
            var index = IndexOf(id);
            if (index < 0 || !_clients[index].Minimized)
                return [];

            SetMinimized(id, false);
            return [new Placement(id, bounds)];
        }

        return RestoreTiled(window);
    }

    public IReadOnlyList<Placement> PlaceFloating(WindowId window, Rectangle bounds)
    {
        var id = new ClientId(window.Value);
        return _floating.Place(id, bounds, _options.FloatingOnScreen(_area), FloatingLimits(id)) ? WithFloating(id) : [];
    }

    public void Retitle(WindowId window, string title)
    {
        for (var i = 0; i < _clients.Count; i++)
        {
            if (_clients[i].Window == window)
                _clients[i] = _clients[i] with { Title = title };
        }
    }

    public void Reclass(WindowId window, string windowClass)
    {
        for (var i = 0; i < _clients.Count; i++)
        {
            if (_clients[i].Window == window)
                _clients[i] = _clients[i] with { Class = windowClass };
        }
    }

    public void Readdress(WorkspaceId workspace)
    {
        for (var i = 0; i < _clients.Count; i++)
            _clients[i] = _clients[i] with { Workspace = workspace };
    }

    public void NudgeFloating(int dx, int dy) => _floating.Nudge(dx, dy);

    public SizeLimits FloatingLimits(ClientId id) => _limits.TryGetValue(id, out var limits) ? new SizeLimits(limits.Min, limits.Max) : default;

    public abstract IReadOnlyList<Placement> Placed();

    public abstract IReadOnlyList<Placement> Remove(WindowId window);

    public IReadOnlyList<Placement> Visible()
    {
        var placements = new List<Placement>(Placed());
        _floating.AddShown(placements, _clients);
        return placements;
    }

    public bool TryTake(WindowId window, out Taken taken)
    {
        taken = default;
        var id = new ClientId(window.Value);
        var index = IndexOf(id);
        if (index < 0)
            return false;

        taken = Describe(index);
        Remove(window);
        return true;
    }

    public bool Substitute(WindowId current, WindowId next, Taken incoming, out Taken outgoing)
    {
        outgoing = default;
        var from = new ClientId(current.Value);
        var to = new ClientId(next.Value);
        var index = IndexOf(from);
        if (index < 0 || from == to || IndexOf(to) >= 0)
            return false;

        outgoing = Describe(index);
        _clients[index] = SlotHandover.Rename(_clients[index], next, incoming);
        Rename(from, to);
        _floating.Hand(from, to);
        _natural.Remove(from);
        _natural[to] = incoming.Bounds;
        _limits.Remove(from);
        _limits[to] = new SizeLimits(incoming.MinSize, incoming.MaxSize) { Tiled = incoming.TiledLimits };
        return true;
    }

    private protected abstract void Rename(ClientId from, ClientId to);

    private protected abstract void Refit();

    private protected abstract IReadOnlyList<Placement> MinimizeTiled(ClientId id);

    private protected abstract IReadOnlyList<Placement> RestoreTiled(WindowId window);

    private protected IReadOnlyList<Placement> WithFloating(ClientId id)
    {
        var placements = new List<Placement>(Placed());
        if (_floating.TryGet(id, out var box))
            placements.Add(new Placement(id, box));

        return placements;
    }

    private Taken Describe(int index)
    {
        var client = _clients[index];
        var bounds = _floating.TryGet(client.Id, out var floating)
            ? floating
            : _natural.TryGetValue(client.Id, out var natural) ? natural : default;
        _limits.TryGetValue(client.Id, out var limits);
        return SlotHandover.Describe(client, bounds, limits, TakenWidth(client.Id));
    }

    private protected virtual double? TakenWidth(ClientId id) => null;

    private protected int IndexOf(ClientId id) => _clients.FindIndex(client => client.Id == id);

    private protected bool Minimized(ClientId id)
    {
        var index = IndexOf(id);
        return index >= 0 && _clients[index].Minimized;
    }

    private protected void SetMinimized(ClientId id, bool minimized)
    {
        var index = IndexOf(id);
        if (index >= 0)
            _clients[index] = _clients[index] with { Minimized = minimized };
    }

    private protected void SetFloating(ClientId id, bool floating)
    {
        var index = IndexOf(id);
        if (index >= 0)
            _clients[index] = _clients[index] with { Floating = floating };
    }
}
