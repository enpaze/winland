namespace Winland.Domain;

internal sealed class PinnedClients
{
    private readonly HashSet<WindowId> _pinned = [];

    public bool IsPinned(WindowId window) => _pinned.Contains(window);

    public void Pin(WindowId window)
    {
        _pinned.Add(window);
    }

    public void Unpin(WindowId window) => _pinned.Remove(window);

    public void PinAtOpen(WindowId window, bool floating, bool ruled, WindowId? owner)
    {
        if (floating && (ruled || (owner is WindowId owning && IsPinned(owning))))
            Pin(window);
    }

    public bool StaysVisible(WindowId window, bool minimized, bool monitorShowsAWorkspace) =>
        IsPinned(window) && !minimized && monitorShowsAWorkspace;
}
