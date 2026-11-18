namespace Winland.Application;

public sealed partial class WindowManager
{
    public bool Manages(WindowId window)
    {
        lock (_gate)
            return Find(window) is not null;
    }

    private readonly HashSet<WindowId> _shownMaximized = [];

    private Outcome AppearMaybeMaximized(WindowAppeared appeared)
    {
        var outcome = Appear(appeared);
        if (appeared.ShownMaximized && Find(appeared.Window) is not null)
            _shownMaximized.Add(appeared.Window);

        return outcome;
    }

    private Outcome MaximizeRequest(WindowId window)
    {
        if (WorkspaceContaining(window) is not LiveWorkspace desk || desk.ClientOf(window) is not Client client)
            return View([]);

        if (_shownMaximized.Remove(window))
        {
            return _workspaces.Shown.IsShown(desk.Id) && !client.Minimized && desk.Layout.TryBounds(window, out var slot)
                ? View([new Placement(client.Id, PlacedBounds(client, desk, slot))])
                : View([]);
        }

        var next = client.Fullscreen == FullscreenMode.Maximized ? FullscreenMode.None : FullscreenMode.Maximized;
        return CommitFullscreen(window, next);
    }

    private WindowId? _movedItself;

    private readonly Dictionary<WindowId, (long Since, int Count)> _putBacks = [];

    private Outcome FullscreenRequest(WindowFullscreenRequested requested)
    {
        if (Find(requested.Window) is not Client client)
            return View([]);

        if (requested.Fullscreen == (client.Fullscreen == FullscreenMode.Fullscreen))
            return View([]);

        _movedItself = requested.Window;
        return CommitFullscreen(requested.Window, requested.Fullscreen ? FullscreenMode.Fullscreen : FullscreenMode.None);
    }

    private Outcome PutBack(WindowDrifted drifted)
    {
        if (WorkspaceContaining(drifted.Window) is not LiveWorkspace desk
            || desk.ClientOf(drifted.Window) is not { Minimized: false } client
            || _interaction?.Window == drifted.Window
            || !_workspaces.Shown.IsShown(desk.Id)
            || !desk.Layout.TryBounds(drifted.Window, out var slot))
            return View([]);

        if (drifted.TiledOnly && client.Floating)
            return client.Fullscreen == FullscreenMode.None ? FollowNativeFloating(drifted.Window, drifted.Bounds) : View([]);

        var bounds = PlacedBounds(client, desk, slot);
        if (bounds == drifted.Bounds)
            return View([]);

        if (!client.Floating)
        {
            var now = Clock();
            var recent = _putBacks.GetValueOrDefault(drifted.Window);
            if (now - recent.Since >= 1000)
                recent = (now, 0);
            if (recent.Count >= 3)
            {
                bounds = new Rectangle(bounds.X + ((bounds.Width - drifted.Bounds.Width) / 2),
                    bounds.Y + ((bounds.Height - drifted.Bounds.Height) / 2), drifted.Bounds.Width, drifted.Bounds.Height);
            }
            else
            {
                _putBacks[drifted.Window] = (recent.Since, recent.Count + 1);
            }
        }

        _movedItself = drifted.Window;
        return bounds == drifted.Bounds ? View([]) : View([new Placement(client.Id, bounds)]);
    }
}
