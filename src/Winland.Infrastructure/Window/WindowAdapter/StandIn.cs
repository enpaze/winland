namespace Winland.Infrastructure;

public sealed partial class WindowAdapter
{
    private readonly Lock _standInGate = new();
    private readonly Dictionary<WindowId, StandIn> _standIns = [];

    private const double RepaintMilliseconds = 50;

    private static bool Resizes(Rectangle bounds, Rectangle content) => content.Width != bounds.Width || content.Height != bounds.Height;

    private bool StandFor(WindowId window, Rectangle bounds, Rectangle content)
    {
        StandIn? standIn;
        lock (_standInGate)
            _standIns.TryGetValue(window, out standIn);

        if (standIn is not null && standIn.Content != content)
        {
            EndStandIn(window, bounds);
            standIn = null;
        }

        if (standIn is null)
        {
            if (!Resizes(bounds, content))
                return false;

            standIn = BeginStandIn(window, content);
            if (standIn is null)
                return false;
        }

        ShowStandIn(standIn, bounds);
        return true;
    }

    private StandIn? BeginStandIn(WindowId window, Rectangle content)
    {
        if (!_desktop.Shown(window) || !TryVisible(window, out var visible))
            return null;

        var showed = _opacity.Wanted(window) > 0;
        var given = showed
            ? content with { Width = Math.Max(content.Width, visible.Width), Height = Math.Max(content.Height, visible.Height) }
            : content;

        var insets = InsetsFor(window, _desktop.Dpi(window));
        var standIn = BeginThumbnailStandIn(window, content, given, insets, visible);
        if (standIn is null)
            return null;

        if (showed && TryOuter(window, out var before))
            standIn.Before = new Size(before.Width, before.Height);

        if (!_opacity.Veil(window))
        {
            EndStandIn(window, null);
            return null;
        }

        standIn.BorderOff = true;
        _desktop.SetAttribute(window, BorderColorAttribute, NoneBorderColor);

        SendQuick(window, insets.OuterFor(given), keepSize: false);
        return standIn;
    }

    private StandIn? BeginThumbnailStandIn(WindowId window, Rectangle content, Rectangle given, FrameInsets insets, Rectangle visible)
    {
        var surface = _desktop.OpenStandIn(window);
        if (surface is null)
            return null;

        var standIn = new StandIn(window, surface, content, new Size(given.Width, given.Height), insets);
        lock (_standInGate)
            _standIns[window] = standIn;

        ShowStandIn(standIn, visible);
        return standIn;
    }

    private void ShowStandIn(StandIn standIn, Rectangle bounds)
    {
        var surface = standIn.Surface;

        surface.Place(bounds);

        StandFrames.Set(standIn.Id, bounds, surface.Handle);
        BorderHost.Follow(standIn.Id);

        var painted = surface.PictureSize();

        if (painted is Size grown && standIn.Before is Size before && (grown.Width > before.Width || grown.Height > before.Height))
        {
            var now = _clock();
            standIn.Grew ??= now;
            if (now - standIn.Grew.Value < RepaintMilliseconds || _desktop.Repainting(standIn.Id))
                painted = new Size(Math.Min(grown.Width, before.Width), Math.Min(grown.Height, before.Height));
        }

        var map = StandInMap.For(bounds, standIn.Given, standIn.Insets, painted);
        var alpha = _opacity.Wanted(standIn.Id);

        surface.Show(StandInSlot.Picture, map.Picture, alpha);
        surface.Show(StandInSlot.Right, map.Right, alpha);
        surface.Show(StandInSlot.Bottom, map.Bottom, alpha);
    }

    private void StandInAlpha(WindowId window, byte alpha)
    {
        StandIn? standIn;
        lock (_standInGate)
            _standIns.TryGetValue(window, out standIn);

        standIn?.Surface.SetAlpha(alpha);
    }

    private void EndStandIn(WindowId window, Rectangle? at)
    {
        StandIn? standIn;
        lock (_standInGate)
        {
            if (!_standIns.Remove(window, out standIn))
                return;
        }

        if (at is Rectangle bounds)
            Move(window, InsetsFor(window, _desktop.Dpi(window)).OuterFor(bounds));

        StandFrames.Remove(window);
        BorderHost.Follow(window);
        _opacity.Unveil(window);
        if (standIn.BorderOff)
            _desktop.SetAttribute(window, BorderColorAttribute, _dwmPainted.ContainsKey(window) ? NoneBorderColor : DefaultBorderColor);

        standIn.Surface.Close();
    }

    private void HideStandIn(WindowId window)
    {
        StandIn? standIn;
        lock (_standInGate)
            _standIns.TryGetValue(window, out standIn);

        if (standIn is null)
        {
            EndStandIn(window, null);
            return;
        }

        standIn.Surface.Hide();

        StandFrames.Set(window, default, standIn.Surface.Handle);
        BorderHost.Follow(window);
    }

    private void EndStandIns()
    {
        WindowId[] windows;
        lock (_standInGate)
            windows = [.. _standIns.Keys];

        foreach (var window in windows)
            EndStandIn(window, null);
    }

    private sealed class StandIn(WindowId id, IStandInSurface surface, Rectangle content, Size given, FrameInsets insets)
    {
        public WindowId Id { get; } = id;

        public IStandInSurface Surface { get; } = surface;

        public Rectangle Content { get; } = content;

        public Size Given { get; } = given;

        public FrameInsets Insets { get; } = insets;

        public bool BorderOff { get; set; }

        public Size? Before { get; set; }

        public double? Grew { get; set; }
    }
}
