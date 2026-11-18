namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly HashSet<WindowId> _awaitingMapFocus = [];

    private readonly List<WindowId> _stacking = [];

    private void Stack(WindowId window)
    {
        _stacking.Remove(window);
        _stacking.Add(window);
    }

    private Outcome Consider(WindowFocused focused)
    {
        var window = focused.Window;
        if (_awaitingMapFocus.Remove(window) && focused.Cause != FocusCause.Requested)
            return Offering(View([]), Kept());

        if (focused.Cause == FocusCause.Requested)
        {
            Foreground(window);
            return View([]);
        }

        if (Find(window) is null)
        {
            _focus.UnmanagedTook(window);
            Foreground(window);
            return View([]);
        }

        if (HasNoFocus(window))
            return Offering(View([]), Kept());

        var activated = focused.Cause == FocusCause.Activated;
        var afterInput = ActivatedAfterClick(focused);
        if ((activated || afterInput) && (activated || _props.Own(window, WindowProp.FocusOnActivate) != true))
        {
            if (_focus.Focused == window)
                return View([]);

            MarkUrgent(window);
            return Offering(View([]), Kept());
        }

        if (WorkspaceContaining(window) is { } home && (!_workspaces.Shown.IsShown(home.Id) || home.IsWaiting(window)))
            return FocusClient(window);

        if (WorkspaceContaining(window) is { } desk
            && !desk.Id.IsSpecial
            && _workspaces.Shown.IsShown(desk.Id)
            && _workspaces.Shown.SpecialOn(desk.Monitor) is not null)
        {
            var hidden = ConcealSpecial(desk.Monitor);
            Foreground(window);
            return View([], hide: hidden);
        }

        if (WorkspaceContaining(window) is { } owner && _workspaces.Shown.IsShown(owner.Id))
        {
            var covered = owner.Covered();
            var target = FocusUnderFullscreen(owner, window, out var refit);
            if (target != window)
                return Offering(View([]), target);

            if (refit is not null)
            {
                Foreground(window);
                return Present(owner, covered, refit);
            }
        }

        Foreground(window);
        return View([]);
    }

    private bool ActivatedAfterClick(WindowFocused focused)
    {
        if (focused.Cause != FocusCause.User
            || focused.Clicked is not WindowId clicked
            || clicked == focused.Window
            || Find(clicked) is not { } source
            || Find(focused.Window) is not { } target)
            return false;

        return source.ProcessId == 0 || target.ProcessId == 0 || source.ProcessId != target.ProcessId;
    }

    private void RememberFocus(WindowId window, WindowDecision decision)
    {
        if (_focus.Adopted(window) is WindowId kept && IsShownClient(kept))
            Give(kept, WorkspaceContaining(kept));

        if (!SkipsInitialFocus(window))
            return;

        _awaitingMapFocus.Add(window);
    }

    private void ForgetFocusMarks(WindowId window)
    {
        if (_lastMouse == window)
            _lastMouse = null;
        _focus.Forget(window);
        foreach (var desk in _workspaces)
        {
            if (desk.Focus == window)
                FocusState.Remember(desk, null);
        }
    }

    private void Give(WindowId? window, LiveWorkspace? desk, bool pointer = false)
    {
        _focus.Take(window, desk);
        if (window is not WindowId given || desk is null || !_workspaces.Shown.IsShown(desk.Id) || !IsCurrent(desk))
            return;

        if (_monitors.Count > 0)
            Focus(desk.Monitor);

        if (pointer)
            desk.Layout.NotePointerFocus(given);
        else
            desk.Layout.NoteFocus(given);

        _focus.Confirm(given);
    }

    private bool IsCurrent(LiveWorkspace desk) =>
        (_workspaces.Shown.SpecialOn(desk.Monitor) ?? _workspaces.Shown.ActiveOn(desk.Monitor)) == desk.Id;

    private void Foreground(WindowId window, bool pointer = false)
    {
        if (_focus.Focused == window)
            return;

        if (WorkspaceContaining(window) is { } owner && _workspaces.Shown.IsShown(owner.Id))
        {
            if (IsCurrent(owner))
                Give(window, owner, pointer);
            else if (_monitors.Count > 0)
                Focus(owner.Monitor);
        }
        else if (Find(window) is null)
            Give(null, null);
    }

    private WindowId? FocusAfter(LiveWorkspace desk, bool floating, Rectangle bounds, bool hadBounds)
    {
        if (floating)
            return _focus.Recent(desk) ?? LastAccepted(desk);

        var centre = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        if (hadBounds && (Closest(desk, centre, tiledOnly: true) ?? Closest(desk, centre, tiledOnly: false)) is WindowId near)
            return near;

        return null;
    }

    private WindowId? Closest(LiveWorkspace desk, Point point, bool tiledOnly)
    {
        WindowId? best = null;
        var bestDistance = long.MaxValue;
        var showing = desk.Showing();
        foreach (var client in desk.Layout.Clients)
        {
            if (!Accepts(desk, client.Window) || !showing.Contains(client.Window) || (tiledOnly && client.Floating))
                continue;

            if (!desk.Layout.TryBounds(client.Window, out var bounds))
                continue;

            var distance = DistanceTo(point, bounds);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = client.Window;
        }

        return best;
    }

    private WindowId? LastAccepted(LiveWorkspace desk)
    {
        for (var i = desk.Layout.Clients.Count - 1; i >= 0; i--)
        {
            if (Accepts(desk, desk.Layout.Clients[i].Window))
                return desk.Layout.Clients[i].Window;
        }

        return null;
    }

    private bool Accepts(LiveWorkspace desk, WindowId window) =>
        desk.ClientOf(window) is { Minimized: false } && !HasNoFocus(window);

    private static long DistanceTo(Point point, Rectangle box)
    {
        var dx = 0L;
        if (point.X < box.X)
            dx = box.X - point.X;
        else if (point.X >= box.X + box.Width)
            dx = point.X - (box.X + box.Width);

        var dy = 0L;
        if (point.Y < box.Y)
            dy = box.Y - point.Y;
        else if (point.Y >= box.Y + box.Height)
            dy = point.Y - (box.Y + box.Height);

        return (dx * dx) + (dy * dy);
    }

    private bool SkipsInitialFocus(WindowId window)
    {
        if (_props.FocusRuleOf(window) is not FocusRule rule)
            return false;

        return rule.NoInitialFocus || rule.NoFocus;
    }

    private bool HasNoFocus(WindowId window) =>
        ShadowedByModal(window) || _props.NoFocus(window);

    private WindowId? Kept() =>
        _focus.Focused is WindowId current && IsShownClient(current) ? current : null;

    private bool IsShownClient(WindowId window)
    {
        var desk = WorkspaceContaining(window);
        if (desk is null || !_workspaces.Shown.IsShown(desk.Id))
            return false;

        return desk.ClientOf(window) is { Minimized: false };
    }

    private static Outcome Offering(Outcome outcome, WindowId? focus) =>
        focus is WindowId window ? outcome with { Focus = window } : outcome;
}
