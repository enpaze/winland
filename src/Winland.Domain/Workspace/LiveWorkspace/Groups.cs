namespace Winland.Domain;

internal sealed partial class LiveWorkspace
{
    private readonly List<WindowGroup> _groups = [];
    private readonly Dictionary<WindowId, Waiting> _waiting = [];

    public IReadOnlyList<WindowGroup> Groups => _groups;

    public IEnumerable<Client> WaitingClients
    {
        get
        {
            foreach (var waiting in _waiting.Values)
                yield return waiting.Client;
        }
    }

    public bool IsWaiting(WindowId window) => _waiting.ContainsKey(window);

    public WindowGroup? GroupOf(WindowId window)
    {
        foreach (var group in _groups)
        {
            if (group.Contains(window))
                return group;
        }

        return null;
    }

    public WindowGroup StartGroup(WindowId window)
    {
        var group = new WindowGroup(window);
        _groups.Add(group);
        return group;
    }

    public List<(WindowId Window, Taken Taken)> EndGroup(WindowGroup group)
    {
        var released = new List<(WindowId, Taken)>();
        if (!_groups.Remove(group))
            return released;

        Show(group, group.Count - 1);
        for (var i = 0; i < group.Count - 1; i++)
        {
            if (_waiting.Remove(group.Members[i], out var waiting))
                released.Add((group.Members[i], waiting.Taken));
        }

        return released;
    }

    public void Join(WindowGroup group, WindowId window, Taken taken, int position)
    {
        group.Insert(position, window);

        var floating = ClientOf(group.Shown)?.Floating ?? taken.Floating;
        _waiting[window] = Wait(window, taken with { Floating = floating, Minimized = false });
    }

    public bool Show(WindowGroup group, int index)
    {
        if (index < 0 || index >= group.Count || index == group.Current)
            return false;

        var shown = group.Shown;
        var next = group.Members[index];
        if (!_waiting.Remove(next, out var waiting))
            return false;

        if (!Layout.Substitute(shown, next, waiting.Taken, out var outgoing))
        {
            _waiting[next] = waiting;
            return false;
        }

        Unminimize(next);
        _waiting[shown] = Wait(shown, outgoing);
        group.ShowAt(index);
        if (Focus == shown)
            Focus = next;

        return true;
    }

    public GroupExit Leave(WindowGroup group, WindowId window, out Taken taken)
    {
        taken = default;
        var index = group.IndexOf(window);
        if (index < 0)
            return GroupExit.NotMember;

        if (group.Count == 1)
        {
            _groups.Remove(group);
            return GroupExit.Dissolved;
        }

        var current = group.CurrentAfterRemoving(index);
        var shown = group.Shown;
        var nextIndex = current < index ? current : current + 1;
        var next = group.Members[nextIndex];
        if (window == shown)
        {
            if (!_waiting.Remove(next, out var waiting))
                return GroupExit.NotMember;

            if (!Layout.Substitute(window, next, waiting.Taken, out taken))
            {
                _waiting[next] = waiting;
                return GroupExit.NotMember;
            }

            Unminimize(next);
        }
        else
        {
            if (!_waiting.Remove(window, out var leaving))
                return GroupExit.NotMember;

            taken = leaving.Taken;
        }

        group.RemoveAt(index);
        if (Focus == window)
            Focus = group.Shown;

        return GroupExit.Detached;
    }

    public void HandGroup(WindowGroup group, LiveWorkspace destination)
    {
        if (!_groups.Remove(group))
            return;

        destination._groups.Add(group);
        foreach (var member in group.Members)
        {
            if (_waiting.Remove(member, out var waiting))
                destination._waiting[member] = waiting with { Client = waiting.Client with { Workspace = destination.Id } };
        }
    }

    private void Unminimize(WindowId window)
    {
        if (ClientOf(window) is { Minimized: true })
            Layout.Restore(window);
    }

    private Client? WaitingOf(WindowId window) =>
        _waiting.TryGetValue(window, out var waiting) ? waiting.Client : null;

    private Waiting Wait(WindowId window, Taken taken) =>
        new(
            new Client(
                new ClientId(window.Value),
                window,
                Floating: taken.Floating,
                Workspace: Id,
                Class: taken.Class,
                Title: taken.Title,
                ProcessId: taken.ProcessId,
                InitialClass: taken.InitialClass ?? taken.Class,
                InitialTitle: taken.InitialTitle ?? taken.Title,
                Bounds: taken.Bounds),
            taken with { Minimized = false });

    private readonly record struct Waiting(Client Client, Taken Taken);
}
