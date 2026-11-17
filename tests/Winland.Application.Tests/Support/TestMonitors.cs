namespace Winland.Application.Tests;

internal static class TestMonitors
{
    public static Monitor Display(string name, string description, Rectangle area) =>
        new(name, description, area.X, area.Y, area.Width, area.Height, 1, true, area);

    public static Monitor Display(
        string name,
        string description,
        int x,
        int y,
        int width,
        int height,
        double scale = 1,
        Rectangle? workArea = null,
        bool enabled = true) =>
        new(name, description, x, y, width, height, scale, enabled, workArea ?? new Rectangle(x, y, width, height));

    public static Monitor Display(string name, int x, int width, int height) =>
        new(name, name, x, 0, width, height, 1, true, new Rectangle(x, 0, width, height));
}
