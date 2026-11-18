namespace Winland.Application;

public sealed partial class WindowManager
{
    private WorkspaceId ActiveId() => _workspaces.Shown.ActiveOn(FocusedId());

    [System.Diagnostics.Conditional("DEBUG")]
    private void CheckShownWorkspaces()
    {
        var seen = new HashSet<WorkspaceId>();
        foreach (var screen in _monitors)
        {
            if (screen.Arranged.Enabled && _workspaces.Shown.Shown(screen.Id) is WorkspaceId shown && !seen.Add(shown))
                throw new InvalidOperationException($"Workspace {shown} is shown on two Monitors.");
        }
    }

    private MonitorId FocusedId() =>
        FocusedMonitor()?.Id ?? (_monitors.Count == 0 ? MonitorId.Primary : _monitors[0].Id);

    private LiveMonitor? FocusedMonitor()
    {
        foreach (var screen in _monitors)
        {
            if (screen.Focused && screen.Arranged.Enabled)
                return screen;
        }

        foreach (var screen in _monitors)
        {
            if (screen.Arranged.Enabled)
                return screen;
        }

        return null;
    }

    private LiveWorkspace FocusedWorkspace() => WorkspaceFor(ActiveId(), FocusedId());

    private LiveWorkspace? WorkspaceContaining(WindowId window)
    {
        foreach (var desk in _workspaces)
        {
            if (desk.ClientOf(window) is not null)
                return desk;
        }

        return null;
    }

    private Client? Find(WindowId window)
    {
        foreach (var desk in _workspaces)
        {
            if (desk.ClientOf(window) is { } client)
                return client;
        }

        return null;
    }

    private Client? ClientOn(WorkspaceId id, WindowId window)
    {
        var desk = _workspaces.Find(id);
        return desk is null ? null : desk.ClientOf(window);
    }

    private Rectangle WorkArea(LiveWorkspace desk, Rectangle fallback)
    {
        var screen = MonitorById(desk.Monitor);
        if (screen is { Arranged.Enabled: true })
            return screen.Arranged.WorkArea;

        return fallback;
    }

    private LiveMonitor? MonitorById(MonitorId id)
    {
        foreach (var screen in _monitors)
        {
            if (screen.Id == id)
                return screen;
        }

        return null;
    }
}
