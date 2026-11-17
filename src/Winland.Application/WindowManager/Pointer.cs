namespace Winland.Application;

public sealed partial class WindowManager
{
    private string? _resizeCursor;

    private Outcome RunKey(Input input)
    {
        var match = _keyState.Decide(_config.Binds, input);

        if (input is KeyPressed pressed && _interaction is null && KeyDrag(match) is { } drag && DragTarget(_pointer) is WindowId window)
        {
            var begun = BeginGesture(window, drag, pressed.Key, _pointer!.Value);
            var ran = RunBind(match) with { Focus = window };
            return begun.Count == 0 ? ran : ran with { Placements = begun };
        }

        if (input is KeyPressed && _interaction is null && KeyDrag(match) is not null && OnlyDrag(match))
            return RunBind(match) with { Consumed = false };

        if (input is KeyReleased released && _interaction is Gesture gesture && gesture.Button.Equals(released.Key, StringComparison.OrdinalIgnoreCase))
        {
            var ended = EndGesture(gesture, _pointer ?? gesture.Origin);
            NoteDragCursor();
            var after = RunBind(match);
            return ended.Then(after) with { Consumed = true };
        }

        return RunBind(match);
    }

    private static BindDispatch? KeyDrag(BindDecision match)
    {
        foreach (var dispatch in DispatchesOf(match))
        {
            if (dispatch.Drag && IsDragDispatcher(dispatch))
                return dispatch;
        }

        return null;
    }

    private Outcome PressPointer(PointerButtonPressed pressed)
    {
        var match = _keyState.Decide(_config.Binds, pressed);

        if (_interaction is null && TryDragHit(match, out var drag) && (TabUnder(drag, pressed.Position) ?? DragTarget(pressed.Position)) is WindowId target)
        {
            var tabbed = TakeTabOut(drag, pressed.Position, out var window);
            window ??= target;
            var begun = BeginGesture(window.Value, drag, pressed.Button, pressed.Position);
            var ran = RunBind(match) with { Focus = window };
            ran = tabbed is null ? ran : tabbed.Then(ran);
            return begun.Count == 0 ? ran : ran with { Placements = Outcome.Union(ran.Placements, begun, ran.State) };
        }

        NoteDragCursor();
        if (_interaction is null && TryDragHit(match, out _) && OnlyDrag(match))
            return RunBind(match) with { Consumed = false };

        return RunBind(match);
    }

    private Outcome ReleasePointer(PointerButtonReleased released)
    {
        if (released.Button == "mouse:272" && _interaction is NativeMove { Adopted: true } move)
            return EndNativeMove(new WindowMoveEnded(move.Window, released.Position, null));

        Outcome? ended = null;
        if (_interaction is Gesture gesture && gesture.Button.Equals(released.Button, StringComparison.OrdinalIgnoreCase))
            ended = EndGesture(gesture, released.Position);

        NoteDragCursor();
        var ran = RunBind(_keyState.Decide(_config.Binds, released));
        if (ended is null)
            return ran;

        return ended.Then(ran) with { Consumed = ended.Consumed || ran.Consumed };
    }

    private long? _lastMoveAt;
    private double _traveled;
    private WindowId? _lastMouse;

    private Outcome MovePointer(PointerMoved moved)
    {
        _ = _keyState.Decide(_config.Binds, moved);
        NoteDragCursor();
        if (_interaction is NativeMove move)
            move.Pointer = moved.Position;

        if (_interaction is Gesture gesture)
        {
            _lastMoveAt = When(moved.TimeMs);
            return MoveGesture(gesture, moved.Position);
        }

        return View([], focus: Follow(moved, Track(moved)));
    }

    private bool Glides(PointerMoved moved, Outcome last, out PointerAim aim)
    {
        _ = _keyState.Decide(_config.Binds, moved);
        NoteDragCursor();
        aim = Track(moved);
        if (aim.Monitor || aim.Focus is not null || _resizeCursor != last.ResizeCursor)
            return false;

        _pointer = moved.Position;
        return true;
    }

    private readonly record struct PointerAim(double Traveled, bool Monitor, WindowId? Focus);

    private PointerAim Track(PointerMoved moved)
    {
        var now = When(moved.TimeMs);
        if (_pointer is Point held && held == moved.Position)
        {
            _lastMoveAt = now;
            return default;
        }

        var traveled = Advance(moved.Position, now);

        if (_config.Input.FollowMouse && MonitorAt(moved.Position) is { } screen && screen.Id != FocusedId())
            return new PointerAim(traveled, true, null);

        return new PointerAim(traveled, false, Choose(moved, traveled));
    }

    private WindowId? Follow(PointerMoved moved, PointerAim aim)
    {
        if (!aim.Monitor)
            return TakePointerFocus(aim.Focus);

        FocusMonitorAt(moved.Position);
        return TakePointerFocus(Choose(moved, aim.Traveled));
    }

    private WindowId? Choose(PointerMoved moved, double traveled)
    {
        if (_keyState.ButtonsHeld)
            return null;

        var under = WindowUnder(moved.Position);
        var decision = PointerFocus.Choose(
            _config.Input.FollowMouse,
            traveled,
            under,
            _focus.Focused,
            _lastMouse);
        if (decision.RememberMouse)
            _lastMouse = decision.LastMouse;

        if (_focus.Focused is null && _focus.Unmanaged is not null && decision.Focus == _focus.BeforeUnmanaged)
            return null;

        if (moved.Under is WindowId over && decision.Focus is WindowId target && over != target && Find(over) is null)
            return null;

        return decision.Focus;
    }

    private WindowId? TakePointerFocus(WindowId? window)
    {
        if (window is not WindowId next || next == _focus.Focused || HasNoFocus(next))
            return null;

        if (WorkspaceContaining(next) is not LiveWorkspace desk || !_workspaces.Shown.IsShown(desk.Id))
            return null;

        Foreground(next, pointer: true);
        return _focus.Focused == next ? next : null;
    }

    private double Advance(Point position, long now)
    {
        var step = 0d;
        if (_pointer is Point previous)
            step = Distance(previous, position) / ScaleOf(position);

        var recent = _lastMoveAt is long seen && now >= seen && now - seen < 500;
        _lastMoveAt = now;
        if (_config.Input.FollowMouse && recent)
            _traveled += step;
        else
            _traveled = 0;

        return _traveled;
    }

    private void FocusMonitorAt(Point point)
    {
        if (_monitors.Count == 0)
            return;

        var screen = MonitorAt(point);
        if (screen is null || screen.Id == FocusedId())
            return;

        Focus(screen.Id);
    }

    private WindowId? WindowUnder(Point? point)
    {
        if (point is not Point at)
            return null;

        return PointerFocus.At(Targets(at, special: true), at)
            ?? PointerFocus.At(Targets(at, special: false), at);
    }

    private List<PointerFocus.Target> Targets(Point point, bool special)
    {
        LiveMonitor? screen = null;
        if (_monitors.Count > 0)
        {
            screen = MonitorAt(point);
            if (screen is null)
                return [];
        }

        var targets = new List<PointerFocus.Target>();
        foreach (var desk in _workspaces)
        {
            if (desk.Id.IsSpecial != special || !_workspaces.Shown.IsShown(desk.Id))
                continue;

            if (screen is not null && desk.Monitor != screen.Id)
                continue;

            var showing = desk.Showing();
            if (CoveringClient(desk, showing) is { } covering)
            {
                var slot = desk.Layout.TryBounds(covering.Window, out var found) ? found : covering.Bounds;
                targets.Add(new PointerFocus.Target(covering.Window, covering.Floating, PlacedBounds(covering, desk, slot)));
                continue;
            }

            foreach (var client in desk.Layout.Clients)
            {
                if (client.Minimized || !showing.Contains(client.Window) || HasNoFocus(client.Window))
                    continue;

                if (!desk.Layout.TryBounds(client.Window, out var bounds))
                    continue;

                targets.Add(new PointerFocus.Target(client.Window, client.Floating, bounds));
            }
        }

        var tiles = targets.Where(target => !target.Floating);
        var floats = targets.Where(target => target.Floating).OrderBy(target => _stacking.IndexOf(target.Window));
        return [.. tiles, .. floats];
    }

    private LiveMonitor? MonitorAt(Point point)
    {
        if (_monitors.Count == 0)
            return null;

        return Match(screen => screen.Covers(point));
    }

    private double ScaleOf(Point point)
    {
        var scale = MonitorAt(point)?.Arranged.Scale ?? 1;
        return scale is > 0 and < 1000 && double.IsFinite(scale) ? scale : 1;
    }

    private bool Floats(WindowId? window) =>
        window is WindowId id && Find(id) is { Floating: true };

    private long When(long? time) => time ?? Clock();

    private static double Distance(Point from, Point to)
    {
        var dx = (double)to.X - from.X;
        var dy = (double)to.Y - from.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private void NoteDragCursor() => _resizeCursor = (_interaction as Gesture)?.Cursor;

    private LiveWorkspace PointerWorkspace(Point pointer)
    {
        if (MonitorAt(pointer) is { } screen && (_workspaces.Shown.SpecialOn(screen.Id) ?? _workspaces.Shown.Shown(screen.Id)) is WorkspaceId shown && _workspaces.Find(shown) is LiveWorkspace desk)
            return desk;

        return CurrentWorkspace();
    }

    private bool TryRingTarget(Point pointer, int reach, out WindowId window, out Rectangle bounds)
    {
        window = default;
        bounds = default;
        if (reach <= 0)
            return false;

        var desk = PointerWorkspace(pointer);
        var showing = desk.Showing();
        var covering = CoveringClient(desk, showing);
        var clients = new List<Client>(desk.Layout.Clients.Count);
        foreach (var client in desk.Layout.Clients)
        {
            if (client.Minimized || !showing.Contains(client.Window))
                continue;

            if (covering is not null && (!client.Floating || client.Window == covering.Window || !_cover.AllowedOver(desk, client)))
                continue;

            clients.Add(client);
        }

        clients = [.. clients.Where(client => !client.Floating), .. clients.Where(client => client.Floating).OrderBy(client => _stacking.IndexOf(client.Window))];

        if (TryRing(desk, clients, pointer, reach, floatingOnly: true, out var inside, out window, out bounds))
            return true;

        if (inside)
            return false;

        return TryRing(desk, clients, pointer, reach, floatingOnly: false, out _, out window, out bounds);
    }

    private static bool TryRing(
        LiveWorkspace desk,
        IReadOnlyList<Client> clients,
        Point pointer,
        int reach,
        bool floatingOnly,
        out bool inside,
        out WindowId window,
        out Rectangle bounds)
    {
        inside = false;
        for (var i = clients.Count - 1; i >= 0; i--)
        {
            var client = clients[i];
            if (client.Minimized || client.Floating != floatingOnly || !desk.Layout.TryBounds(client.Window, out bounds))
                continue;

            if (BorderPointer.Contains(bounds, pointer))
            {
                inside = true;
                break;
            }

            if (!BorderPointer.InGrab(bounds, pointer, reach))
                continue;

            window = client.Window;
            return true;
        }

        window = default;
        bounds = default;
        return false;
    }

    private LayoutOptions ActiveOptions()
    {
        var screen = MonitorById(FocusedWorkspace().Monitor);
        if (screen is { Arranged.Enabled: true })
            return _config.Layout.ScaledBy(screen.Arranged.Scale);

        return _config.Layout;
    }

}
