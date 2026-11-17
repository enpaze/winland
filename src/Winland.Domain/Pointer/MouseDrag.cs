namespace Winland.Domain;

internal sealed class MouseDrag
{
    private Grab? _grab;

    public IReadOnlyList<Placement> Step(IMouseDragHost host, WindowId window, PointerDrag drag)
    {
        ArgumentNullException.ThrowIfNull(host);
        return drag.Phase switch
        {
            PointerDragPhase.Begin => Begin(host, new ClientId(window.Value), drag),
            PointerDragPhase.Move => Move(host, drag.Pointer, drag.Resize),
            PointerDragPhase.End => End(host, drag.Pointer),
            _ => [],
        };
    }

    public bool Forget(ClientId id)
    {
        if (_grab is not { } grab || grab.Id != id)
            return false;

        _grab = null;
        return true;
    }

    public void HandTo(MouseDrag next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (ReferenceEquals(this, next) || _grab is not { } grab)
            return;

        _grab = null;
        next._grab = grab;
    }

    private IReadOnlyList<Placement> Begin(IMouseDragHost host, ClientId id, PointerDrag drag)
    {
        if (!host.TryDragBounds(id, out var bounds, out var floating))
        {
            _grab = null;
            return [];
        }

        var (left, top) = BorderPointer.GrabbedCorner(drag.Pointer, bounds);
        _grab = new Grab(id, drag.Resize, floating, drag.Pointer, bounds, left, top, drag.KeepRatio);
        return [];
    }

    private IReadOnlyList<Placement> Move(IMouseDragHost host, Point pointer, bool resize)
    {
        if (_grab is not Grab grab || grab.Resize != resize)
            return [];

        var floating = host.Floating;
        if (grab.WasFloating && !floating.Contains(grab.Id))
            return [];

        var dx = pointer.X - grab.Last.X;
        var dy = pointer.Y - grab.Last.Y;
        grab.Last = pointer;
        if (resize)
        {
            if (!grab.WasFloating)
                return host.ResizeTile(grab.Id, dx, dy, (grab.Left, grab.Top));

            var resized = BorderPointer.Resize(grab.Start, grab.Pressed, pointer, grab.Left, grab.Top, grab.KeepRatio);
            floating.Set(grab.Id, host.FloatingLimits(grab.Id).Clamp(resized));
            return Dragged(host.ShowFloating(grab.Id), grab.Id);
        }

        var lifting = !grab.WasFloating && !grab.Lifted;
        if (lifting)
        {
            if (!host.LiftTile(grab.Id, out var box))
                return [];

            grab.Lifted = true;
            grab.Start = box;
            grab.HotX = box.Width / 2;
            grab.HotY = box.Height / 2;
        }

        var next = new Rectangle(pointer.X - grab.HotX, pointer.Y - grab.HotY, grab.Start.Width, grab.Start.Height);
        floating.Set(grab.Id, next);
        var shown = host.ShowFloating(grab.Id);
        return lifting ? shown : Dragged(shown, grab.Id);
    }

    private static IReadOnlyList<Placement> Dragged(IReadOnlyList<Placement> placements, ClientId id) =>
        [.. placements.Where(placement => placement.Client == id)];

    private IReadOnlyList<Placement> End(IMouseDragHost host, Point pointer)
    {
        if (_grab is not Grab grab)
            return [];

        _grab = null;
        if (grab.Lifted && host.Floating.Contains(grab.Id))
            return host.DropTile(grab.Id, pointer);

        if (grab.WasFloating && host.Floating.Contains(grab.Id))
            return host.ShowFloating(grab.Id);

        return host.EndInPlace();
    }

    private sealed class Grab(ClientId id, bool resize, bool wasFloating, Point grab, Rectangle start, bool left, bool top, bool keepRatio)
    {
        public ClientId Id { get; } = id;

        public bool Resize { get; } = resize;

        public bool WasFloating { get; } = wasFloating;

        public Point Pressed { get; } = grab;

        public Point Last { get; set; } = grab;

        public Rectangle Start { get; set; } = start;

        public bool Left { get; } = left;

        public bool Top { get; } = top;

        public bool KeepRatio { get; } = keepRatio;

        public bool Lifted { get; set; }

        public int HotX { get; set; } = grab.X - start.X;

        public int HotY { get; set; } = grab.Y - start.Y;
    }
}

internal interface IMouseDragHost
{
    FloatingClients Floating { get; }

    SizeLimits FloatingLimits(ClientId id);

    bool TryDragBounds(ClientId id, out Rectangle bounds, out bool floating);

    bool LiftTile(ClientId id, out Rectangle box);

    IReadOnlyList<Placement> ResizeTile(ClientId id, int dx, int dy, (bool Left, bool Top) corner);

    IReadOnlyList<Placement> DropTile(ClientId id, Point pointer);

    IReadOnlyList<Placement> ShowFloating(ClientId id);

    IReadOnlyList<Placement> EndInPlace();
}

internal interface IDragOwner
{
    MouseDrag Drag { get; }
}

internal readonly record struct PointerDrag(bool Resize, PointerDragPhase Phase, Point Pointer, bool KeepRatio = false);

public enum PointerDragPhase
{
    Begin,
    Move,
    End,
}
