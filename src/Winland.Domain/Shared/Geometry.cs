namespace Winland.Domain;

public readonly record struct Point(int X, int Y);

public readonly record struct Size(int Width, int Height);

public readonly record struct Rectangle(int X, int Y, int Width, int Height)
{
    public bool Contains(Point point) =>
        point.X >= X
        && point.Y >= Y
        && point.X < X + Width
        && point.Y < Y + Height;

    public double DistanceSquared(Point point)
    {
        var dx = point.X < X ? X - point.X : point.X >= X + Width ? point.X - (X + Width) : 0;
        var dy = point.Y < Y ? Y - point.Y : point.Y >= Y + Height ? point.Y - (Y + Height) : 0;
        return (dx * (double)dx) + (dy * (double)dy);
    }
}

internal enum Side
{
    Left,
    Right,
    Up,
    Down,
}

internal static class Sides
{
    private static readonly (string Word, Side Side)[] Words =
    [
        ("l", Side.Left),
        ("left", Side.Left),
        ("r", Side.Right),
        ("right", Side.Right),
        ("u", Side.Up),
        ("up", Side.Up),
        ("d", Side.Down),
        ("down", Side.Down),
    ];

    public static bool TryParse(string text, out Side side)
    {
        foreach (var (word, parsed) in Words)
        {
            if (text.Equals(word, StringComparison.OrdinalIgnoreCase))
            {
                side = parsed;
                return true;
            }
        }

        side = Side.Left;
        return false;
    }

    public static Point Focal(Rectangle box, Side direction) => direction switch
    {
        Side.Left => new Point(box.X - 1, box.Y + (box.Height / 2)),
        Side.Right => new Point(box.X + box.Width + 1, box.Y + (box.Height / 2)),
        Side.Up => new Point(box.X + (box.Width / 2), box.Y - 1),
        Side.Down => new Point(box.X + (box.Width / 2), box.Y + box.Height + 1),
        _ => new Point(box.X, box.Y),
    };
}
