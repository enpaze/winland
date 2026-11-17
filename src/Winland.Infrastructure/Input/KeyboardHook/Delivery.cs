namespace Winland.Infrastructure;

public sealed partial class KeyboardHook : IDisposable
{
    internal (bool Passes, BindDecision Decision) Deliver(KeyEdge edge)
    {
        Input input = edge.Up
            ? new KeyReleased(edge.Name) { ScanCode = edge.Xkb }
            : new KeyPressed(edge.Name) { ScanCode = edge.Xkb };

        var draggedByKey = Drag.Current is BindDrag.Active held && held.Button.Equals(edge.Name, StringComparison.OrdinalIgnoreCase);
        if (draggedByKey && !edge.Up)
            return (false, new BindDecision(true, null));
        if (draggedByKey && Drag.Release(edge.Name) is BindDrag.Active dropped)
            PostGesture(dropped.Dispatcher, dropped.Arguments, CursorPoint(), PointerDragPhase.End, edge.Name);

        var decision = _core.Decide(input, _session);

        var keyDrag = edge.Up ? null : KeyDrag(decision);
        if (keyDrag is not null && Drag.Current is null && MouseHook.OnlyDrag(decision) && !_core.HasDragTarget(CursorPoint()))
        {
            _session.ForgetCaught(edge.Name);
            return (true, decision);
        }

        if (keyDrag is BindDispatch drag)
        {
            if (decision.Mask)
                Mask();

            if (Drag.Begin(edge.Name, drag.Dispatcher, drag.Arguments))
                PostGesture(drag.Dispatcher, drag.Arguments, CursorPoint(), PointerDragPhase.Begin, edge.Name);

            foreach (var dispatch in decision.Dispatches)
            {
                if (dispatch.Dispatcher.Length > 0 && !dispatch.Drag)
                    PostDispatch(dispatch.Dispatcher, dispatch.Arguments);
            }

            return (false, decision);
        }

        Enqueue(decision);
        if (draggedByKey)
            return (false, decision);

        if (decision.Mask)
            Mask();

        return (!decision.Consumed, decision);
    }
}
