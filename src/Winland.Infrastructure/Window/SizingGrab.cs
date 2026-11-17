namespace Winland.Infrastructure;

internal enum DragKind
{
    Unclear,
    Move,
    Size,
}

internal static class SizingGrab
{
    private const int HitNowhere = 0;
    private const int HitClient = 1;
    private const int HitSizeBox = 4;
    private const int HitLeft = 10;
    private const int HitBottomRight = 17;

    internal static DragKind Classify(int? hit)
    {
        if (hit is not int code || code is HitNowhere or HitClient)
            return DragKind.Unclear;

        return code == HitSizeBox || code is >= HitLeft and <= HitBottomRight ? DragKind.Size : DragKind.Move;
    }

    internal static DragKind Resolve(Rectangle start, Rectangle now)
    {
        if (now.Width != start.Width || now.Height != start.Height)
            return DragKind.Size;

        return now.X != start.X || now.Y != start.Y ? DragKind.Move : DragKind.Unclear;
    }
}
