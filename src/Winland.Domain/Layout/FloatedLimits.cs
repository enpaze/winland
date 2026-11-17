namespace Winland.Domain;

internal static class FloatedLimits
{
    public static Rectangle Centered(SizeLimits limits, Rectangle predicted, Rectangle area)
    {
        var size = limits.Clamp(new Rectangle(0, 0, predicted.Width, predicted.Height));
        return new Rectangle(
            area.X + ((area.Width - size.Width) / 2),
            area.Y + ((area.Height - size.Height) / 2),
            size.Width,
            size.Height);
    }
}
