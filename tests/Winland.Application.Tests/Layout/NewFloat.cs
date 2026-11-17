namespace Winland.Application.Tests;

internal static class NewFloat
{
    public static Rectangle Centered(Rectangle area, Rectangle bounds) =>
        new(area.X + ((area.Width - bounds.Width) / 2), area.Y + ((area.Height - bounds.Height) / 2), bounds.Width, bounds.Height);
}
