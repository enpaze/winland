namespace Winland.Application;

public sealed partial class WindowManager
{
    private WarpTo? _crossMonitorWarp;

    private void RankFocus(Layout layout) => layout.RankFocus(_focus.Recency);

    private Outcome MoveFocusDirection(string arguments)
    {
        var active = ActiveWindow();
        var desk = active is WindowId owned && WorkspaceContaining(owned) is LiveWorkspace own ? own : FocusedWorkspace();
        var covered = desk.Covered();
        if (!Sides.TryParse(FirstToken(arguments), out var direction))
            return View([]);

        if (active is WindowId current && !HasFullscreen(desk))
        {
            var focus = desk.Layout.MoveFocus(current, arguments);
            if (focus is WindowId chosen)
            {
                if (HasNoFocus(chosen))
                    return View([]);

                Give(chosen, desk);
                return Present(desk, covered, [], focus: chosen);
            }
        }

        if (EnabledMonitor(desk.Monitor) is not LiveMonitor origin || TargetOf(origin) is not MonitorTarget from)
            return View([]);

        if (MonitorSelector.InDirection(Targets(), from, direction) is not MonitorTarget neighbor
            || MonitorOf(neighbor) is not LiveMonitor destination)
            return View([]);

        WindowId? adjacent = null;
        if (active is WindowId fromWindow && desk.Layout.TryBounds(fromWindow, out var box))
            adjacent = AdjacentOn(destination, desk, fromWindow, box, direction);

        return FocusOnto(destination, adjacent);
    }

    private static bool HasFullscreen(LiveWorkspace desk)
    {
        foreach (var client in desk.Layout.Clients)
        {
            if (desk.IsCovering(client))
                return true;
        }

        return false;
    }

    private Outcome MoveWindowDirection(string arguments)
    {
        if (TryMonitorArgument(arguments, out var monitorText, out var silent))
        {
            if (ResolveMonitor(monitorText) is not LiveMonitor chosen)
                return Rejected("error: monitor not found");

            return MoveActiveOnto(chosen, silent);
        }

        var active = ActiveWindow();
        var desk = active is WindowId owned && WorkspaceContaining(owned) is LiveWorkspace own ? own : FocusedWorkspace();
        var covered = desk.Covered();
        if (active is WindowId shown && Find(shown) is { Fullscreen: not FullscreenMode.None })
            return Rejected("error: can't move fullscreen window");

        if (active is not WindowId window || !Sides.TryParse(FirstToken(arguments), out var direction))
        {
            IReadOnlyList<Placement> stayed = active is WindowId id ? desk.Layout.MoveWindow(id, arguments) : [];
            return Present(desk, covered, stayed);
        }

        if (EnabledMonitor(desk.Monitor) is LiveMonitor origin
            && desk.Layout.TryBounds(window, out var box))
        {
            var focal = Sides.Focal(Ideal(desk, window, box), direction);
            var hit = MonitorSelector.At(Targets(), focal);
            if (hit is MonitorTarget other && other.Id != origin.Id && MonitorOf(other) is LiveMonitor across)
                return MoveActiveOnto(across, silent: false, focal);
        }

        var placements = desk.Layout.MoveWindow(window, arguments);
        if (placements.Count == 0
            && desk.Layout.FollowsMonitorWhenUnmoved
            && EnabledMonitor(desk.Monitor) is LiveMonitor home
            && TargetOf(home) is MonitorTarget from
            && MonitorSelector.InDirection(Targets(), from, direction) is MonitorTarget neighbor
            && MonitorOf(neighbor) is LiveMonitor next)
            return MoveActiveOnto(next, silent: false);

        return Present(desk, covered, placements);
    }

    private Outcome FocusMonitorDispatch(string arguments)
    {
        if (ResolveMonitor(arguments) is not LiveMonitor screen)
            return Rejected("error: monitor not found");

        return FocusOnto(screen, preferred: null);
    }

    private Outcome MoveCurrentWorkspace(string arguments)
    {
        if (ResolveMonitor(arguments) is not LiveMonitor destination)
            return Rejected("error: monitor not found");

        if (_workspaces.Find(ActiveId()) is not LiveWorkspace desk)
            return Rejected("error: invalid workspace");

        return MoveWorkspaceTo(desk, destination);
    }

    private Outcome MoveWorkspaceTo(LiveWorkspace desk, LiveMonitor destination)
    {
        if (desk.Monitor == destination.Id)
            return View([]);

        var before = ShowingNow();
        var origin = MonitorById(desk.Monitor);
        var wasActive = origin is not null && _workspaces.Shown.Shown(origin.Id) == desk.Id;
        var follow = wasActive && origin is { Focused: true };
        var dx = destination.Arranged.X - (origin?.Arranged.X ?? destination.Arranged.X);
        var dy = destination.Arranged.Y - (origin?.Arranged.Y ?? destination.Arranged.Y);
        if (wasActive && origin is not null)
            Plug(origin, desk);

        desk.Layout.NudgeFloating(dx, dy);
        Readdressed(_workspaces.MoveTo(desk, destination.Id));
        Refit(desk);

        WindowId? focus = null;
        if (follow)
        {
            var displaced = _workspaces.Shown.Shown(destination.Id);
            _workspaces.Shown.Show(destination.Id, desk.Id);
            if (displaced is WorkspaceId gone && gone != desk.Id)
                DropIfEmpty(gone);

            var next = _focus.Remembered(desk);
            Focus(destination.Id);
            Give(next, desk);
            focus = next;
        }

        var (show, hide) = VisibilityDelta(before);
        return View(ShownPlacements(), show, hide, focus: focus);
    }

    private Outcome MoveActiveOnto(LiveMonitor screen, bool silent, Point? focal = null)
    {
        if (_workspaces.Shown.Shown(screen.Id) is not WorkspaceId target)
            return View([]);

        return MoveTo(target, silent, consumed: false, focal: focal);
    }

    private Outcome FocusOnto(LiveMonitor screen, WindowId? preferred)
    {
        if (screen.Focused && preferred is null)
            return View([]);

        var desk = _workspaces.Shown.SpecialOn(screen.Id) is WorkspaceId special && _workspaces.Find(special) is LiveWorkspace open
            ? open
            : _workspaces.Shown.Shown(screen.Id) is WorkspaceId workspace ? _workspaces.Find(workspace) : null;
        var next = preferred;
        if (next is WindowId wanted && (desk is null || desk.ClientOf(wanted) is null || HasNoFocus(wanted)))
            next = null;
        if (next is null && desk is not null)
            next = _focus.Remembered(desk);

        Focus(screen.Id);
        Give(next, desk);

        return View([], focus: next);
    }

    private void Plug(LiveMonitor origin, LiveWorkspace leaving)
    {
        LiveWorkspace? replacement = null;
        foreach (var other in _workspaces)
        {
            if (other.Id == leaving.Id || other.Id.IsSpecial || other.Monitor != origin.Id)
                continue;

            replacement = other;
            break;
        }

        replacement ??= WorkspaceFor(_workspaces.NextFree(origin.Id), origin.Id);
        _workspaces.Shown.Show(origin.Id, replacement.Id);

        foreach (var window in PinnedOn(leaving))
            Transfer(leaving, replacement, window);
    }

    private WindowId? AdjacentOn(LiveMonitor screen, LiveWorkspace from, WindowId window, Rectangle box, Side direction)
    {
        if (_workspaces.Shown.Shown(screen.Id) is not WorkspaceId workspace || _workspaces.Find(workspace) is not LiveWorkspace desk)
            return null;

        var origin = Ideal(from, window, box);
        var floating = from.ClientOf(window) is { Floating: true } held && !from.IsCovering(held);
        var clients = desk.Layout.Clients.Select(client => desk.IsCovering(client) ? client with { Floating = false } : client).ToList();
        bool BoxOf(WindowId other, out Rectangle ideal)
        {
            ideal = default;
            if (HasNoFocus(other) || !desk.Layout.TryBounds(other, out var raw))
                return false;

            ideal = Ideal(desk, other, raw);
            return true;
        }

        return DirectionalNeighbor.Find(clients, origin, direction, window, floating, BoxOf, _focus.Recency)
            ?? DirectionalNeighbor.Find(clients, origin, direction, window, !floating, BoxOf, _focus.Recency);
    }

    private Rectangle Ideal(LiveWorkspace desk, WindowId window, Rectangle box)
    {
        if (desk.ClientOf(window) is not Client client || EnabledMonitor(desk.Monitor) is not { } screen)
            return box;
        if (desk.IsCovering(client))
            return screen.Bounds;
        if (client.Floating)
            return box;

        var border = desk.Options.BorderSize;
        var work = desk.Options.GapsOut.Apply(screen.Arranged.WorkArea);
        var monitor = screen.Bounds;
        var left = box.X - border;
        var top = box.Y - border;
        var right = box.X + box.Width + border;
        var bottom = box.Y + box.Height + border;
        if (Math.Abs(left - work.X) <= 1)
            left = monitor.X;
        if (Math.Abs(top - work.Y) <= 1)
            top = monitor.Y;
        if (Math.Abs(right - (work.X + work.Width)) <= 1)
            right = monitor.X + monitor.Width;
        if (Math.Abs(bottom - (work.Y + work.Height)) <= 1)
            bottom = monitor.Y + monitor.Height;

        return new Rectangle(left, top, right - left, bottom - top);
    }

    private List<MonitorTarget> Targets()
    {
        var targets = new List<MonitorTarget>();
        foreach (var screen in _monitors)
        {
            if (screen.Arranged.Enabled)
                targets.Add(Target(screen));
        }

        return targets;
    }

    private static MonitorTarget Target(LiveMonitor screen)
    {
        var monitor = screen.Arranged;
        return new MonitorTarget(screen.Id, monitor.Name, monitor.Description, monitor.X, monitor.Y, monitor.Width, monitor.Height);
    }

    private static MonitorTarget? TargetOf(LiveMonitor? screen) =>
        screen is { Arranged.Enabled: true } enabled ? Target(enabled) : null;

    private LiveMonitor? MonitorOf(MonitorTarget? target) =>
        target is MonitorTarget found ? EnabledMonitor(found.Id) : null;

    private LiveMonitor? ResolveMonitor(string text) =>
        MonitorOf(MonitorSelector.Resolve(text, Targets(), TargetOf(FocusedMonitor())));

    private HashSet<WindowId> ShowingNow()
    {
        var shown = new HashSet<WindowId>();
        foreach (var window in ShownWindows())
            shown.Add(window);

        return shown;
    }

    private (WindowId[] Show, WindowId[] Hide) VisibilityDelta(HashSet<WindowId> before)
    {
        var after = ShowingNow();
        var show = new List<WindowId>();
        var hide = new List<WindowId>();
        foreach (var window in after)
        {
            if (!before.Contains(window))
                show.Add(window);
        }

        foreach (var window in before)
        {
            if (!after.Contains(window))
                hide.Add(window);
        }

        return (show.ToArray(), hide.ToArray());
    }

    private static bool TryMonitorArgument(string arguments, out string monitor, out bool silent)
    {
        silent = false;
        monitor = "";
        var text = arguments.Trim();
        if (text.EndsWith(" silent", StringComparison.OrdinalIgnoreCase))
        {
            silent = true;
            text = text[..^7].TrimEnd();
        }

        if (!text.StartsWith("mon:", StringComparison.OrdinalIgnoreCase))
            return false;

        monitor = text[4..].Trim();
        return true;
    }

    private static string FirstToken(string arguments)
    {
        var text = arguments.Trim();
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }
}
