namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome ToggleGroup()
    {
        if (ActiveWindow() is not WindowId id || WorkspaceContaining(id) is not LiveWorkspace desk)
            return View([]);

        var covered = desk.Covered();
        if (Find(id) is { Fullscreen: not FullscreenMode.None })
            _ = Restamp(desk, EndFullscreen(desk, id));

        if (desk.GroupOf(id) is not WindowGroup group)
        {
            StartGroup(desk, id);
            return Present(desk, covered, desk.Layout.Visible());
        }

        var head = group.Members[0];
        var focus = desk.Focus;
        var area = WorkArea(desk, _area);
        foreach (var (window, taken) in desk.EndGroup(group))
            desk.Receive(window, taken, area);

        FocusState.Remember(desk, focus);
        if (focus is WindowId kept)
            desk.Layout.NoteFocus(kept);

        (_extraEvents ??= []).Add(new GroupToggled(head, false));
        return Present(desk, covered, desk.Layout.Visible());
    }

    private WindowGroup StartGroup(LiveWorkspace desk, WindowId window)
    {
        var group = desk.StartGroup(window);
        (_extraEvents ??= []).Add(new GroupToggled(window, true));
        return group;
    }

    private Outcome ChangeGroupActive(string text)
    {
        if (ActiveWindow() is not WindowId id || WorkspaceContaining(id) is not LiveWorkspace desk)
            return Rejected("error: no window found");

        if (desk.GroupOf(id) is not WindowGroup group)
            return Rejected("error: window is not in a group");

        if (group.Count == 1)
            return Rejected("error: only one window in group");

        int index;
        if (int.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var number))
            index = number <= 0 ? group.Count - 1 : Math.Min(number - 1, group.Count - 1);
        else
            index = text is "b" or "prev" ? (group.Current + group.Count - 1) % group.Count : (group.Current + 1) % group.Count;

        return ShowMember(desk, group, index);
    }

    private Outcome ScrollGroupBar(GroupBarScrolled scrolled)
    {
        if (WorkspaceContaining(scrolled.Shown) is not LiveWorkspace desk
            || desk.GroupOf(scrolled.Shown) is not WindowGroup group || group.Count < 2)
        {
            return View([]);
        }

        var index = scrolled.Forward ? (group.Current + 1) % group.Count : (group.Current + group.Count - 1) % group.Count;
        return ShowMember(desk, group, index);
    }

    private Outcome ShowMember(LiveWorkspace desk, WindowGroup group, int index)
    {
        var covered = desk.Covered();
        var shown = group.Shown;
        var hadFocus = _focus.Focused == shown;
        if (!desk.Show(group, index))
            return View([]);

        WindowId? focus = null;
        if (hadFocus)
        {
            focus = group.Shown;
            Give(group.Shown, desk);
        }

        return _workspaces.Shown.IsShown(desk.Id)
            ? Present(desk, covered, desk.Layout.Visible(), focus: focus)
            : View([]);
    }

    private Outcome MoveIntoGroup(string text)
    {
        if (!TryGroupDirection(text, out var direction))
            return Rejected("error: unsupported direction: " + text);

        if (ActiveWindow() is not WindowId id || WorkspaceContaining(id) is not LiveWorkspace desk)
            return View([]);

        if (desk.Layout.Neighbor(id, direction) is not WindowId neighbor || neighbor == id)
        {
            if (NeighborAcross(desk, id, direction) is not (WindowId across, LiveWorkspace there))
                return View([]);

            return IntoGroupAcross(desk, there, id, across);
        }

        if (desk.GroupOf(neighbor) is null)
            return View([]);

        return IntoGroup(desk, desk.Covered(), id, neighbor);
    }

    private Outcome IntoGroup(LiveWorkspace desk, HashSet<WindowId> covered, WindowId window, WindowId destination)
    {
        var target = desk.GroupOf(destination);
        if (target is not null && target.Contains(window))
            return Present(desk, covered, desk.Layout.Visible());

        if (!TakeForGroup(desk, window, out var taken))
            return View([]);

        target ??= StartGroup(desk, destination);
        var position = target.Current + 1;
        desk.Join(target, window, taken, position);
        desk.Show(target, position);
        Give(window, desk);
        (_extraEvents ??= []).Add(new ClientIntoGroup(window));
        return Present(desk, covered, desk.Layout.Visible(), focus: window);
    }

    private (WindowId Window, LiveWorkspace Desk)? NeighborAcross(LiveWorkspace desk, WindowId window, string direction)
    {
        if (!Sides.TryParse(direction, out var side)
            || EnabledMonitor(desk.Monitor) is not LiveMonitor origin || TargetOf(origin) is not MonitorTarget from
            || MonitorSelector.InDirection(Targets(), from, side) is not MonitorTarget neighbor
            || MonitorOf(neighbor) is not LiveMonitor screen || _workspaces.Shown.Shown(screen.Id) is not WorkspaceId shown
            || _workspaces.Find(shown) is not LiveWorkspace there || !desk.Layout.TryBounds(window, out var box)
            || AdjacentOn(screen, desk, window, box, side) is not WindowId found)
            return null;

        return (found, there);
    }

    private Outcome IntoGroupAcross(LiveWorkspace desk, LiveWorkspace there, WindowId window, WindowId neighbor)
    {
        if (there.GroupOf(neighbor) is not WindowGroup target)
            return View([]);

        var covered = there.Covered();
        var sourceCovered = desk.Covered();
        if (!TakeForGroup(desk, window, out var taken))
            return View([]);

        var position = target.Current + 1;
        there.Join(target, window, OntoMonitor(taken, desk.Monitor, there.Monitor), position);
        there.Show(target, position);
        Restamp(desk, []);
        Restamp(there, []);
        DropIfEmpty(desk.Id);
        Focus(there.Monitor);
        Give(window, there);
        (_extraEvents ??= []).Add(new ClientIntoGroup(window));
        var outcome = Present(there, covered, there.Layout.Visible(), focus: window);
        if (_workspaces.Find(desk.Id) is not LiveWorkspace left || !_workspaces.Shown.IsShown(left.Id))
            return outcome;

        var show = new List<WindowId>(outcome.Show);
        var hide = new List<WindowId>(outcome.Hide);
        left.Collect(sourceCovered, show, hide);
        return outcome with { Placements = [.. left.Layout.Placed(), .. outcome.Placements], Show = show, Hide = hide };
    }

    private static bool TakeForGroup(LiveWorkspace desk, WindowId window, out Taken taken)
    {
        taken = default;
        if (desk.GroupOf(window) is WindowGroup own)
        {
            var exit = desk.Leave(own, window, out taken);
            if (exit == GroupExit.Detached)
                return true;
            if (exit == GroupExit.NotMember)
                return false;
        }

        return desk.Layout.TryTake(window, out taken);
    }

    private Outcome MoveOutOfGroup(string text)
    {
        if (text.Length > 0)
            return Rejected("error: moveoutofgroup takes no argument");

        if (ActiveWindow() is not WindowId id || WorkspaceContaining(id) is not LiveWorkspace desk)
            return View([]);

        if (desk.GroupOf(id) is not WindowGroup group)
            return Rejected("error: window not in a group");

        return OutOfGroup(desk, group, id);
    }

    private Outcome OutOfGroup(LiveWorkspace desk, WindowGroup group, WindowId window)
    {
        var covered = desk.Covered();
        var exit = desk.Leave(group, window, out var taken);
        if (exit == GroupExit.Detached)
        {
            desk.Layout.NoteFocus(group.Shown);
            desk.Receive(window, taken, WorkArea(desk, _area));
        }

        Give(window, desk);
        (_extraEvents ??= []).Add(new ClientOutOfGroup(window));
        return _workspaces.Shown.IsShown(desk.Id)
            ? Present(desk, covered, desk.Layout.Visible(), focus: window)
            : View([]);
    }

    private static bool TryGroupDirection(string text, out string direction)
    {
        var parsed = Sides.TryParse(text, out var side);
        direction = !parsed ? "" : side switch
        {
            Side.Left => "l",
            Side.Right => "r",
            Side.Up => "u",
            _ => "d",
        };
        return parsed;
    }
}
