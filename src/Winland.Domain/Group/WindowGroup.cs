namespace Winland.Domain;

internal sealed class WindowGroup(WindowId first)
{
    private readonly List<WindowId> _members = [first];

    public IReadOnlyList<WindowId> Members => _members;

    public int Current { get; private set; }

    public WindowId Shown => _members[Current];

    public int Count => _members.Count;

    public bool Contains(WindowId window) => _members.Contains(window);

    public int IndexOf(WindowId window) => _members.IndexOf(window);

    public int CurrentAfterRemoving(int index) =>
        index == Current ? Math.Max(0, Current - 1) : index < Current ? Current - 1 : Current;

    public void Insert(int position, WindowId window)
    {
        position = Math.Clamp(position, 0, _members.Count);
        _members.Insert(position, window);
        if (position <= Current)
            Current++;
    }

    public void ShowAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _members.Count);
        Current = index;
    }

    public void RemoveAt(int index)
    {
        var current = CurrentAfterRemoving(index);
        _members.RemoveAt(index);
        Current = current;
    }
}

internal enum GroupExit
{
    NotMember,

    Dissolved,

    Detached,
}
