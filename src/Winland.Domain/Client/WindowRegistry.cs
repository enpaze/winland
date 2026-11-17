namespace Winland.Domain;

internal sealed class WindowRegistry
{
    private readonly List<Action<WindowId>> _forgetters = [];

    public void Track<T>(Dictionary<WindowId, T> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _forgetters.Add(window => map.Remove(window));
    }

    public void Track(HashSet<WindowId> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _forgetters.Add(window => set.Remove(window));
    }

    public void Track(List<WindowId> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        _forgetters.Add(window => list.RemoveAll(item => item == window));
    }

    public void Track(Action<WindowId> forget)
    {
        ArgumentNullException.ThrowIfNull(forget);
        _forgetters.Add(forget);
    }

    public void Forget(WindowId window)
    {
        foreach (var forget in _forgetters)
            forget(window);
    }
}
