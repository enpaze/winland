namespace Winland.Domain;

internal sealed class FocusState(Func<WindowId, bool> canTake)
{
    private readonly List<WindowId> _history = [];

    public WindowId? Focused { get; private set; }

    public IReadOnlyList<WindowId> History => _history;

    public WindowId? Unmanaged { get; private set; }

    public WindowId? BeforeUnmanaged { get; private set; }

    public void Take(WindowId? window, LiveWorkspace? desk = null)
    {
        Focused = window;
        desk?.RememberFocus(window);
    }

    public static void Remember(LiveWorkspace desk, WindowId? window)
    {
        ArgumentNullException.ThrowIfNull(desk);
        desk.RememberFocus(window);
    }

    public void Confirm(WindowId window)
    {
        _history.Remove(window);
        _history.Insert(0, window);
    }

    public void UnmanagedTook(WindowId window)
    {
        BeforeUnmanaged = Focused;
        Unmanaged = window;
    }

    public WindowId? Adopted(WindowId window)
    {
        if (Unmanaged != window)
            return null;

        var prior = BeforeUnmanaged;
        Unmanaged = null;
        BeforeUnmanaged = null;
        return prior;
    }

    public void Forget(WindowId window)
    {
        _history.Remove(window);
        if (Unmanaged == window)
        {
            Unmanaged = null;
            BeforeUnmanaged = null;
        }

        if (BeforeUnmanaged == window)
            BeforeUnmanaged = null;
    }

    public int Recency(WindowId window)
    {
        var index = _history.IndexOf(window);
        return index < 0 ? 0 : _history.Count - index;
    }

    public WindowId? Remembered(LiveWorkspace desk)
    {
        ArgumentNullException.ThrowIfNull(desk);
        return desk.Remembered() is WindowId window && canTake(window) ? window : null;
    }

    public WindowId? Recent(LiveWorkspace desk)
    {
        ArgumentNullException.ThrowIfNull(desk);
        foreach (var window in _history)
        {
            if (desk.ClientOf(window) is { Minimized: false } && !desk.IsWaiting(window) && canTake(window))
                return window;
        }

        return null;
    }

    public static WindowId? AfterLeaving(LiveWorkspace source, IReadOnlyList<WindowId> revealed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(revealed);
        return revealed.Count == 1 && source.ClientOf(revealed[0]) is not null ? revealed[0] : source.LastVisible();
    }
}
