using System.Globalization;
using static Winland.Domain.OrdinalText;

namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome ApplyGesture(PointerGesture gesture)
    {
        switch (gesture.Phase)
        {
            case PointerDragPhase.Begin:
                var dispatch = new BindDispatch(gesture.Dispatcher, gesture.Arguments);
                if (_interaction is not null || (TabUnder(dispatch, gesture.Position) ?? DragTarget(gesture.Position)) is not WindowId target)
                    return View([], consumed: true);

                var tabbed = TakeTabOut(dispatch, gesture.Position, out var window);
                window ??= target;
                var begun = BeginGesture(window.Value, dispatch, gesture.Button, gesture.Position);
                var view = View(begun, consumed: true, focus: window);
                return tabbed is null ? view : tabbed.Then(view);
            case PointerDragPhase.Move when _interaction is Gesture held:
                return MoveGesture(held, gesture.Position);
            case PointerDragPhase.End when _interaction is Gesture held && held.Button.Equals(gesture.Button, StringComparison.OrdinalIgnoreCase):
                return EndGesture(held, gesture.Position);
            default:
                return View([]);
        }
    }

    private IReadOnlyList<Placement> BeginGesture(WindowId window, BindDispatch drag, string button, Point pointer)
    {
        var desk = LayoutWorkspace(window);
        Give(window, desk);
        var gesture = new Gesture(window, button, IsResize(drag.Dispatcher), pointer)
        {
            Pointer = pointer,
            Cursor = DragCursor(desk, window, IsResize(drag.Dispatcher), pointer),
            WasTiled = LayoutWorkspace(window).ClientOf(window) is { Floating: false },
            KeepRatio = IsResize(drag.Dispatcher) && KeepsRatio(drag.Arguments),
        };

        _interaction = gesture;
        NoteDragCursor();
        return PickUp(window, gesture, pointer);
    }

    private IReadOnlyList<Placement> PickUp(WindowId window, Gesture gesture, Point pointer)
    {
        var desk = LayoutWorkspace(window);
        var placements = new List<Placement>();
        if (desk.ClientOf(window) is { Fullscreen: not FullscreenMode.None } client)
        {
            placements.AddRange(Restamp(desk, EndFullscreen(desk, window)));
            if (client.Floating && desk.Layout.TryBounds(window, out var restored))
                placements.AddRange(desk.Layout.PlaceFloating(window, restored with { X = pointer.X - (restored.Width / 2), Y = pointer.Y - (restored.Height / 2) }));
        }

        gesture.Cursor = DragCursor(desk, window, gesture.Resize, pointer);
        NoteDragCursor();
        placements.AddRange(desk.Layout.Drag(window, gesture.Step(PointerDragPhase.Begin, pointer)));
        return placements;
    }

    private Outcome MoveGesture(Gesture gesture, Point pointer)
    {
        gesture.Pointer = pointer;
        var layout = LayoutWorkspace(gesture.Window).Layout;
        var moved = layout.Drag(gesture.Window, gesture.Step(PointerDragPhase.Move, pointer));
        if (FollowDraggedMonitor(gesture) is { } crossed)
            return crossed with { Live = true };

        return View(moved, consumed: true) with { Live = true };
    }

    private Outcome EndGesture(Gesture gesture, Point pointer)
    {
        _interaction = null;
        NoteDragCursor();
        var drop = !gesture.Resize ? GroupDrop(gesture.Window, gesture.WasTiled, pointer) : null;
        if (drop is null && !gesture.Resize && DropOnMonitor(gesture, pointer) is { } carried)
            return carried;

        var ended = LayoutWorkspace(gesture.Window).Layout.Drag(gesture.Window, gesture.Step(PointerDragPhase.End, pointer));
        var followed = gesture.MovedWorkspace && !gesture.Resize && FocusDragged(gesture.Window);
        if (drop is { } into)
            return JoinDragged(gesture.Window, into, consumed: true);

        return View(ended, consumed: true, focus: followed ? gesture.Window : null);
    }

    private readonly record struct DropTarget(LiveWorkspace Desk, WindowGroup Group, int? Index);

    private Outcome? TakeTabOut(BindDispatch drag, Point pointer, out WindowId? member)
    {
        member = TabUnder(drag, pointer);
        if (member is not WindowId window || WorkspaceContaining(window) is not LiveWorkspace desk || desk.GroupOf(window) is not WindowGroup group)
        {
            member = null;
            return null;
        }

        return OutOfGroup(desk, group, window);
    }

    private WindowId? TabUnder(BindDispatch drag, Point pointer)
    {
        if (IsResize(drag.Dispatcher))
            return null;

        foreach (var bar in GroupBars(Snapshot()))
        {
            if (!BorderPointer.Contains(bar.Bounds, pointer))
                continue;

            var index = bar.TabAt(pointer.X - bar.Bounds.X, pointer.Y - bar.Bounds.Y);
            return index < 0 || bar.Tabs.Count < 2 ? null : bar.Tabs[index].Window;
        }

        return null;
    }

    private Outcome? FollowDraggedMonitor(Gesture gesture) => MoveWithMonitor(gesture, settle: false, pointer: default);

    private Outcome? DropOnMonitor(Gesture gesture, Point pointer) => MoveWithMonitor(gesture, settle: true, pointer);

    private Outcome? MoveWithMonitor(Gesture gesture, bool settle, Point pointer)
    {
        if (!LayoutWorkspace(gesture.Window).Layout.TryBounds(gesture.Window, out var box))
            return null;

        var moved = MoveWithMonitor(gesture.Window, box, gesture.WasTiled, settle, pointer);
        if (moved is not null)
            gesture.MovedWorkspace = true;
        return moved;
    }

    private Outcome? MoveWithMonitor(WindowId window, Rectangle box, bool wasTiled, bool settle, Point pointer)
    {
        var source = LayoutWorkspace(window);
        if (source.ClientOf(window) is not { Floating: true })
            return null;

        var middle = new Point(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        if (MonitorAt(middle) is not { } screen || (_workspaces.Shown.SpecialOn(screen.Id) ?? _workspaces.Shown.Shown(screen.Id)) is not WorkspaceId shown
            || _workspaces.Find(shown) is not LiveWorkspace target || target.Id == source.Id)
        {
            return null;
        }

        if (!settle)
            source.Layout.MouseDrag.HandTo(target.Layout.MouseDrag);

        if (!source.Take(window, out var taken))
        {
            if (!settle)
                target.Layout.MouseDrag.HandTo(source.Layout.MouseDrag);

            return null;
        }

        var tileBack = settle && wasTiled;
        var monitor = EnabledMonitor(target.Monitor)?.Bounds ?? default;
        target.Receive(window, taken with { Floating = !tileBack, Bounds = box }, WorkArea(target, _area), monitor, pointer: tileBack ? pointer : null);
        CarryGroup(source, target, window);
        FocusState.Remember(target, window);
        if (settle)
            Give(window, target);

        var placements = new List<Placement>();
        if (_workspaces.Shown.IsShown(source.Id))
            placements.AddRange(source.Layout.Visible());
        if (_workspaces.Shown.IsShown(target.Id))
            placements.AddRange(target.Layout.Visible());

        return View(placements, consumed: true, focus: settle ? window : null);
    }

    private bool FocusDragged(WindowId window)
    {
        if (WorkspaceContaining(window) is not LiveWorkspace target)
            return false;

        var focused = FocusedId();
        if (target.Monitor == focused && target.Id == _workspaces.Shown.ActiveOn(focused) && _workspaces.Shown.SpecialOn(focused) != target.Id)
            return false;

        Give(window, target);
        return true;
    }

    private static string DragCursor(LiveWorkspace desk, WindowId window, bool resize, Point pointer)
    {
        if (!resize)
            return "grabbing";

        var bounds = desk.Layout.TryBounds(window, out var found) ? found : default;
        var (left, top) = BorderPointer.GrabbedCorner(pointer, bounds);
        return BorderPointer.Cursor(BorderPointer.GrabbedSide(left, top))!;
    }

    private DropTarget? GroupDrop(WindowId dragged, bool wasTiled, Point pointer)
    {
        if (BarDrop(dragged, wasTiled, pointer, GroupBars(Snapshot())) is { } onBar)
            return onBar;

        if (WorkspaceContaining(dragged) is not LiveWorkspace from
            || WindowUnderExcept(pointer, dragged) is not WindowId under
            || WorkspaceContaining(under) is not LiveWorkspace home
            || home.GroupOf(under) is not WindowGroup group
            || (home.ClientOf(under) is { Floating: false } && !wasTiled)
            || !CanDragInto(from, dragged, group))
            return null;

        return new DropTarget(home, group, null);
    }

    private DropTarget? BarDrop(WindowId dragged, bool wasTiled, Point pointer, IReadOnlyList<GroupBar> bars)
    {
        if (WorkspaceContaining(dragged) is not LiveWorkspace from)
            return null;

        foreach (var bar in bars)
        {
            if (bar.Shown == dragged || !BorderPointer.Contains(bar.Bounds, pointer))
                continue;

            if (WorkspaceContaining(bar.Shown) is not LiveWorkspace there || there.GroupOf(bar.Shown) is not WindowGroup target)
                return null;

            var floatedIntoTiled = there.ClientOf(bar.Shown) is { Floating: false } && !wasTiled;
            if (!CanDragInto(from, dragged, target) || floatedIntoTiled)
                return null;

            return new DropTarget(there, target, Math.Clamp(bar.DropIndex(pointer), 0, target.Count));
        }

        return null;
    }

    private (WindowId Window, bool WasTiled, Point Pointer)? HeldForDrop() =>
        _interaction switch
        {
            Gesture { Resize: false } gesture => (gesture.Window, gesture.WasTiled, gesture.Pointer),
            NativeMove move => (move.Window, !move.Floating, move.Pointer),
            _ => null,
        };

    private static bool CanDragInto(LiveWorkspace from, WindowId dragged, WindowGroup target) =>
        from.GroupOf(dragged) != target;

    private Outcome JoinDragged(WindowId dragged, DropTarget drop, bool consumed)
    {
        var from = LayoutWorkspace(dragged);
        var (there, target, index) = (drop.Desk, drop.Group, drop.Index);
        var covered = there.Covered();
        var members = from.GroupOf(dragged) is WindowGroup own ? [.. own.Members] : new List<WindowId> { dragged };
        for (var i = 0; i < members.Count; i++)
        {
            if (!TakeForGroup(from, members[i], out var taken))
                continue;

            var position = index is int at ? at + i : target.Current + 1;
            position = Math.Clamp(position, 0, target.Count);
            there.Join(target, members[i], taken, position);
            there.Show(target, position);
        }

        if (from != there)
        {
            Restamp(from, []);
            DropIfEmpty(from.Id);
            Focus(there.Monitor);
        }

        Restamp(there, []);
        Give(dragged, there);
        (_extraEvents ??= []).Add(new ClientIntoGroup(dragged));
        var outcome = Present(there, covered, there.Layout.Visible(), focus: dragged) with { Consumed = consumed };
        return from != there && _workspaces.Find(from.Id) is LiveWorkspace left && _workspaces.Shown.IsShown(left.Id)
            ? outcome with { Placements = [.. left.Layout.Placed(), .. outcome.Placements] }
            : outcome;
    }

    private WindowId? WindowUnderExcept(Point point, WindowId except)
    {
        foreach (var special in new[] { true, false })
        {
            var targets = FocusTargets(point, special);
            targets.RemoveAll(target => target.Window == except);
            if (PointerFocus.At(targets, point) is WindowId found)
                return found;
        }

        return null;
    }

    public bool HasDragTarget(Point pointer)
    {
        if (!_gate.TryEnter())
            return true;

        try
        {
            return DragTarget(pointer) is not null;
        }
        finally
        {
            _gate.Exit();
        }
    }

    private static bool OnlyDrag(BindDecision match) => DispatchesOf(match).All(dispatch => dispatch.Dispatcher.Length == 0 || IsDragDispatcher(dispatch));

    private void ForgetDrag(WindowId window)
    {
        if (_interaction?.Window == window)
        {
            _interaction = null;
            _resizeCursor = null;
        }
        if (_released?.Window == window)
            _released = null;
    }

    private static bool KeepsRatio(string arguments) =>
        int.TryParse(arguments.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode) && mode == 1;

    private WindowId? DragTarget(Point? point)
    {
        if (point is not Point at)
            return null;

        if (WindowUnder(at) is WindowId inside)
            return inside;

        foreach (var special in new[] { true, false })
        {
            var grown = new List<PointerFocus.Target>();
            foreach (var target in FocusTargets(at, special))
            {
                var border = WorkspaceContaining(target.Window)?.Options.BorderFor(new ClientId(target.Window.Value)) ?? 0;
                var bounds = target.Bounds;
                grown.Add(target with { Bounds = new Rectangle(bounds.X - border, bounds.Y - border, bounds.Width + (2 * border), bounds.Height + (2 * border)) });
            }

            if (PointerFocus.At(grown, at) is WindowId edge)
                return edge;
        }

        return null;
    }

    private static bool TryDragHit(BindDecision match, out BindDispatch drag)
    {
        foreach (var dispatch in DispatchesOf(match))
        {
            if (IsDragDispatcher(dispatch))
            {
                drag = dispatch;
                return true;
            }
        }

        drag = default;
        return false;
    }

    private static bool IsDragDispatcher(BindDispatch dispatch) =>
        (Is(dispatch.Dispatcher, "movewindow") && dispatch.Arguments.Length == 0) || IsResize(dispatch.Dispatcher);

    private static bool IsResize(string dispatcher) =>
        Is(dispatcher, "resizewindow");

    private sealed class Gesture(WindowId window, string button, bool resize, Point origin)
        : PointerInteraction(window)
    {
        public string Button { get; } = button;

        public bool Resize { get; } = resize;

        public Point Origin { get; } = origin;

        public bool KeepRatio { get; init; }

        public bool WasTiled { get; init; }

        public Point Pointer { get; set; }

        public bool MovedWorkspace { get; set; }

        public string Cursor { get; set; } = "grabbing";

        public PointerDrag Step(PointerDragPhase phase, Point pointer) => new(Resize, phase, pointer, KeepRatio);
    }
}
