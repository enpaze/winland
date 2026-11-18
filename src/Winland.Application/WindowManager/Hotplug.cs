namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly MonitorIdentity _identity = new();

    private Outcome SetMonitors(IReadOnlyList<Monitor> reported)
    {
        ArgumentNullException.ThrowIfNull(reported);
        var identified = _identity.Number(reported);
        var previous = _monitors.ToArray();
        var wasEnabled = new List<LiveMonitor>(previous.Length);
        foreach (var screen in previous)
        {
            if (screen.Arranged.Enabled)
                wasEnabled.Add(screen);
        }

        var next = new List<LiveMonitor>(identified.Length);
        for (var i = 0; i < identified.Length; i++)
        {
            var monitor = identified[i];
            var effective = monitor;
            var screen = FindTracked(previous, monitor) ?? FindTracked(next, monitor);
            if (screen is null)
                screen = new LiveMonitor(NextMonitorId(previous, next), monitor, effective);
            else
            {
                screen.Reported = monitor;
                screen.Arranged = effective;
            }

            next.Add(screen);
        }

        if (!next.Exists(screen => screen.Arranged.Enabled))
            next.Add(FindNamed(previous, FallbackName) ?? new LiveMonitor(NextMonitorId(previous, next), Fallback, Fallback));

        _monitors.Clear();
        _monitors.AddRange(next);
        _workspaces.Shown.SetLive(next.Select(screen => screen.Id), next.Where(screen => screen.Arranged.Enabled).Select(screen => screen.Id));

        var enabled = EnabledMonitors();
        LiveMonitor? home = null;
        foreach (var screen in enabled)
        {
            if (!screen.Focused)
                continue;

            home = screen;
            break;
        }

        home ??= enabled.Count > 0 ? enabled[0] : null;
        Relocate(wasEnabled, enabled, home);
        AssignActives(enabled);
        DropEmpty();
        RefitAll();
        EnsureFocus(home);
        var refocus = RefocusShown();
        return View(ShownPlacements(), ShownWindows(), HiddenWindows(), focus: refocus);
    }

    private void Relocate(List<LiveMonitor> wasEnabled, List<LiveMonitor> enabled, LiveMonitor? host)
    {
        foreach (var screen in enabled)
        {
            if (!HeldBy(wasEnabled, screen.Id))
                Return(screen, wasEnabled);
        }

        foreach (var screen in wasEnabled)
        {
            if (!HeldBy(enabled, screen.Id))
                Depart(screen, host);
        }

        RehomeSpecials(host, enabled);
    }

    private WindowId? RefocusShown()
    {
        if (_focus.Focused is not WindowId current || IsShownClient(current))
            return null;

        var desk = TopWorkspace();
        var next = _focus.Remembered(desk);
        Give(next, desk);
        return next;
    }

    private void Depart(LiveMonitor screen, LiveMonitor? host)
    {
        if (host is not null)
        {
            foreach (var desk in _workspaces.Where(desk => desk.Monitor == screen.Id && !desk.Id.IsSpecial))
                desk.Layout.NudgeFloating(host.Arranged.X - screen.Arranged.X, host.Arranged.Y - screen.Arranged.Y);
        }

        Readdressed(_workspaces.Depart(screen.Id, ReturnKey(screen.Reported), host?.Id));
    }

    private void Return(LiveMonitor screen, IReadOnlyList<LiveMonitor> previous)
    {
        foreach (var desk in _workspaces.Where(desk => string.Equals(desk.HomeIdentity, ReturnKey(screen.Reported), StringComparison.OrdinalIgnoreCase)))
        {
            var host = MonitorById(desk.Monitor) ?? previous.FirstOrDefault(item => item.Id == desk.Monitor);
            if (host is not null)
                desk.Layout.NudgeFloating(screen.Arranged.X - host.Arranged.X, screen.Arranged.Y - host.Arranged.Y);
        }

        Readdressed(_workspaces.Return(screen.Id, ReturnKey(screen.Reported), out var shownNumber));
        if (shownNumber is not long number)
            return;

        var shown = WorkspaceFor(WorkspaceId.FromNumber(number, screen.Id), screen.Id);
        _workspaces.Shown.Show(screen.Id, shown.Id);
    }

    private void AssignActives(List<LiveMonitor> enabled)
    {
        var claimed = new HashSet<WorkspaceId>();
        foreach (var screen in enabled)
        {
            if (_workspaces.Shown.Shown(screen.Id) is WorkspaceId active && _workspaces.On(screen.Id, active) is not null && claimed.Add(active))
                continue;

            LiveWorkspace? populated = null;
            LiveWorkspace? empty = null;
            long emptyNumber = long.MaxValue;
            foreach (var desk in _workspaces)
            {
                if (desk.Monitor != screen.Id || desk.Id.IsSpecial || claimed.Contains(desk.Id))
                    continue;

                if (desk.Layout.Clients.Count > 0)
                {
                    populated = desk;
                    break;
                }

                if (!desk.Id.TryGetNumber(out var number) || number >= emptyNumber)
                    continue;

                empty = desk;
                emptyNumber = number;
            }

            var one = WorkspaceId.FromNumber(1, screen.Id);
            LiveWorkspace own;
            if (populated is not null)
                own = populated;
            else if (_workspaces.Find(one) is LiveWorkspace first && !claimed.Contains(first.Id))
                own = first;
            else if (_workspaces.FirstFree(screen.Id) == 1)
                own = WorkspaceFor(one, screen.Id);
            else
                own = empty ?? WorkspaceFor(_workspaces.NextFree(screen.Id), screen.Id);

            _workspaces.Shown.Show(screen.Id, own.Id);
            claimed.Add(own.Id);
        }

        foreach (var screen in _monitors)
        {
            if (!screen.Arranged.Enabled && _workspaces.Shown.Shown(screen.Id) is null)
                _workspaces.Shown.Show(screen.Id, WorkspaceId.Initial);
        }
    }

    private void RefitAll()
    {
        foreach (var desk in _workspaces)
            Refit(desk);
    }

    private void Refit(LiveWorkspace desk)
    {
        Stamp(desk);
        if (EnabledMonitor(desk.Monitor) is not { } screen)
            return;

        desk.Layout.Apply(desk.Options);
        desk.Layout.Fit(screen.Arranged.WorkArea, screen.Bounds);
    }

    private void EnsureFocus(LiveMonitor? home)
    {
        foreach (var screen in _monitors)
        {
            if (screen.Focused && screen.Arranged.Enabled)
                return;
        }

        Focus(home?.Id ?? MonitorId.Primary);
    }

    private void Focus(MonitorId id)
    {
        var found = false;
        foreach (var screen in _monitors)
        {
            var focused = screen.Id == id && screen.Arranged.Enabled;
            screen.Focused = focused;
            if (focused)
                found = true;
        }

        if (!found)
        {
            foreach (var screen in _monitors)
            {
                if (!screen.Arranged.Enabled)
                    continue;

                screen.Focused = true;
                break;
            }
        }

        Remember(_workspaces.Shown.ActiveOn(FocusedId()), FocusedId());
    }

    private LiveMonitor? MonitorFor(WindowAppeared appeared)
    {
        if (_monitors.Count == 0)
            return null;

        if (appeared.Opened && !appeared.Traits.PlacesItself)
        {
            var home = appeared.Owner is WindowId owner && WorkspaceContaining(owner) is LiveWorkspace desk ? desk.Monitor : FocusedId();
            if (Match(screen => screen.Id == home) is LiveMonitor opened)
                return opened;
        }

        var byArea = Match(screen => screen.Reported.WorkArea == appeared.WorkArea);
        if (byArea is not null)
            return byArea;

        if (appeared.Pointer is { } pointer)
        {
            var byPointer = Match(screen => screen.Covers(pointer));
            if (byPointer is not null)
                return byPointer;
        }

        if (appeared.Bounds is { Width: > 0, Height: > 0 } bounds)
        {
            var center = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
            var byBounds = Match(screen => screen.Covers(center));
            if (byBounds is not null)
                return byBounds;
        }

        return Match(_ => true);
    }

    private LiveMonitor? Match(Func<LiveMonitor, bool> predicate)
    {
        foreach (var screen in _monitors)
        {
            if (screen.Arranged.Enabled && predicate(screen))
                return screen;
        }

        return null;
    }

    private static string ReturnKey(Monitor monitor) =>
        MonitorIdentity.Basis(monitor).Length > 0 ? monitor.Description : monitor.Name;

    private static LiveMonitor? FindTracked(IReadOnlyList<LiveMonitor> screens, Monitor monitor)
    {
        if (MonitorIdentity.Basis(monitor).Length > 0)
        {
            foreach (var screen in screens)
            {
                if (MonitorIdentity.Basis(screen.Reported).Length == 0)
                    continue;

                if (string.Equals(screen.Reported.Description, monitor.Description, StringComparison.Ordinal))
                    return screen;
            }

            return null;
        }

        return FindNamed(screens, monitor.Name);
    }

    private static LiveMonitor? FindNamed(IReadOnlyList<LiveMonitor> screens, string name)
    {
        foreach (var screen in screens)
        {
            if (string.Equals(screen.Reported.Name, name, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        return null;
    }

    private static bool HeldBy(List<LiveMonitor> enabled, MonitorId id)
    {
        foreach (var screen in enabled)
        {
            if (screen.Id == id)
                return true;
        }

        return false;
    }

    private const string FallbackName = "FALLBACK";

    private static readonly Monitor Fallback = new(FallbackName, "", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1080));

    private List<LiveMonitor> EnabledMonitors()
    {
        var enabled = new List<LiveMonitor>();
        foreach (var screen in _monitors)
        {
            if (screen.Arranged.Enabled)
                enabled.Add(screen);
        }

        return enabled;
    }

    private static MonitorId NextMonitorId(IReadOnlyList<LiveMonitor> previous, List<LiveMonitor> next)
    {
        var used = new HashSet<int>();
        foreach (var screen in previous)
            used.Add(screen.Id.Value);

        foreach (var screen in next)
            used.Add(screen.Id.Value);

        var id = 0;
        while (used.Contains(id))
            id++;

        return new MonitorId(id);
    }

    private Monitor[] Reported()
    {
        var monitors = new Monitor[_monitors.Count];
        for (var i = 0; i < _monitors.Count; i++)
            monitors[i] = _monitors[i].Reported;

        return monitors;
    }
}
