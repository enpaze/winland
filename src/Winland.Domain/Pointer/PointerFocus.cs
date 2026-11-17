namespace Winland.Domain;

internal static class PointerFocus
{
    internal readonly record struct Target(WindowId Window, bool Floating, Rectangle Bounds);

    internal readonly record struct Decision(WindowId? Focus, WindowId? LastMouse, bool RememberMouse);

    internal static WindowId? At(IReadOnlyList<Target> targets, Point point)
    {
        ArgumentNullException.ThrowIfNull(targets);
        for (var i = targets.Count - 1; i >= 0; i--)
        {
            if (!targets[i].Floating)
                continue;

            if (Hits(targets[i], point))
                return targets[i].Window;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i].Floating)
                continue;

            if (Hits(targets[i], point))
                return targets[i].Window;
        }

        return null;
    }

    internal static Decision Choose(
        bool enabled,
        double traveled,
        WindowId? under,
        WindowId? keyboard,
        WindowId? lastMouse)
    {
        if (!enabled || under is null)
            return new Decision(null, lastMouse, false);

        if (lastMouse == under && keyboard == under)
            return new Decision(null, lastMouse, false);

        if (keyboard != under && traveled > 0)
            return new Decision(under, under, true);

        return new Decision(null, under, true);
    }

    private static bool Hits(Target target, Point point)
    {
        var bounds = target.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        return point.X >= bounds.X && point.Y >= bounds.Y && point.X < bounds.X + bounds.Width && point.Y < bounds.Y + bounds.Height;
    }
}
