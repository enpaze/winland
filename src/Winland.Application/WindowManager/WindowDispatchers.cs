namespace Winland.Application;

/// <summary>Dispatchers that act on one window and the daemon: focus, float, swap, move to a Workspace and <c>exit</c>.</summary>
public sealed partial class WindowManager
{
    private Outcome FocusWindow(string arguments)
    {
        var selector = arguments.Trim();
        if (selector.Length == 0 || WindowSelector.Find(selector, ActiveWindow(), EveryClient()) is not WindowId id)
            return Rejected("error: no such window");

        return FocusClient(id);
    }

    private Outcome FocusClient(WindowId id)
    {
        if (WorkspaceContaining(id) is not LiveWorkspace desk)
            return Rejected("error: no such window");

        // A window shadowed by a modal refuses the focus, though its Workspace is still shown.
        if (HasNoFocus(id))
        {
            return _workspaces.Shown.IsShown(desk.Id)
                ? View([])
                : SwitchTo(desk.Id, null, consumed: false);
        }

        if (_pins.IsPinned(id))
            return FocusPinned(desk, id);

        var covered = desk.Covered();
        id = FocusUnderFullscreen(desk, id, out var refit);
        var raised = RaiseMember(desk, id);
        desk.Layout.NoteFocus(id);
        if (desk.Id.IsSpecial)
        {
            var special = OpenSpecial(desk.Id, id);
            return raised is null ? special : raised.Then(special);
        }

        var outcome = SwitchTo(desk.Id, id, consumed: false);
        outcome = outcome.Focus is null ? outcome with { Focus = id } : outcome;
        if (refit is not null && _workspaces.Shown.IsShown(desk.Id))
            outcome = Present(desk, covered, refit).Then(outcome);
        return raised is null ? outcome : raised.Then(outcome);
    }

    private Outcome FloatWindow(string arguments)
    {
        if (arguments.Trim().Length > 0)
            return Rejected("error: togglefloating takes no argument");

        if (ActiveWindow() is not WindowId id || Find(id) is not Client client || WorkspaceContaining(id) is not LiveWorkspace desk)
            return View([]);

        var covered = desk.Covered();
        var placements = desk.Layout.ToggleFloating(id, _pointer);
        // Only a floating Client is pinned, including one whose pin a fullscreen mode holds.
        _pins.Unpin(id);

        // A window that floats is raised, and so allowed over a fullscreen one.
        if (!client.Floating)
            _cover.SetFlag(id, true);

        return _workspaces.Shown.IsShown(desk.Id)
            ? Present(desk, covered, placements)
            : View([]);
    }

    private Outcome SwapWindow(string arguments)
    {
        var text = arguments.Trim();
        if (ActiveWindow() is not WindowId current || WorkspaceContaining(current) is not LiveWorkspace home)
            return Rejected("error: No window to swap with");

        // Winland's answers, in its order.
        if (Find(current) is { Fullscreen: not FullscreenMode.None })
            return Rejected("error: Can't swap fullscreen window");

        if (!Sides.TryParse(text, out _) || home.Layout.Neighbor(current, text) is not WindowId other || other == current)
            return Rejected("error: No window to swap with in that direction");
        if (Find(other) is { Fullscreen: not FullscreenMode.None })
            return Rejected("error: Can't swap fullscreen window");

        var covered = home.Covered();
        var placements = home.Layout.Swap(current, other);
        if (placements.Count == 0)
            return Rejected("error: No window to swap with");

        return _workspaces.Shown.IsShown(home.Id)
            ? Present(home, covered, placements)
            : View(placements);
    }

    private Outcome ExitDaemon() =>
        View([]) with { Exit = true };

    // Every Client a window selector can find, including one waiting in a Group.
    private List<Client> EveryClient()
    {
        var clients = new List<Client>();
        foreach (var desk in _workspaces)
        {
            clients.AddRange(desk.Layout.Clients);
            clients.AddRange(desk.WaitingClients);
        }

        return clients;
    }

    private Outcome Rejected(string error) =>
        View([]) with { Error = error };
}
