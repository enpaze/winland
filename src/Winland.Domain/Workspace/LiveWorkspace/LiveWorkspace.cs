namespace Winland.Domain;

internal sealed partial class LiveWorkspace(WorkspaceId id, MonitorId monitor, Layout layout)
{
    public WorkspaceId Id { get; private set; } = id;

    public void Retitle(WindowId window, string title)
    {
        Layout.Retitle(window, title);
        if (_waiting.TryGetValue(window, out var waiting))
            _waiting[window] = Wait(window, waiting.Taken with { Title = title });
    }

    public void Reclass(WindowId window, string windowClass)
    {
        Layout.Reclass(window, windowClass);
        if (_waiting.TryGetValue(window, out var waiting))
            _waiting[window] = Wait(window, waiting.Taken with { Class = windowClass });
    }

    public void Readdress(WorkspaceId id)
    {
        Id = id;
        Layout.Readdress(id);
        foreach (var (window, waiting) in _waiting.ToArray())
            _waiting[window] = waiting with { Client = waiting.Client with { Workspace = id } };
    }

    public MonitorId Monitor { get; set; } = monitor;

    public Layout Layout { get; private set; } = layout;

    public void Replace(Layout layout) => Layout = layout;

    public WindowId? Focus { get; private set; }

    internal void RememberFocus(WindowId? window) => Focus = window;

    public bool Persistent { get; set; }

    public string? HomeIdentity { get; private set; }

    public long HomeNumber { get; private set; }

    public bool IsGuest => HomeIdentity is not null;

    internal void MarkGuest(string homeIdentity, long homeNumber)
    {
        HomeIdentity = homeIdentity;
        HomeNumber = homeNumber;
    }

    internal void ClearGuest()
    {
        HomeIdentity = null;
        HomeNumber = 0;
    }

    public LayoutOptions Options { get; set; } = LayoutOptions.Unconfigured;

    public Client? ClientOf(WindowId window)
    {
        foreach (var client in Layout.Clients)
        {
            if (client.Window == window)
                return client;
        }

        return WaitingOf(window);
    }

    public bool IsCovering(Client client) =>
        client.Fullscreen != FullscreenMode.None && (!client.LayoutFullscreen || Layout.FullscreenCovers(client.Window));

    public HashSet<WindowId> Showing()
    {
        var ids = new Dictionary<ClientId, WindowId>(Layout.Clients.Count);
        foreach (var client in Layout.Clients)
            ids[client.Id] = client.Window;

        var showing = new HashSet<WindowId>();
        foreach (var placement in Layout.Visible())
        {
            if (ids.TryGetValue(placement.Client, out var window))
                showing.Add(window);
        }

        return showing;
    }

    public WindowId[] VisibleWindows()
    {
        var showing = Showing();
        var visible = new List<WindowId>();
        foreach (var client in Layout.Clients)
        {
            if (!client.Minimized && showing.Contains(client.Window))
                visible.Add(client.Window);
        }

        return visible.ToArray();
    }

    public HashSet<WindowId> Covered()
    {
        var showing = Showing();
        var covered = new HashSet<WindowId>();
        foreach (var client in Layout.Clients)
        {
            if (client.Minimized || client.Floating || showing.Contains(client.Window))
                continue;

            covered.Add(client.Window);
        }

        foreach (var window in _waiting.Keys)
            covered.Add(window);

        return covered;
    }

    public void Collect(HashSet<WindowId> covered, List<WindowId> show, List<WindowId> hide)
    {
        var after = Covered();
        foreach (var window in after)
        {
            if (!covered.Contains(window))
                hide.Add(window);
        }

        foreach (var window in covered)
        {
            if (!after.Contains(window) && ClientOf(window) is { Minimized: false })
                show.Add(window);
        }
    }

    public WindowId? LastVisible()
    {
        for (var i = Layout.Clients.Count - 1; i >= 0; i--)
        {
            if (!Layout.Clients[i].Minimized)
                return Layout.Clients[i].Window;
        }

        return null;
    }

    public WindowId? Remembered()
    {
        if (Focus is WindowId focus && ClientOf(focus) is { Minimized: false })
            return focus;

        return LastVisible();
    }

    public bool Take(WindowId window, out Taken taken)
    {
        var client = ClientOf(window);
        if (!Layout.TryTake(window, out taken))
            return false;

        if (client is not null)
            taken = taken with { Fullscreen = client.Fullscreen, LayoutFullscreen = client.LayoutFullscreen };

        return true;
    }

    public void Receive(WindowId window, Taken taken, Rectangle area, Rectangle monitor = default, Point? pointer = null)
    {
        if (taken.ScrollingWidth is double width)
            Layout.NoteScrollingWidth(window, width);

        Layout.Add(
            window,
            area,
            pointer,
            taken.Floating,
            taken.Bounds,
            Id,
            taken.Class,
            taken.Title,
            monitor: monitor,
            processId: taken.ProcessId,
            initialClass: taken.InitialClass,
            initialTitle: taken.InitialTitle,
            minSize: taken.MinSize,
            maxSize: taken.MaxSize,
            tiledLimits: taken.TiledLimits);
        if (taken.Fullscreen != FullscreenMode.None)
            Layout.SetFullscreen(window, taken.Fullscreen, taken.LayoutFullscreen);
        if (taken.Minimized)
            Layout.Minimize(window);
    }
}
