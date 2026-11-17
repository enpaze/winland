namespace Winland.Application;

internal sealed record StateChange
{
    private StateChange(State before, State after)
    {
        Before = before;
        After = after;
        Was = ByWindow(before.Clients);
        Is = ByWindow(after.Clients);
        HiddenBefore = new HashSet<WindowId>(before.Hidden.Select(client => client.Window));
        HiddenAfter = new HashSet<WindowId>(after.Hidden.Select(client => client.Window));
        Closed = [.. before.Clients.Select(client => client.Window).Where(window => !Is.ContainsKey(window))];
        Opened = [.. after.Clients.Select(client => client.Window).Where(window => !Was.ContainsKey(window))];
    }

    public State Before { get; }

    public State After { get; }

    public IReadOnlyDictionary<WindowId, Client> Was { get; }

    public IReadOnlyDictionary<WindowId, Client> Is { get; }

    public IReadOnlySet<WindowId> HiddenBefore { get; }

    public IReadOnlySet<WindowId> HiddenAfter { get; }

    public IReadOnlyList<WindowId> Closed { get; }

    public IReadOnlyList<WindowId> Opened { get; }

    public static StateChange Between(State before, State after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return new StateChange(before, after);
    }

    public bool WasShown(WindowId window) => Was.ContainsKey(window) && !HiddenBefore.Contains(window);

    private static Dictionary<WindowId, Client> ByWindow(IReadOnlyList<Client> clients)
    {
        var map = new Dictionary<WindowId, Client>(clients.Count);
        foreach (var client in clients)
            map[client.Window] = client;

        return map;
    }
}
