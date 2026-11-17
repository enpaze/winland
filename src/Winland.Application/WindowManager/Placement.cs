namespace Winland.Application;

public sealed partial class WindowManager
{
    internal const long ReapplyMilliseconds = 2000;

    private const int DefaultFloatWidth = 640;
    private const int DefaultFloatHeight = 400;

    private readonly Dictionary<WindowId, MapPlaced> _mapPlaced = [];
    public bool AwaitsFirstResize(WindowId window)
    {
        lock (_gate)
            return _mapPlaced.TryGetValue(window, out var placed) && placed.Until >= Clock();
    }

    private LiveMonitor? RuleMonitor(string? text, LiveMonitor? opened)
    {
        if (text is null || _monitors.Count == 0)
            return opened;

        var selector = text.Trim();
        if (selector.Length == 0 || selector == "unset")
            return opened;

        var space = selector.LastIndexOf(' ');
        var silent = space >= 0 && selector[space..].Contains("silent", StringComparison.Ordinal);
        if (silent)
            selector = selector[..space].Trim();

        if (MonitorOf(MonitorSelector.Resolve(selector, Targets(), TargetOf(FocusedMonitor()))) is not LiveMonitor chosen)
            return opened;

        if (!silent && chosen.Id != FocusedId())
            Focus(chosen.Id);

        return chosen;
    }

    private Rectangle PlaceOnMap(WindowAppeared appeared, WindowDecision decision, LiveWorkspace desk, Rectangle area)
    {
        var placement = decision.Placement;
        var monitor = MonitorById(desk.Monitor) is { Arranged.Enabled: true } screen ? screen.Bounds : area;
        var desired = appeared.Bounds;
        var sized = desired.Width > 2 && desired.Height > 2;
        var width = sized || !decision.Floating ? desired.Width : DefaultFloatWidth;
        var height = sized || !decision.Floating ? desired.Height : DefaultFloatHeight;
        if (placement.Size is { } size)
        {
            var sizedWidth = size.X.Of(area.Width);
            var sizedHeight = size.Y.Of(area.Height);
            if (sizedWidth >= 1 && sizedHeight >= 1)
            {
                width = sizedWidth;
                height = sizedHeight;
            }
        }

        (width, height) = Clamp(width, height, decision.MinSize, decision.MaxSize);
        Rectangle placed;
        if (placement.Move is { } move)
        {
            var x = move.X.Percent ? area.X + move.X.Of(area.Width) : monitor.X + move.X.Of(0);
            var y = move.Y.Percent ? area.Y + move.Y.Of(area.Height) : monitor.Y + move.Y.Of(0);
            placed = new Rectangle(x, y, width, height);
        }
        else if (decision.Floating && appeared.Traits.PlacesItself && !placement.Center)
        {
            placed = desired with { Width = width, Height = height };
        }
        else if (decision.Floating && OwnerBounds(appeared) is Rectangle owner)
        {
            placed = new Rectangle(
                owner.X + ((owner.Width - width) / 2),
                owner.Y + ((owner.Height - height) / 2),
                width,
                height);
        }
        else
        {
            var floating = area;
            placed = new Rectangle(
                floating.X + ((floating.Width - width) / 2),
                floating.Y + ((floating.Height - height) / 2),
                width,
                height);
        }

        return decision.Floating ? LayoutOptions.NewFloatingOnScreen(area).Keep(placed) : placed;
    }

    private Rectangle? OwnerBounds(WindowAppeared appeared) =>
        appeared.Owner is WindowId owner && owner != appeared.Window && WorkspaceContaining(owner) is LiveWorkspace home
            && home.Layout.TryBounds(owner, out var bounds)
            ? bounds
            : null;

    private void NotePlaced(WindowId window, LiveWorkspace desk, WindowDecision decision, bool restored)
    {
        var now = Clock();
        if (!restored && desk.ClientOf(window) is { Floating: true, Fullscreen: FullscreenMode.None } && desk.Layout.TryBounds(window, out var bounds))
            _mapPlaced[window] = new MapPlaced(bounds, now + ReapplyMilliseconds);
    }

    private Outcome Reapply(WindowResized resized)
    {
        if (!_mapPlaced.TryGetValue(resized.Window, out var placed))
            return View([]);

        if (Clock() > placed.Until)
        {
            _mapPlaced.Remove(resized.Window);
            return View([]);
        }

        if (resized.Bounds.Width == placed.Bounds.Width && resized.Bounds.Height == placed.Bounds.Height)
            return View([]);

        _mapPlaced.Remove(resized.Window);
        if (WorkspaceContaining(resized.Window) is not LiveWorkspace desk || desk.ClientOf(resized.Window) is not { Floating: true })
            return View([]);

        var covered = desk.Covered();
        var placements = desk.Layout.PlaceFloating(resized.Window, placed.Bounds);
        return _workspaces.Shown.IsShown(desk.Id) ? Present(desk, covered, placements) : View([]);
    }

    private static int Pixels(double value) =>
        (int)Math.Round(Math.Clamp(value, int.MinValue / 2, int.MaxValue / 2), MidpointRounding.AwayFromZero);

    private static (int Width, int Height) Clamp(int width, int height, Size? min, Size? max)
    {
        if (max is { } hi)
        {
            if (hi.Width < int.MaxValue && width > hi.Width)
                width = hi.Width;
            if (hi.Height < int.MaxValue && height > hi.Height)
                height = hi.Height;
        }

        if (min is { } lo)
        {
            if (lo.Width > 0 && width < lo.Width)
                width = lo.Width;
            if (lo.Height > 0 && height < lo.Height)
                height = lo.Height;
        }

        return (width, height);
    }

    private readonly record struct MapPlaced(Rectangle Bounds, long Until);
}
