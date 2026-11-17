namespace Winland.Application;

public sealed partial class WindowManager
{
    private (WindowId Window, Rectangle Bounds)? _released;

    public bool AwaitsDragRelease => _interaction is NativeMove { Adopted: true };

    private IReadOnlyList<Placement> OpenDragged(WindowAppeared appeared, LiveWorkspace desk, IReadOnlyList<Placement> placements)
    {
        if (!appeared.Dragging || _interaction is not null || desk.ClientOf(appeared.Window) is not { Floating: false })
            return placements;

        _ = BeginNativeMove(new WindowMoveStarted(appeared.Window, appeared.Pointer ?? default));
        if (_interaction is NativeMove move)
            move.Adopted = true;
        _ = desk.Layout.PlaceFloating(appeared.Window, appeared.Bounds);
        _movedItself = appeared.Window;
        return [.. desk.Layout.Visible().Where(placement => placement.Client != new ClientId(appeared.Window.Value))];
    }

    private Outcome BeginNativeMove(WindowMoveStarted started)
    {
        if (_interaction is not null)
            return View([]);

        var window = started.Window;
        if (WorkspaceContaining(window) is not LiveWorkspace desk
            || desk.ClientOf(window) is not { Minimized: false } client)
            return View([]);

        _interaction = new NativeMove(window, client.Floating) { Pointer = started.Pointer };
        if (client.Floating)
            return BeginNativeFloating(desk, client);

        if (client.Fullscreen != FullscreenMode.None)
            _ = Restamp(desk, EndFullscreen(desk, window));

        var grab = new PointerDrag(false, PointerDragPhase.Begin, started.Pointer);
        _ = desk.Layout.Drag(window, grab);
        var lifted = desk.Layout.Drag(window, grab with { Phase = PointerDragPhase.Move });
        return View([.. lifted.Where(placement => placement.Client != client.Id)]);
    }

    private Outcome EndNativeMove(WindowMoveEnded ended)
    {
        if (_interaction is not NativeMove move || move.Window != ended.Window)
            return View([]);

        _interaction = null;
        var drop = GroupDrop(ended.Window, wasTiled: !move.Floating, ended.Pointer);
        if (!move.Floating)
            _released = ended.Bounds is Rectangle released ? (ended.Window, released) : null;
        if (ended.Bounds is Rectangle box
            && MoveWithMonitor(ended.Window, box, !move.Floating, settle: true, ended.Pointer) is Outcome crossed)
        {
            return drop is { } target ? JoinDragged(ended.Window, target, consumed: false) : crossed with { Consumed = false };
        }

        if (move.Floating)
        {
            var left = FollowNativeFloating(ended.Window, ended.Bounds);
            return drop is { } onto ? JoinDragged(ended.Window, onto, consumed: false) : left;
        }

        var layout = LayoutWorkspace(ended.Window).Layout;
        var dropped = layout.Drag(ended.Window, new PointerDrag(false, PointerDragPhase.End, ended.Pointer));
        if (drop is { } into)
            return JoinDragged(ended.Window, into, consumed: false);

        return View(dropped.Count > 0 ? dropped : layout.Placed());
    }

    private Outcome BeginNativeFloating(LiveWorkspace desk, Client client)
    {
        if (client.Fullscreen == FullscreenMode.None)
            return View([]);

        var covered = desk.Covered();
        var placements = Restamp(desk, EndFullscreen(desk, client.Window));
        return Present(desk, covered, [.. placements.Where(placement => placement.Client != client.Id)]);
    }

    private Outcome FollowNativeFloating(WindowId window, Rectangle? bounds)
    {
        if (bounds is not Rectangle frame || WorkspaceContaining(window) is not LiveWorkspace desk || desk.ClientOf(window) is not { Floating: true })
            return View([]);

        var placements = desk.Layout.PlaceFloating(window, frame);
        var self = new ClientId(window.Value);
        return View([.. placements.Where(placement => placement.Client != self)]);
    }

    private sealed class NativeMove(WindowId window, bool floating) : PointerInteraction(window)
    {
        public bool Floating { get; } = floating;

        public bool Adopted { get; set; }

        public Point Pointer { get; set; }
    }
}
