namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome FullscreenDispatcher(FullscreenMode mode, string arguments)
    {
        if (FullscreenCommands.Parse(arguments) is not FullscreenParse parsed)
            return Rejected("error: " + (mode == FullscreenMode.Maximized ? "maximize" : "fullscreen") + " takes on, off or default");

        if (ActiveWindow() is not WindowId window || WorkspaceContaining(window) is not LiveWorkspace desk || desk.ClientOf(window) is not Client client)
            return Rejected("error: window not found");

        if (!FullscreenCommands.Changed(client.Fullscreen, mode, parsed.Action, out var next))
            return View([]);

        return CommitFullscreen(window, next, parsed.LayoutAware);
    }

    private Outcome CommitFullscreen(WindowId window, FullscreenMode mode, bool layoutAware = true)
    {
        var desk = WorkspaceContaining(window);
        if (desk is null || desk.ClientOf(window) is not Client client)
            return Rejected("error: window not found");

        var covered = desk.Covered();
        if (SetFullscreen(desk, window, mode, layoutAware) is not { } set)
            return View([]);

        var placements = Restamp(desk, set);
        return PresentIfShown(desk, covered, placements);
    }

    private IReadOnlyList<Placement>? SetFullscreen(LiveWorkspace desk, WindowId window, FullscreenMode mode, bool layoutAware = true)
    {
        if (desk.ClientOf(window) is null || _pins.IsPinned(window))
            return null;

        return desk.Layout.SetFullscreen(window, mode, layoutAware);
    }

    private static IReadOnlyList<Placement> EndFullscreen(LiveWorkspace desk, WindowId window) =>
        desk.Layout.SetFullscreen(window, FullscreenMode.None);

    private IReadOnlyList<Placement> Restamp(LiveWorkspace desk, IReadOnlyList<Placement> placements)
    {
        var options = desk.Options;
        Stamp(desk);
        if (desk.Options == options)
            return placements;

        var applied = desk.Layout.Apply(desk.Options);
        var merged = new List<Placement>(applied);
        foreach (var placement in placements)
        {
            if (!merged.Exists(other => other.Client == placement.Client))
                merged.Add(placement);
        }

        return merged;
    }

    private static IReadOnlyList<Placement> OpenUnderFullscreen(LiveWorkspace desk, WindowId window, IReadOnlyList<Placement> placements)
    {
        if (desk.ClientOf(window) is not { Floating: false } || CoveringOther(desk, window) is not Client covering)
            return placements;

        return EndFullscreen(desk, covering.Window);
    }

    private WindowId FocusUnderFullscreen(LiveWorkspace desk, WindowId window, out IReadOnlyList<Placement>? placements)
    {
        placements = null;

        if (desk.ClientOf(window) is { Floating: true } && CoveringOther(desk, window) is { LayoutFullscreen: false })
            _cover.SetFlag(window, true);

        if (desk.ClientOf(window) is not { Floating: false } || CoveringOther(desk, window) is not { LayoutFullscreen: false } covering)
            return window;

        placements = EndFullscreen(desk, covering.Window);
        return window;
    }

    private static Client? CoveringOther(LiveWorkspace desk, WindowId window)
    {
        foreach (var client in desk.Layout.Clients)
        {
            if (client.Window != window && desk.IsCovering(client))
                return client;
        }

        return null;
    }

    private Rectangle CoveringBounds(LiveWorkspace desk)
    {
        var work = WorkArea(desk, _area);
        return EnabledMonitor(desk.Monitor) is { } screen
            ? screen.Bounds
            : FullscreenGeometry.Cover(FullscreenMode.Fullscreen, work, default);
    }

    private static IReadOnlyList<Placement> Cover(IReadOnlyList<Placement> placements, State snapshot)
    {
        if (placements.Count == 0)
            return placements;

        Dictionary<ClientId, Rectangle>? covering = null;
        foreach (var client in snapshot.Clients)
        {
            if (client.Fullscreen == FullscreenMode.None)
                continue;

            covering ??= new Dictionary<ClientId, Rectangle>();
            covering[client.Id] = client.Bounds;
        }

        if (covering is null)
            return placements;

        var rewritten = new Placement[placements.Count];
        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            rewritten[i] = covering.TryGetValue(placement.Client, out var bounds)
                ? new Placement(placement.Client, bounds)
                : placement;
        }

        return rewritten;
    }

    private static Client? CoveringClient(LiveWorkspace desk, IReadOnlyCollection<WindowId> showing)
    {
        foreach (var client in desk.Layout.Clients)
        {
            if (desk.IsCovering(client) && !client.Minimized && showing.Contains(client.Window))
                return client;
        }

        return null;
    }

    private Rectangle PlacedBounds(Client client, LiveWorkspace desk, Rectangle slot)
    {
        if (client.Fullscreen == FullscreenMode.None || client.LayoutFullscreen)
            return slot;

        var work = WorkArea(desk, _area);
        if (client.Fullscreen == FullscreenMode.Maximized)
            return Maximized(client, desk, work);

        var monitor = CoveringBounds(desk);
        if (monitor.Width <= 0 || monitor.Height <= 0)
            monitor = work;

        return FullscreenGeometry.Cover(client.Fullscreen, work, monitor);
    }

    private static Rectangle Maximized(Client client, LiveWorkspace desk, Rectangle work)
    {
        var options = desk.Options;
        var area = client.Floating ? work : options.GapsOut.Apply(work);
        var border = options.BorderSize;
        return new Rectangle(
            area.X + border,
            area.Y + border,
            Math.Max(1, area.Width - (2 * border)),
            Math.Max(1, area.Height - (2 * border)));
    }
}
