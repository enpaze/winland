namespace Winland.Domain;

public readonly record struct Point(int X, int Y);

public readonly record struct Size(int Width, int Height);

public readonly record struct Rectangle(int X, int Y, int Width, int Height);

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
        ("t", Side.Up),
        ("top", Side.Up),
        ("d", Side.Down),
        ("down", Side.Down),
        ("b", Side.Down),
        ("bottom", Side.Down),
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
}
