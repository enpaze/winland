namespace Winland.Infrastructure;

internal static class TrayBadge
{
    private const uint Blue = 0x2F6FED;
    private const uint White = 0xFFFFFF;

    internal static void Compose(Span<uint> pixels, int size)
    {
        var ink = new byte[size * size];
        int left = size, top = size, right = -1, bottom = -1;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = pixels[(y * size) + x];
                var level = (byte)Math.Max(pixel & 0xFF, Math.Max((pixel >> 8) & 0xFF, (pixel >> 16) & 0xFF));
                ink[(y * size) + x] = level;
                if (level == 0)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        var dx = right < 0 ? 0 : (int)Math.Round((size - left - right - 1) / 2.0, MidpointRounding.AwayFromZero);
        var dy = bottom < 0 ? 0 : (int)Math.Round((size - top - bottom - 1) / 2.0, MidpointRounding.AwayFromZero);
        var radius = size / 2.0;
        var rim = Math.Max(1.0, size / 14.0);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Math.Sqrt(Math.Pow(x + 0.5 - radius, 2) + Math.Pow(y + 0.5 - radius, 2));
                var disc = Coverage(radius - distance);
                if (disc <= 0)
                {
                    pixels[(y * size) + x] = 0;
                    continue;
                }

                var color = Mix(Blue, White, Coverage(distance - (radius - rim)));
                var (inkX, inkY) = (x - dx, y - dy);
                if (inkX >= 0 && inkX < size && inkY >= 0 && inkY < size)
                    color = Mix(color, White, ink[(inkY * size) + inkX] / 255.0);

                pixels[(y * size) + x] = ((uint)Math.Round(disc * 255) << 24) | color;
            }
        }
    }

    internal static bool Clear(uint pixel) => pixel >> 24 == 0;

    private static double Coverage(double inside) => Math.Clamp(inside + 0.5, 0, 1);

    private static uint Mix(uint from, uint to, double amount)
    {
        uint Channel(int shift)
        {
            var a = (from >> shift) & 0xFF;
            var b = (to >> shift) & 0xFF;
            return (uint)Math.Round(a + ((b - (double)a) * amount)) << shift;
        }

        return Channel(16) | Channel(8) | Channel(0);
    }
}
