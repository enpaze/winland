namespace Winland.Domain;

public static class BarShapes
{
    private const int Samples = 4;

    public static Corners Rounded(int index, int count, int radius)
    {
        if (radius <= 0)
            return Corners.None;

        if (count <= 1)
            return Corners.All;

        if (index == 0)
            return Corners.TopLeft | Corners.BottomLeft;

        return index == count - 1 ? Corners.TopRight | Corners.BottomRight : Corners.None;
    }

    public static double Coverage(int x, int y, Rectangle box, int radius, Corners corners)
    {
        if (x < box.X || y < box.Y || x >= box.X + box.Width || y >= box.Y + box.Height)
            return 0;

        var r = Math.Min(radius, Math.Min(box.Width, box.Height) / 2.0);
        if (r <= 0 || corners == Corners.None)
            return 1;

        var left = x < box.X + r;
        var right = x >= box.X + box.Width - r;
        var top = y < box.Y + r;
        var bottom = y >= box.Y + box.Height - r;
        if (!(left || right) || !(top || bottom))
            return 1;

        var corner = top ? (left ? Corners.TopLeft : Corners.TopRight) : (left ? Corners.BottomLeft : Corners.BottomRight);
        if ((corners & corner) == 0)
            return 1;

        var centerX = left ? box.X + r : box.X + box.Width - r;
        var centerY = top ? box.Y + r : box.Y + box.Height - r;
        var inside = 0;
        for (var sy = 0; sy < Samples; sy++)
        {
            for (var sx = 0; sx < Samples; sx++)
            {
                var px = x + ((sx + 0.5) / Samples);
                var py = y + ((sy + 0.5) / Samples);
                var dx = left ? Math.Max(0, centerX - px) : Math.Max(0, px - centerX);
                var dy = top ? Math.Max(0, centerY - py) : Math.Max(0, py - centerY);
                if ((dx * dx) + (dy * dy) <= r * r)
                    inside++;
            }
        }

        return inside / (double)(Samples * Samples);
    }
}
