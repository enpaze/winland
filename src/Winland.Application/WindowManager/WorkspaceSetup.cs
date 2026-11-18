namespace Winland.Application;

public sealed partial class WindowManager
{
    private LiveWorkspace WorkspaceFor(WorkspaceId id, MonitorId? monitor = null)
    {
        if (!id.IsSpecial && monitor is MonitorId place)
            id = new WorkspaceId(id.Name, place.Value);

        var existing = _workspaces.Find(id);
        if (existing is not null)
            return existing;

        var owner = id.IsSpecial ? monitor ?? FocusedId() : new MonitorId(id.Monitor);
        var layout = LayoutFactory.Create(_config.Document);
        layout.Configure(_config.Scrolling);
        RankFocus(layout);
        var desk = new LiveWorkspace(id, owner, layout);
        _workspaces.Add(desk);
        Stamp(desk);

        MatchLayout(desk);
        layout = desk.Layout;
        var screen = MonitorById(desk.Monitor);
        if (screen is { Arranged.Enabled: true })
        {
            Stamp(desk);
            layout.Apply(desk.Options);
            layout.Fit(screen.Arranged.WorkArea, screen.Bounds);
        }
        else
        {
            layout.Apply(desk.Options);
        }

        return desk;
    }

    private void Stamp(LiveWorkspace desk)
    {
        desk.Options = OptionsFor(desk);
        if (desk.Layout is ScrollingLayout scrolling)
            scrolling.OtherMonitors = [.. _monitors.Where(screen => screen.Arranged.Enabled && screen.Id != desk.Monitor).Select(screen => screen.Bounds)];
    }

    private LayoutOptions OptionsFor(LiveWorkspace desk)
    {
        var screen = MonitorById(desk.Monitor);
        var scale = screen is { Arranged.Enabled: true } ? screen.Arranged.Scale : 1;
        return (_config.Layout with { ClientBorder = _clientBorder }).ScaledBy(scale);
    }
}
