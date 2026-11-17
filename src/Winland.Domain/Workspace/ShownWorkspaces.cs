namespace Winland.Domain;

internal sealed class ShownWorkspaces
{
    private readonly HashSet<MonitorId> _live = [];
    private readonly HashSet<MonitorId> _enabled = [];
    private readonly Dictionary<MonitorId, WorkspaceId> _shown = [];
    private readonly Dictionary<MonitorId, WorkspaceId> _lastShown = [];
    private readonly Dictionary<MonitorId, WorkspaceId> _special = [];
    private readonly Dictionary<MonitorId, WorkspaceId> _lastSpecial = [];

    private readonly List<Visited> _visits = [];

    public WorkspaceId? Shown(MonitorId monitor) => _shown.TryGetValue(monitor, out var shown) ? shown : null;

    public WorkspaceId? LastShown(MonitorId monitor) => _lastShown.TryGetValue(monitor, out var last) ? last : null;

    public WorkspaceId ActiveOn(MonitorId monitor) =>
        Shown(monitor) ?? (_lastShown.TryGetValue(monitor, out var last) ? last : WorkspaceId.Initial);

    public WorkspaceId? SpecialOn(MonitorId monitor)
    {
        var map = _live.Contains(monitor) ? _special : _lastSpecial;
        return map.TryGetValue(monitor, out var special) ? special : null;
    }

    public void Show(MonitorId monitor, WorkspaceId workspace)
    {
        if (_live.Contains(monitor))
            _shown[monitor] = workspace;
        _lastShown[monitor] = workspace;
    }

    public void ShowSpecial(MonitorId monitor, WorkspaceId? workspace)
    {
        if (workspace is WorkspaceId special)
        {
            if (_live.Contains(monitor))
                _special[monitor] = special;
            _lastSpecial[monitor] = special;
            return;
        }

        _special.Remove(monitor);
        _lastSpecial.Remove(monitor);
    }

    public void SetLive(IEnumerable<MonitorId> monitors, IEnumerable<MonitorId>? enabled = null)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var next = new HashSet<MonitorId>(monitors);
        _enabled.Clear();
        _enabled.UnionWith(enabled ?? next);
        foreach (var monitor in _live)
        {
            if (!next.Contains(monitor))
                Unassign(monitor);
        }

        foreach (var monitor in next)
        {
            if (!_live.Contains(monitor))
                Unassign(monitor);
        }

        _live.Clear();
        _live.UnionWith(next);
    }

    public bool IsShown(WorkspaceId workspace)
    {
        if (workspace.IsSpecial)
            return ShowingSpecial(workspace) is not null;

        if (_live.Count == 0)
            return workspace == ActiveOn(MonitorId.Primary);

        return _enabled.Any(monitor => Shown(monitor) == workspace);
    }

    public MonitorId? ShowingSpecial(WorkspaceId special)
    {
        if (_live.Count == 0)
            return SpecialOn(MonitorId.Primary) == special ? MonitorId.Primary : null;

        foreach (var monitor in _enabled)
        {
            if (SpecialOn(monitor) == special)
                return monitor;
        }

        return null;
    }

    public bool ShowsAWorkspace(MonitorId monitor) =>
        _live.Count == 0 || (_enabled.Contains(monitor) && Shown(monitor) is not null);

    public void Rename(WorkspaceId from, WorkspaceId to)
    {
        Replace(_shown, from, to);
        Replace(_lastShown, from, to);
        Replace(_special, from, to);
        Replace(_lastSpecial, from, to);
        for (var i = 0; i < _visits.Count; i++)
        {
            if (!_visits[i].Gone && _visits[i].Workspace == from)
                _visits[i] = _visits[i] with { Workspace = to };
        }
    }

    public void Remove(WorkspaceId workspace)
    {
        Drop(_shown, workspace);
        Drop(_lastShown, workspace);
        for (var i = 0; i < _visits.Count; i++)
        {
            if (_visits[i].Workspace == workspace)
                _visits[i] = _visits[i] with { Gone = true };
        }
    }

    public void Visit(WorkspaceId workspace, MonitorId monitor)
    {
        if (_visits.Count > 0 && _visits[0] is { Gone: false } newest && newest.Workspace == workspace)
            return;

        _visits.RemoveAll(visit => !visit.Gone && visit.Workspace == workspace);
        _visits.Insert(0, new Visited(workspace, monitor));
    }

    public WorkspaceId? Previous(MonitorId monitor, bool perMonitor)
    {
        Collect();
        var active = ActiveOn(monitor);
        var index = _visits.FindIndex(visit => !visit.Gone && visit.Workspace == active);
        if (!perMonitor)
            return index >= 0 && index + 1 < _visits.Count ? _visits[index + 1].Workspace : null;

        for (var i = index < 0 ? 0 : index + 1; i < _visits.Count; i++)
        {
            if (_visits[i].Monitor == monitor)
                return _visits[i].Workspace;
        }

        return null;
    }

    private void Unassign(MonitorId monitor)
    {
        _shown.Remove(monitor);
        _special.Remove(monitor);
    }

    private void Collect()
    {
        var seen = new List<MonitorId>();
        _visits.RemoveAll(visit =>
        {
            for (var i = 1; i < seen.Count; i++)
            {
                if (seen[i] == visit.Monitor)
                    return visit.Gone;
            }

            seen.Add(visit.Monitor);
            return false;
        });
    }

    private static void Replace(Dictionary<MonitorId, WorkspaceId> map, WorkspaceId from, WorkspaceId to)
    {
        foreach (var (monitor, workspace) in map.ToArray())
        {
            if (workspace == from)
                map[monitor] = to;
        }
    }

    private static void Drop(Dictionary<MonitorId, WorkspaceId> map, WorkspaceId workspace)
    {
        foreach (var (monitor, shown) in map.ToArray())
        {
            if (shown == workspace)
                map.Remove(monitor);
        }
    }

    private readonly record struct Visited(WorkspaceId Workspace, MonitorId Monitor, bool Gone = false);
}
