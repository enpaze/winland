namespace Winland.Application;

public sealed record MonitorSnapshot(MonitorId Id, Monitor Monitor, WorkspaceId ActiveWorkspace, bool Focused)
{
    public WorkspaceId? SpecialWorkspace { get; init; }
}

public sealed record State(IReadOnlyList<Client> Clients)
{
    public IReadOnlyList<Workspace> Workspaces { get; init; } = [];

    public WorkspaceId ActiveWorkspace { get; init; } = WorkspaceId.Initial;

    public WorkspaceId? SpecialWorkspace { get; init; }

    public MonitorId ActiveMonitor { get; init; } = MonitorId.Primary;

    public IReadOnlyList<Client> Hidden { get; init; } = [];

    public IReadOnlyList<Monitor> Monitors { get; init; } = [];

    public IReadOnlyList<MonitorSnapshot> Screens { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<WindowId>> Groups { get; init; } = [];

    public IReadOnlyList<WindowId> Stacking { get; init; } = [];
}

public sealed record Outcome(
    IReadOnlyList<Placement> Placements,
    State State,
    bool Consumed = false,
    WindowId? Focus = null)
{
    public DispatcherTraits? Traits { get; init; }

    public IReadOnlyList<WindowId> Closes { get; init; } = [];

    public WindowId? Close
    {
        get => Closes.Count > 0 ? Closes[0] : null;
        init => Closes = value is WindowId window ? [window] : [];
    }

    public IReadOnlyList<WindowId> Show { get; init; } = [];

    public IReadOnlyList<WindowId> Hide { get; init; } = [];

    public IReadOnlyList<WindowId> ShowMinimized { get; init; } = [];

    public IReadOnlyList<Launch> Launches { get; init; } = [];

    public Launch? Launch
    {
        get => Launches.Count > 0 ? Launches[0] : null;
        init => Launches = value is null ? [] : [value];
    }

    public IReadOnlyList<StreamEvent> Events { get; init; } = [];

    public IReadOnlyList<Decoration> Decorations { get; init; } = [];

    public IReadOnlyList<Motion> Motions { get; init; } = [];

    public bool Live { get; init; }

    public IReadOnlyList<WindowId> ResetDecorations { get; init; } = [];

    public IReadOnlyList<GroupBar> GroupBars { get; init; } = [];

    public ConfigNotice? ConfigNotice { get; init; }

    public TrayIcon? Tray { get; init; }


    public WarpTo? Warp { get; init; }

    public CursorBehavior? Cursor { get; init; }

    public string? ResizeCursor { get; init; }

    public string? Error { get; init; }


    public bool Exit { get; init; }

    public IReadOnlyList<ZOrderRequest> ZOrder { get; init; } = [];

    public bool IsEmpty { get; init; }

    public Outcome Then(Outcome next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return next with
        {
            Placements = Union(Placements, next.Placements, next.State),
            Focus = next.Focus ?? Focus,
            Closes = [.. Closes, .. next.Closes],
            Show = [.. Show.Where(window => !next.Hide.Contains(window)), .. next.Show],
            Hide = [.. Hide.Where(window => !next.Show.Contains(window)), .. next.Hide],
            ShowMinimized = [.. ShowMinimized, .. next.ShowMinimized],
            Launches = [.. Launches, .. next.Launches],
            Events = [.. Events, .. next.Events],
            Tray = next.Tray ?? Tray,
            Traits = Traits is { } mine ? mine | (next.Traits ?? DispatcherTraits.None) : next.Traits,
            Warp = next.Warp ?? Warp,
            Cursor = next.Cursor ?? Cursor,
            Error = Error ?? next.Error,
            Exit = Exit || next.Exit,
            ZOrder = [.. ZOrder, .. next.ZOrder],
            IsEmpty = IsEmpty && next.IsEmpty,
        };
    }

    internal bool IsEmptyAfter(Outcome? applied) =>
        applied is not null
        && Placements.Count == 0
        && !Consumed
        && Traits is null
        && Focus is null
        && Closes.Count == 0
        && Show.Count == 0
        && Hide.Count == 0
        && ShowMinimized.Count == 0
        && Launches.Count == 0
        && Events.Count == 0
        && Motions.Count == 0
        && !Live
        && ResetDecorations.Count == 0
        && Warp is null
        && Error is null
        && !Exit
        && ZOrder.Count == 0
        && ResizeCursor == applied.ResizeCursor
        && Cursor == applied.Cursor
        && Tray == applied.Tray
        && ConfigNotice == applied.ConfigNotice
        && Decorations.SequenceEqual(applied.Decorations)
        && GroupBars.Count == applied.GroupBars.Count
        && GroupBars.Zip(applied.GroupBars, (bar, last) => bar.SameAs(last)).All(same => same);

    internal Outcome Lasting() =>
        Empty(State) with
        {
            ResizeCursor = ResizeCursor,
            Cursor = Cursor,
            Tray = Tray,
            ConfigNotice = ConfigNotice,
            Decorations = Decorations,
            GroupBars = GroupBars,
        };

    internal static Outcome Empty(State state) => new([], state) { IsEmpty = true };

    internal static IReadOnlyList<Placement> Union(IReadOnlyList<Placement> first, IReadOnlyList<Placement> second, State state)
    {
        if (first.Count == 0)
            return second;

        var placed = new HashSet<ClientId>(second.Count);
        foreach (var placement in second)
            placed.Add(placement.Client);

        var open = new HashSet<ClientId>(state.Clients.Count);
        foreach (var client in state.Clients)
            open.Add(client.Id);
        foreach (var client in state.Hidden)
            open.Remove(client.Id);

        var merged = new List<Placement>(first.Count + second.Count);
        foreach (var placement in first)
        {
            if (!placed.Contains(placement.Client) && open.Contains(placement.Client))
                merged.Add(placement);
        }

        merged.AddRange(second);
        return merged;
    }
}

[Flags]
public enum DispatcherTraits
{
    None = 0,

    WarpsToFocused = 1,

    WarpsToEmptyMonitor = 2,
}

public readonly record struct ZOrderRequest(WindowId Window, bool Top);
