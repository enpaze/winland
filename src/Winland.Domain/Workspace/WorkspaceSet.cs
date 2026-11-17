using System.Collections;

namespace Winland.Domain;

internal sealed class WorkspaceSet : IReadOnlyCollection<LiveWorkspace>
{
    private readonly List<LiveWorkspace> _all = [];

    private readonly Dictionary<string, long> _rememberedShown = new(StringComparer.OrdinalIgnoreCase);

    public ShownWorkspaces Shown { get; } = new();

    public int Count => _all.Count;

    public IEnumerator<LiveWorkspace> GetEnumerator() => _all.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public LiveWorkspace? Find(WorkspaceId id)
    {
        foreach (var desk in _all)
        {
            if (desk.Id == id)
                return desk;
        }

        return null;
    }

    public LiveWorkspace? On(MonitorId monitor, WorkspaceId id) =>
        Find(id) is LiveWorkspace desk && desk.Monitor == monitor ? desk : null;

    public void Add(LiveWorkspace desk)
    {
        ArgumentNullException.ThrowIfNull(desk);
        _all.Add(desk);
    }

    public void Remove(LiveWorkspace desk)
    {
        ArgumentNullException.ThrowIfNull(desk);
        _all.Remove(desk);
        Shown.Remove(desk.Id);
    }

    public void DropIfEmpty(WorkspaceId id, bool shown)
    {
        if (shown || Find(id) is not LiveWorkspace desk || desk.Persistent || desk.Layout.Clients.Count > 0)
            return;

        Remove(desk);
    }

    public IReadOnlyList<WorkspaceId> Listed(MonitorId? only)
    {
        var named = new List<WorkspaceId>();
        var numbered = new List<(long Number, WorkspaceId Id)>();
        foreach (var desk in _all)
        {
            if (desk.Id.IsSpecial)
                continue;

            if (only is MonitorId monitor && desk.Monitor != monitor)
                continue;

            if (desk.Id.TryGetNumber(out var number))
                numbered.Add((number, desk.Id));
            else
                named.Add(desk.Id);
        }

        named.Reverse();
        numbered.Sort((left, right) => left.Number.CompareTo(right.Number));
        foreach (var (_, id) in numbered)
            named.Add(id);

        return named;
    }

    public bool NumberIsEmpty(long number, MonitorId monitor) =>
        Find(WorkspaceId.FromNumber(number, monitor)) is not LiveWorkspace desk || desk.Layout.Clients.Count == 0;

    public long FirstFree(MonitorId monitor)
    {
        for (var number = 1L; number < WorkspaceId.MaxNumber; number++)
        {
            if (Find(WorkspaceId.FromNumber(number, monitor)) is null)
                return number;
        }

        throw new InvalidOperationException("No workspace id remains.");
    }

    public WorkspaceId NextFree(MonitorId monitor) => WorkspaceId.FromNumber(FirstFree(monitor), monitor);

    public long NumberOn(LiveWorkspace desk, MonitorId monitor)
    {
        ArgumentNullException.ThrowIfNull(desk);
        var preferred = desk.Id.TryGetNumber(out var number) ? number : 1;
        var occupant = Find(WorkspaceId.FromNumber(preferred, monitor));
        if (occupant is null || occupant == desk)
            return preferred;

        for (var candidate = 1L; candidate < WorkspaceId.MaxNumber; candidate++)
        {
            var found = Find(WorkspaceId.FromNumber(candidate, monitor));
            if (found is null || found == desk)
                return candidate;
        }

        throw new InvalidOperationException("No workspace id remains.");
    }

    public long NextGuestNumber(MonitorId monitor)
    {
        long highest = 0;
        var occupied = new HashSet<long>();
        foreach (var desk in _all)
        {
            if (desk.Monitor != monitor || desk.Id.IsSpecial || !desk.Id.TryGetNumber(out var number))
                continue;

            occupied.Add(number);
            if (number > highest)
                highest = number;
        }

        var candidate = highest + 1;
        var full = true;
        for (var number = 1L; number <= 10; number++)
        {
            if (occupied.Contains(number))
                continue;

            full = false;
            break;
        }

        if (full && candidate < 11)
            candidate = 11;

        while (occupied.Contains(candidate))
            candidate++;

        return candidate;
    }

    public WorkspaceRename? Rebind(LiveWorkspace desk, WorkspaceId to)
    {
        ArgumentNullException.ThrowIfNull(desk);
        var from = desk.Id;
        if (from != to)
            desk.Readdress(to);

        if (!to.IsSpecial)
            desk.Monitor = new MonitorId(to.Monitor);

        if (from == to)
            return null;

        Shown.Rename(from, to);
        return new WorkspaceRename(from, to);
    }

    public WorkspaceRename? MoveTo(LiveWorkspace desk, MonitorId destination)
    {
        ArgumentNullException.ThrowIfNull(desk);
        desk.ClearGuest();
        return Rebind(desk, WorkspaceId.FromNumber(NumberOn(desk, destination), destination));
    }

    public IReadOnlyList<WorkspaceRename> Depart(MonitorId leaving, string key, MonitorId? host)
    {
        if (Shown.LastShown(leaving) is WorkspaceId last
            && Find(last) is { Id.IsSpecial: false } shown
            && shown.Id.TryGetNumber(out var shownNumber))
            _rememberedShown[key] = shownNumber;

        var renames = new List<WorkspaceRename>();
        if (host is not MonitorId to)
            return renames;

        var departing = _all.Where(desk => desk.Monitor == leaving && !desk.Id.IsSpecial).ToList();
        departing.Sort(static (left, right) => NumberOf(left).CompareTo(NumberOf(right)));
        foreach (var desk in departing)
        {
            if (desk.Layout.Clients.Count == 0)
            {
                Remove(desk);
                continue;
            }

            if (!desk.IsGuest && desk.Id.TryGetNumber(out var home))
                desk.MarkGuest(key, home);

            Collect(renames, Rebind(desk, WorkspaceId.FromNumber(NextGuestNumber(to), to)));
        }

        return renames;
    }

    public IReadOnlyList<WorkspaceRename> Return(MonitorId returning, string key, out long? shownNumber)
    {
        var guests = _all.Where(desk => string.Equals(desk.HomeIdentity, key, StringComparison.OrdinalIgnoreCase)).ToList();
        guests.Sort(static (left, right) => left.HomeNumber.CompareTo(right.HomeNumber));
        var renames = new List<WorkspaceRename>();
        foreach (var desk in guests)
        {
            var number = desk.HomeNumber > 0 ? desk.HomeNumber : 1;
            desk.ClearGuest();
            var id = WorkspaceId.FromNumber(number, returning);
            if (Find(id) is not null)
                id = NextFree(returning);

            Collect(renames, Rebind(desk, id));
        }

        shownNumber = _rememberedShown.Remove(key, out var remembered) ? remembered : null;
        return renames;
    }

    private static long NumberOf(LiveWorkspace desk) =>
        desk.Id.TryGetNumber(out var number) ? number : 0;

    private static void Collect(List<WorkspaceRename> renames, WorkspaceRename? rename)
    {
        if (rename is WorkspaceRename made)
            renames.Add(made);
    }
}

internal readonly record struct WorkspaceRename(WorkspaceId From, WorkspaceId To);
