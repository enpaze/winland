namespace Winland.Application;

public sealed partial class WindowManager
{
    private Taken OntoMonitor(Taken taken, MonitorId from, MonitorId to)
    {
        if (!taken.Floating || from == to || EnabledMonitor(from) is not { } source || EnabledMonitor(to) is not { } destination)
            return taken;

        var bounds = taken.Bounds;
        return taken with { Bounds = bounds with { X = bounds.X - source.Bounds.X + destination.Bounds.X, Y = bounds.Y - source.Bounds.Y + destination.Bounds.Y } };
    }

    private Outcome MoveTo(
        string argument,
        bool silent,
        bool consumed,
        Point? focal = null)
    {
        if (!WorkspaceAddress.TryResolve(argument, Context(), out var resolved))
            return View([], consumed: consumed) with { Error = WorkspaceAddress.IsNamed(argument) ? "error: no such workspace" : null };

        return MoveTo(resolved, silent, consumed, focal);
    }

    private Outcome MoveTo(
        WorkspaceId target,
        bool silent,
        bool consumed,
        Point? focal = null)
    {
        if (ActiveWindow() is not WindowId id)
            return View([], consumed: consumed);

        var source = WorkspaceContaining(id);
        if (source is not null && target.IsSpecial && _pins.IsPinned(id))
        {
            if (silent)
                return View([], consumed: consumed);

            var opened = OpenSpecial(target, null);
            Give(id, source);
            return opened with { Focus = id, Consumed = consumed };
        }

        if (source is null || source.Id == target)
            return View([], consumed: consumed);

        var sourceCovered = source.Covered();
        var wasHidden = !_workspaces.Shown.IsShown(source.Id) || sourceCovered.Contains(id);
        var taken = default(Taken);
        if (!(source.IsWaiting(id) ? TakeForGroup(source, id, out taken) : source.Take(id, out taken)))
            return View([], consumed: consumed);

        ForgetDrag(id);

        var destination = WorkspaceFor(target, target.IsSpecial ? FocusedId() : null);
        var elsewhere = target.IsSpecial ? null : ShownElsewhere(target);

        var destinationCovered = destination.Covered();
        var area = WorkArea(destination, _area);
        destination.Receive(id, OntoMonitor(taken, source.Monitor, destination.Monitor), area, pointer: focal);
        CarryGroup(source, destination, id);
        Restamp(source, []);
        Restamp(destination, []);

        if (target.IsSpecial)
            return FinishSpecialMove(source, destination, id, silent, consumed, sourceCovered, destinationCovered);

        if (!silent && source.Id.IsSpecial && _workspaces.Shown.SpecialOn(source.Monitor) == source.Id)
        {
            var concealed = ConcealSpecial(source.Monitor);
            return FollowMove(SwitchTo(target, id, consumed), id, concealed, destination);
        }

        if (!silent && elsewhere is { } owner)
        {
            var followed = FocusShown(owner, target, id, consumed);
            var show = new List<WindowId>();
            var conceal = new List<WindowId>();
            if (_workspaces.Shown.IsShown(source.Id))
                source.Collect(sourceCovered, show, conceal);
            if (_workspaces.Shown.IsShown(destination.Id))
                Arrive(show, conceal);
            var moved = new List<Placement>();
            if (_workspaces.Shown.IsShown(source.Id))
                moved.AddRange(source.Layout.Placed());
            if (_workspaces.Shown.IsShown(destination.Id))
                moved.AddRange(destination.Layout.Visible());
            return followed with
            {
                Placements = moved,
                Show = [.. followed.Show, .. show],
                Hide = [.. followed.Hide, .. conceal],
            };
        }

        if (!silent)
        {
            var here = _workspaces.Shown.ActiveOn(FocusedId()) == target;
            var followedSwitch = SwitchTo(target, id, consumed);
            if (here)
            {
                var show = new List<WindowId>(followedSwitch.Show);
                var conceal = new List<WindowId>(followedSwitch.Hide);
                if (_workspaces.Shown.IsShown(source.Id))
                    source.Collect(sourceCovered, show, conceal);
                Arrive(show, conceal);
                var moved = new List<Placement>();
                if (_workspaces.Shown.IsShown(source.Id))
                    moved.AddRange(source.Layout.Placed());
                moved.AddRange(destination.Layout.Visible());
                followedSwitch = followedSwitch with { Placements = moved, Show = show, Hide = conceal };
            }
            else if (_workspaces.Shown.IsShown(source.Id))
            {
                followedSwitch = followedSwitch with { Placements = [.. source.Layout.Placed(), .. followedSwitch.Placements] };
            }

            return followedSwitch.Focus is null && _focus.Focused == id
                ? followedSwitch with { Focus = id }
                : followedSwitch;
        }

        var revealed = new List<WindowId>();
        var hidden = new List<WindowId>();
        if (_workspaces.Shown.IsShown(source.Id))
            source.Collect(sourceCovered, revealed, hidden);
        if (_workspaces.Shown.IsShown(destination.Id))
            Arrive(revealed, hidden);
        else
            hidden.Add(id);

        if (source.Id.IsSpecial
            && source.Layout.Clients.Count == 0
            && _workspaces.Shown.SpecialOn(source.Monitor) == source.Id)
        {
            foreach (var concealed in ConcealSpecial(source.Monitor))
            {
                if (!hidden.Contains(concealed))
                    hidden.Add(concealed);
            }
        }

        var refocus = _focus.Focused == id;
        if (refocus)
            Give(FocusState.AfterLeaving(source, revealed), source);

        var placements = new List<Placement>();
        if (_workspaces.Shown.IsShown(source.Id))
            placements.AddRange(source.Layout.Placed());
        if (_workspaces.Shown.IsShown(destination.Id))
            placements.AddRange(destination.Layout.Visible());

        return View(placements, revealed, hidden, consumed, focus: refocus ? Kept() : null);

        void Arrive(List<WindowId> show, List<WindowId> conceal)
        {
            destination.Collect(destinationCovered, show, conceal);
            if (wasHidden
                && destination.ClientOf(id) is { Minimized: false }
                && !destination.Covered().Contains(id)
                && !show.Contains(id))
                show.Add(id);
        }
    }

    private Outcome FollowMove(Outcome followed, WindowId window, WindowId[] concealed, LiveWorkspace destination)
    {
        var show = new List<WindowId>(followed.Show);
        if (_workspaces.Shown.IsShown(destination.Id) && !show.Contains(window))
            show.Add(window);

        var placements = followed.Placements.Count > 0 ? followed.Placements : destination.Layout.Visible();
        return followed with
        {
            Placements = placements,
            Show = show,
            Hide = Combine(followed.Hide, concealed),
            Focus = followed.Focus ?? window,
        };
    }

    private Outcome SwitchTo(WorkspaceId target, WindowId? focus, bool consumed)
    {
        if (target.IsSpecial)
            return OpenSpecial(target, focus);

        if (ShownElsewhere(target) is { } elsewhere)
        {
            var away = ConcealSpecial(elsewhere.Id);
            var followed = FocusShown(elsewhere, target, focus, consumed);
            return away.Length == 0 ? followed : followed with { Hide = Combine(followed.Hide, away) };
        }

        if (_workspaces.Find(target) is { } located && EnabledMonitor(located.Monitor) is { } home && home.Id != FocusedId())
            Focus(home.Id);

        var monitor = FocusedId();
        var concealed = ConcealSpecial(monitor);

        var sourceId = _workspaces.Shown.ActiveOn(monitor);
        if (sourceId == target)
        {
            if (focus is WindowId requested && ClientOn(target, requested) is not null)
                Give(requested, FocusedWorkspace());

            return View([], hide: concealed, consumed: consumed, focus: concealed.Length > 0 || focus is not null ? Kept() : null);
        }

        var keep = _focus.Focused is WindowId current
            && _pins.IsPinned(current)
            && ClientOn(sourceId, current) is not null
            ? current
            : (WindowId?)null;
        _workspaces.Shown.Show(monitor, target);

        NoteWorkspaceActive(target);
        Remember(target, monitor);
        var destination = WorkspaceFor(target, monitor);
        CarryPinned(sourceId, destination);
        var hide = VisibleIds(sourceId);
        Give(
            focus is WindowId requestedFocus && ClientOn(target, requestedFocus) is not null
                ? requestedFocus
                : keep is WindowId pinnedFocus && ClientOn(target, pinnedFocus) is not null
                    ? pinnedFocus
                    : _focus.Remembered(destination),
            destination);

        DropIfEmpty(sourceId);

        return View(
            destination.Layout.Visible(),
            VisibleIds(target),
            Combine(hide, concealed),
            consumed,
            focus: Kept());
    }

    private LiveMonitor? ShownElsewhere(WorkspaceId target)
    {
        var desk = _workspaces.Find(target);
        if (desk is null)
            return null;

        var owner = EnabledMonitor(desk.Monitor);
        if (owner is null || _workspaces.Shown.Shown(owner.Id) != target || owner.Id == FocusedId())
            return null;

        return owner;
    }

    private Outcome FocusShown(LiveMonitor owner, WorkspaceId target, WindowId? focus, bool consumed)
    {
        Focus(owner.Id);
        var destination = _workspaces.Find(target);
        WindowId? next = null;
        if (focus is WindowId requested && ClientOn(target, requested) is not null)
            next = requested;
        else if (destination is not null)
            next = _focus.Remembered(destination);

        Give(next, destination);

        return View([], focus: next, consumed: consumed);
    }

    private void Remember(WorkspaceId workspace, MonitorId monitor) =>
        _workspaces.Shown.Visit(workspace, monitor);

    private void NoteCrossMonitorWarp(MonitorId from, Outcome switched)
    {
        _crossMonitorWarp = null;
        var owner = FocusedId();
        if (owner == from || MonitorById(owner) is not LiveMonitor screen)
            return;

        var bounds = screen.Bounds;
        if (_focus.Focused is WindowId window)
        {
            foreach (var placement in FocusedWorkspace().Layout.Visible())
            {
                if (placement.Client.Value == window.Value)
                {
                    bounds = placement.Bounds;
                    break;
                }
            }

            foreach (var placement in switched.Placements)
            {
                if (placement.Client.Value == window.Value)
                    bounds = placement.Bounds;
            }
        }

        _crossMonitorWarp = new WarpTo(new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)));
    }

    private WorkspaceId? Previous(MonitorId monitor, bool perMonitor) => _workspaces.Shown.Previous(monitor, perMonitor);

    private WorkspaceAddress.Context Context() => Context(FocusedId());

    private WorkspaceAddress.Context Context(MonitorId monitor)
    {
        var active = _workspaces.Shown.ActiveOn(monitor);
        if (!active.IsSpecial && active.Monitor != monitor.Value)
            active = new WorkspaceId(active.Name, monitor.Value);

        return new WorkspaceAddress.Context(
            active,
            () => Previous(monitor, perMonitor: false),
            () => Previous(monitor, perMonitor: true),
            _workspaces.Listed(monitor),
            number => _workspaces.NumberIsEmpty(number, monitor));
    }

    private void DropIfEmpty(WorkspaceId id) => _workspaces.DropIfEmpty(id, _workspaces.Shown.IsShown(id));

    private void Readdressed(WorkspaceRename? rename)
    {
        if (rename is not WorkspaceRename (var from, var to))
            return;

        _initialWorkspaces.Update(workspace => workspace == from ? to : workspace);
        (_readdressed ??= []).Add((from, to));
    }

    private void Readdressed(IReadOnlyList<WorkspaceRename> renames)
    {
        foreach (var rename in renames)
            Readdressed(rename);
    }

    private void DropEmpty()
    {
        foreach (var desk in _workspaces.ToArray())
            DropIfEmpty(desk.Id);
    }

}
