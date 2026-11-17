namespace Winland.Application;

public sealed partial class WindowManager
{
    private SwipePreview? _swipePreview;

    private Dictionary<WindowId, Rectangle>? _swipeLanding;

    private Outcome PreviewSwipe(WorkspaceSwipe swipe, WorkspaceId? neighbour)
    {
        var screen = MonitorById(swipe.Monitor);
        if (screen is null && _monitors.Count > 0)
            return View([]);

        var preview = _swipePreview ??= new SwipePreview(swipe.Begin);
        var span = (screen?.Bounds ?? _area).Width;
        var offset = -swipe.Delta / SwipeDistance * span;
        var motions = new List<Motion>();
        Offset(swipe.Begin, offset, preview, motions);

        var show = new List<WindowId>();
        var hide = new List<WindowId>();
        if (preview.Neighbour != neighbour)
        {
            hide.AddRange(preview.Revealed);
            foreach (var window in preview.Revealed)
                preview.Shown.Remove(window);
            preview.Revealed.Clear();
            preview.Neighbour = neighbour;
        }

        if (neighbour is WorkspaceId beside && swipe.Delta != 0)
        {
            var besideOffset = offset + (swipe.Delta < 0 ? -span : span);
            foreach (var window in Offset(beside, besideOffset, preview, motions))
            {
                if (preview.Revealed.Add(window))
                    show.Add(window);
            }
        }

        return View([], show, hide) with { Motions = motions };
    }

    private List<WindowId> Offset(WorkspaceId workspace, double offset, SwipePreview preview, List<Motion> motions)
    {
        var moved = new List<WindowId>();
        if (_workspaces.Find(workspace) is not { } desk)
            return moved;

        var shift = Pixels(offset);
        foreach (var client in desk.Layout.Clients)
        {
            if (client.Minimized || _pins.IsPinned(client.Window) || desk.IsWaiting(client.Window) || !desk.Layout.TryBounds(client.Window, out var slot))
                continue;

            var rest = PlacedBounds(client, desk, slot);
            var at = rest with { X = rest.X + shift };
            preview.Shown[client.Window] = at;
            motions.Add(new Motion(client.Window, at) { From = at, Warp = true, Rest = rest });
            moved.Add(client.Window);
        }

        return moved;
    }

    private Outcome ReturnSwipe(WorkspaceSwipe swipe)
    {
        if (TakePreview() is not { } preview)
            return View([]);

        var span = (MonitorById(swipe.Monitor)?.Bounds ?? _area).Width;
        var back = _config.Animations.Enabled && ReducedMotion?.Invoke() != true ? _config.Animations.TransitionTiming : null;
        var away = _config.Animations.Enabled && ReducedMotion?.Invoke() != true ? _config.Animations.TransitionTiming : null;
        var motions = new List<Motion>();
        var hide = new List<WindowId>();
        var side = Pixels(swipe.Delta < 0 ? -span : span);
        foreach (var (window, at) in preview.Shown)
        {
            if (Find(window) is not { } client || WorkspaceContaining(window) is not { } desk || !desk.Layout.TryBounds(window, out var slot))
                continue;

            var rest = PlacedBounds(client, desk, slot);
            if (!preview.Revealed.Contains(window))
            {
                motions.Add(new Motion(window, rest) { From = at, Warp = true, Move = back });
                continue;
            }

            var gone = rest with { X = rest.X + side };
            motions.Add(new Motion(window, gone) { From = at, Warp = true, Move = away, End = MotionEnd.Hide, Rest = rest });
            hide.Add(window);
        }

        return View([], hide: hide) with { Motions = motions };
    }

    private void LandSwipe() => _swipeLanding = TakePreview()?.Shown;

    private SwipePreview? TakePreview()
    {
        var preview = _swipePreview;
        _swipePreview = null;
        return preview;
    }

    private IReadOnlyList<Motion> FromSwipe(IReadOnlyList<Motion> motions)
    {
        var landing = _swipeLanding;
        _swipeLanding = null;
        if (landing is null)
            return motions;

        var started = new Motion[motions.Count];
        for (var i = 0; i < motions.Count; i++)
        {
            var motion = motions[i];
            started[i] = landing.TryGetValue(motion.Window, out var at) && motion.Move is not null
                ? motion with { From = at, Warp = true }
                : motion;
        }

        return started;
    }

    private sealed class SwipePreview(WorkspaceId begin)
    {
        public WorkspaceId Begin { get; } = begin;

        public WorkspaceId? Neighbour { get; set; }

        public Dictionary<WindowId, Rectangle> Shown { get; } = [];

        public HashSet<WindowId> Revealed { get; } = [];
    }
}
