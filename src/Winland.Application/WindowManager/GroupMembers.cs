namespace Winland.Application;

public sealed partial class WindowManager
{
    private static bool GroupOnOpen(LiveWorkspace desk, WindowId window, WindowDecision decision, bool dialog, WindowId? focused, bool show)
    {
        if (focused is not WindowId owner
            || owner == window
            || desk.GroupOf(owner) is not WindowGroup group
            || dialog
            || (decision.Floating && desk.ClientOf(owner)?.Floating != true)
            || !desk.Layout.TryTake(window, out var taken))
            return false;

        var position = group.Current + 1;
        desk.Join(group, window, taken, position);
        if (show)
            desk.Show(group, position);
        return true;
    }

    private bool IsWaiting(WindowId window) => WorkspaceContaining(window)?.IsWaiting(window) == true;

    private Outcome? CloseGroupMember(WindowId window, LiveWorkspace desk, HashSet<WindowId> covered, FullscreenMode mode)
    {
        if (desk.GroupOf(window) is not WindowGroup group)
            return null;

        if (group.Count == 1)
        {
            desk.Leave(group, window, out _);
            return null;
        }

        var wasShown = group.Shown == window;
        var wasActive = _focus.Focused == window;
        desk.Leave(group, window, out _);

        if (wasShown && mode != FullscreenMode.None && desk.ClientOf(group.Shown) is { Fullscreen: not FullscreenMode.None })
            desk.Layout.SetFullscreen(group.Shown, FullscreenMode.None);

        if (!wasShown || !_workspaces.Shown.IsShown(desk.Id))
        {
            if (wasActive)
                Give(null, null);

            return View([]);
        }

        WindowId? focus = null;
        if (wasActive)
        {
            focus = RecentMember(group) ?? group.Shown;
            if (focus != group.Shown && group.IndexOf(focus.Value) is var index and >= 0)
                _ = desk.Show(group, index);

            Give(focus.Value, desk);
        }

        return Present(desk, covered, desk.Layout.Visible(), focus: focus);
    }

    private WindowId? RecentMember(WindowGroup group)
    {
        foreach (var window in _focus.History)
        {
            if (group.Members.Contains(window))
                return window;
        }

        return null;
    }

    private Outcome? RaiseMember(LiveWorkspace desk, WindowId window)
    {
        if (!desk.IsWaiting(window) || desk.GroupOf(window) is not WindowGroup group)
            return null;

        return ShowMember(desk, group, group.IndexOf(window));
    }

    private static void CarryGroup(LiveWorkspace source, LiveWorkspace destination, WindowId window)
    {
        if (source.Id != destination.Id && source.GroupOf(window) is WindowGroup group && group.Shown == window)
            source.HandGroup(group, destination);
    }

}
