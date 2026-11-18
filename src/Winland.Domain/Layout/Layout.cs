namespace Winland.Domain;

internal interface Layout
{
    IReadOnlyList<Client> Clients { get; }

    IReadOnlyList<Placement> Apply(LayoutOptions options);

    void Fit(Rectangle area, Rectangle monitor);

    void Readdress(WorkspaceId workspace);

    void Retitle(WindowId window, string title);

    void Reclass(WindowId window, string windowClass);

    IReadOnlyList<Placement> Add(
        WindowId window,
        Rectangle area,
        Point? pointer,
        bool floating,
        Rectangle bounds,
        WorkspaceId workspace,
        string windowClass = "",
        string title = "",
        Rectangle monitor = default,
        long processId = 0,
        string? initialClass = null,
        string? initialTitle = null,
        Size? minSize = null,
        Size? maxSize = null,
        TiledLimits? tiledLimits = null);

    IReadOnlyList<Placement> UpdateLimits(WindowId window, Size? minSize, Size? maxSize, TiledLimits? tiledLimits = null);

    IReadOnlyList<Placement> Remove(WindowId window);

    IReadOnlyList<Placement> Minimize(WindowId window);

    IReadOnlyList<Placement> Restore(WindowId window);

    bool TryTake(WindowId window, out Taken taken);

    bool Substitute(WindowId current, WindowId next, Taken incoming, out Taken outgoing);

    bool TryBounds(WindowId window, out Rectangle bounds);

    IReadOnlyList<Placement> Visible();

    IReadOnlyList<Placement> Placed();

    IReadOnlyList<Placement> Arrange(IReadOnlyDictionary<WindowId, Rectangle> tiles) => [];

    WindowId? MoveFocus(WindowId active, string arguments);

    WindowId? Neighbor(WindowId active, string arguments) => MoveFocus(active, arguments);

    void NoteFocus(WindowId window);

    void NotePointerFocus(WindowId window)
    {
    }

    bool TakeMoved() => false;

    void RankFocus(Func<WindowId, int> recency)
    {
    }

    IReadOnlyList<Placement> MoveWindow(WindowId window, string arguments);

    bool FollowsMonitorWhenUnmoved => false;

    void NudgeFloating(int dx, int dy)
    {
    }

    IReadOnlyList<Placement> ResizeActive(WindowId window, string arguments);

    IReadOnlyList<Placement> ToggleFloating(WindowId window, Point? pointer = null);

    IReadOnlyList<Placement> Swap(WindowId active, WindowId other) => [];

    IReadOnlyList<Placement> LayoutMessage(WindowId? active, string arguments);

    IReadOnlyList<Placement> Drag(WindowId window, PointerDrag drag);

    MouseDrag MouseDrag { get; }

    string? LayoutMessageError { get; }

    WindowId? LayoutMessageFocus => null;

    void Configure(ScrollingOptions options)
    {
    }

    void NoteScrollingWidth(WindowId window, double width)
    {
    }

    IReadOnlyList<Placement> SetFullscreen(WindowId window, FullscreenMode mode);

    IReadOnlyList<Placement> SetFullscreen(WindowId window, FullscreenMode mode, bool layoutAware) =>
        SetFullscreen(window, mode);

    bool FullscreenCovers(WindowId window) => true;

    IReadOnlyList<Placement> PlaceFloating(WindowId window, Rectangle bounds) => [];
}
