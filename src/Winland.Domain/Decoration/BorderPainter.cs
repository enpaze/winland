namespace Winland.Domain;

public static class BorderPainter
{
    public static Rectangle[] Pieces(int boxWidth, int boxHeight, BorderRing ring)
    {
        ArgumentNullException.ThrowIfNull(ring);
        var corner = (int)Math.Ceiling(Math.Max(ring.OuterRadius, ring.Thickness + ring.Radius));
        corner = Math.Clamp(corner, ring.Thickness, Math.Max(ring.Thickness, boxHeight / 2));
        var side = Math.Max(0, boxHeight - (2 * corner));
        return
        [
            new Rectangle(0, 0, boxWidth, corner),
            new Rectangle(0, boxHeight - corner, boxWidth, corner),
            new Rectangle(0, corner, ring.Thickness, side),
            new Rectangle(boxWidth - ring.Thickness, corner, ring.Thickness, side),
        ];
    }

    public static void Paint(Span<uint> pixels, BorderRing ring, int boxWidth, int boxHeight, Rectangle piece)
    {
        ArgumentNullException.ThrowIfNull(ring);
        if (piece.Width <= 0 || piece.Height <= 0 || pixels.Length < piece.Width * piece.Height)
            return;

        for (var row = 0; row < piece.Height; row++)
        {
            for (var column = 0; column < piece.Width; column++)
            {
                var x = piece.X + column;
                var y = piece.Y + row;
                var cover = Coverage(x, y, boxWidth, boxHeight, ring);
                var index = (row * piece.Width) + column;
                pixels[index] = cover <= 0 ? 0 : Premultiply(ring.Color, cover * ring.Alpha / 255.0);
            }
        }
    }

    public static double Coverage(int x, int y, int boxWidth, int boxHeight, BorderRing ring)
    {
        ArgumentNullException.ThrowIfNull(ring);
        var px = Math.Abs(x + 0.5 - (boxWidth / 2.0));
        var py = Math.Abs(y + 0.5 - (boxHeight / 2.0));
        var outer = Inside(px, py, boxWidth / 2.0, boxHeight / 2.0, ring.OuterRadius);
        var inner = Inside(px, py, (boxWidth / 2.0) - ring.Thickness, (boxHeight / 2.0) - ring.Thickness, ring.Radius);
        return Math.Clamp(outer * (1 - inner), 0, 1);
    }

    internal static double Inside(double px, double py, double halfWidth, double halfHeight, double radius)
    {
        if (halfWidth <= 0 || halfHeight <= 0)
            return 0;

        var r = Math.Clamp(radius, 0, Math.Min(halfWidth, halfHeight));
        double distance;
        if (r > 0 && px > halfWidth - r && py > halfHeight - r)
        {
            var dx = px - (halfWidth - r);
            var dy = py - (halfHeight - r);
            distance = Math.Sqrt((dx * dx) + (dy * dy)) - r;
        }
        else
        {
            distance = Math.Max(px - halfWidth, py - halfHeight);
        }

        return Math.Clamp(0.5 - distance, 0, 1);
    }

    internal static uint Premultiply(BorderColor color, double alpha)
    {
        var a = Math.Clamp(color.A * alpha, 0, 255);
        var factor = a / 255.0;
        return ((uint)Math.Round(a) << 24)
            | ((uint)Math.Round(color.R * factor) << 16)
            | ((uint)Math.Round(color.G * factor) << 8)
            | (uint)Math.Round(color.B * factor);
    }
}
