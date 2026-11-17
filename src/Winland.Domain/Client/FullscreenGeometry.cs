namespace Winland.Domain;

public static class FullscreenGeometry
{
    public static Rectangle Cover(FullscreenMode mode, Rectangle workArea, Rectangle monitor)
    {
        if (mode == FullscreenMode.Maximized)
            return workArea;

        if (mode == FullscreenMode.Fullscreen)
            return monitor.Width > 0 && monitor.Height > 0 ? monitor : workArea;

        return default;
    }
}
