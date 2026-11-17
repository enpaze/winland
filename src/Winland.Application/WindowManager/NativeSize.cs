namespace Winland.Application;

public sealed partial class WindowManager
{
    private Outcome BeginNativeSize(WindowSizeStarted started)
    {
        if (_interaction is not null)
            return View([]);

        var window = started.Window;
        if (WorkspaceContaining(window) is not LiveWorkspace desk
            || desk.ClientOf(window) is not { Minimized: false } client)
            return View([]);

        _interaction = new NativeSize(window, client.Floating, started.Pointer, started.Bounds);
        if (client.Floating)
            return BeginNativeFloating(desk, client);
        _ = desk.Layout.Drag(window, new PointerDrag(true, PointerDragPhase.Begin, started.Pointer));
        return View([]);
    }

    private Outcome ContinueNativeSize(WindowSizing sizing)
    {
        if (_interaction is not NativeSize size || size.Window != sizing.Window)
            return View([]);

        if (size.Floating)
            return FollowNativeFloating(sizing.Window, sizing.Bounds) with { Live = true };

        var resized = LayoutWorkspace(sizing.Window).Layout.Drag(sizing.Window, new PointerDrag(true, PointerDragPhase.Move, size.Along(sizing.Pointer, sizing.Bounds)));
        var self = new ClientId(sizing.Window.Value);
        var placements = resized.Where(placement => placement.Client != self).ToList();

        if (resized.FirstOrDefault(placement => placement.Client == self) is { } own && size.Overruns(sizing.Pointer, sizing.Bounds, own.Bounds))
            placements.Add(own);

        return View(placements) with { Live = true };
    }

    private Outcome EndNativeSize(WindowSizeEnded ended)
    {
        if (_interaction is not NativeSize size || size.Window != ended.Window)
            return View([]);

        _interaction = null;
        if (size.Floating)
            return FollowNativeFloating(ended.Window, ended.Bounds);

        var layout = LayoutWorkspace(ended.Window).Layout;
        var covering = layout.Visible();
        var pointer = size.Along(ended.Pointer, ended.Bounds);
        var resized = layout.Drag(ended.Window, new PointerDrag(true, PointerDragPhase.Move, pointer));
        var released = layout.Drag(ended.Window, new PointerDrag(true, PointerDragPhase.End, pointer));

        return View(released.Count > 0 ? released : resized.Count > 0 ? resized : covering);
    }

    private sealed class NativeSize(WindowId window, bool floating, Point start, Rectangle? bounds) : PointerInteraction(window)
    {
        public bool Floating { get; } = floating;

        public Point Along(Point pointer, Rectangle? now)
        {
            if (bounds is not Rectangle before || now is not Rectangle after)
                return pointer;

            var across = after.X != before.X || after.Width != before.Width;
            var down = after.Y != before.Y || after.Height != before.Height;
            return new Point(across ? pointer.X : start.X, down ? pointer.Y : start.Y);
        }

        public bool Overruns(Point pointer, Rectangle? now, Rectangle tile)
        {
            if (bounds is not Rectangle before || now is not Rectangle after)
                return false;

            var dx = pointer.X - start.X;
            var dy = pointer.Y - start.Y;
            return (after.X != before.X && tile.X > before.X + dx + 1)
                || (after.Y != before.Y && tile.Y > before.Y + dy + 1)
                || (after.X + after.Width != before.X + before.Width && tile.X + tile.Width < before.X + before.Width + dx - 1)
                || (after.Y + after.Height != before.Y + before.Height && tile.Y + tile.Height < before.Y + before.Height + dy - 1);
        }
    }
}
