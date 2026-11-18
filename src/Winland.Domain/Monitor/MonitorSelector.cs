using System.Globalization;

namespace Winland.Domain;

internal static class MonitorSelector
{
    public static MonitorTarget? Resolve(string? text, IReadOnlyList<MonitorTarget> monitors, MonitorTarget? current)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (string.IsNullOrWhiteSpace(text) || monitors.Count == 0)
            return null;

        text = text.Trim();
        if (Sides.TryParse(text, out var direction))
            return current is MonitorTarget origin ? InDirection(monitors, origin, direction) : null;

        if (text.Equals("next", StringComparison.OrdinalIgnoreCase))
            return Step(monitors, current, 1);

        if (text.Equals("prev", StringComparison.OrdinalIgnoreCase))
            return Step(monitors, current, -1);

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            return ById(monitors, id);

        if (text.StartsWith("desc:", StringComparison.OrdinalIgnoreCase))
        {
            var description = text[5..].Trim();
            foreach (var monitor in monitors)
            {
                if (monitor.Description.StartsWith(description, StringComparison.Ordinal))
                    return monitor;
            }

            return null;
        }

        foreach (var monitor in monitors)
        {
            if (monitor.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
                return monitor;
        }

        return null;
    }

    public static MonitorTarget? InDirection(IReadOnlyList<MonitorTarget> monitors, MonitorTarget origin, Side direction)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var best = -1.0;
        MonitorTarget? chosen = null;
        foreach (var monitor in monitors)
        {
            if (monitor.Id == origin.Id || !Touches(origin, monitor, direction))
                continue;

            var length = direction is Side.Left or Side.Right
                ? Shared(origin.Y, origin.Height, monitor.Y, monitor.Height)
                : Shared(origin.X, origin.Width, monitor.X, monitor.Width);
            if (length <= best)
                continue;

            best = length;
            chosen = monitor;
        }

        return chosen;
    }

    public static MonitorTarget? At(IReadOnlyList<MonitorTarget> monitors, Point point)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        MonitorTarget? closest = null;
        var best = long.MaxValue;
        foreach (var monitor in monitors)
        {
            if (new Rectangle(monitor.X, monitor.Y, monitor.Width, monitor.Height).Contains(point))
                return monitor;

            var distance = DistanceSquared(monitor, point);
            if (distance >= best)
                continue;

            best = distance;
            closest = monitor;
        }

        return closest;
    }

    private static MonitorTarget Step(IReadOnlyList<MonitorTarget> monitors, MonitorTarget? current, int step)
    {
        var place = 0;
        for (var i = 0; i < monitors.Count; i++)
        {
            if (current is MonitorTarget origin && monitors[i].Id == origin.Id)
            {
                place = i;
                break;
            }
        }

        return monitors[(place + step + monitors.Count) % monitors.Count];
    }

    private static MonitorTarget? ById(IReadOnlyList<MonitorTarget> monitors, int id)
    {
        foreach (var monitor in monitors)
        {
            if (monitor.Id.Value == id)
                return monitor;
        }

        return null;
    }

    private static bool Touches(MonitorTarget origin, MonitorTarget monitor, Side direction) => direction switch
    {
        Side.Left => Sticks(origin.X, monitor.X + monitor.Width),
        Side.Right => Sticks(origin.X + origin.Width, monitor.X),
        Side.Up => Sticks(origin.Y, monitor.Y + monitor.Height),
        Side.Down => Sticks(origin.Y + origin.Height, monitor.Y),
        _ => false,
    };

    private static bool Sticks(int edge, int other) => Math.Abs(edge - other) < 2;

    private static long DistanceSquared(MonitorTarget monitor, Point point)
    {
        var x = Math.Clamp(point.X, monitor.X, monitor.X + monitor.Width);
        var y = Math.Clamp(point.Y, monitor.Y, monitor.Y + monitor.Height);
        var dx = (long)x - point.X;
        var dy = (long)y - point.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double Shared(int start, int size, int otherStart, int otherSize) =>
        Math.Max(0, Math.Min(start + size, otherStart + otherSize) - Math.Max(start, otherStart));
}

internal readonly record struct MonitorTarget(
    MonitorId Id,
    string Name,
    string Description,
    int X,
    int Y,
    int Width,
    int Height);
