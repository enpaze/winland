using System.Runtime.InteropServices;

namespace Winland.Verify.Tests;

internal static partial class ScreenPixels
{
    private const int SourceCopy = 0x00CC0020;

    public static uint[] Capture(Box area)
    {
        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, area.Width, area.Height);
        var old = SelectObject(memory, bitmap);
        try
        {
            _ = BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, SourceCopy);
            var pixels = new uint[area.Width * area.Height];
            var header = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = area.Width,
                Height = -area.Height,
                Planes = 1,
                BitCount = 32,
            };
            _ = SelectObject(memory, old);
            _ = GetDIBits(memory, bitmap, 0, (uint)area.Height, pixels, ref header, 0);
            return pixels;
        }
        finally
        {
            _ = DeleteObject(bitmap);
            _ = DeleteDC(memory);
            _ = ReleaseDC(0, screen);
        }
    }

    public static (int Count, Box Bounds, uint Sample) Changed(uint[] baseline, uint[] now, Box area)
    {
        var count = 0;
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (var i = 0; i < now.Length; i++)
        {
            if (Near(baseline[i], now[i]))
                continue;

            count++;
            var x = i % area.Width;
            var y = i / area.Width;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x + 1);
            bottom = Math.Max(bottom, y + 1);
        }

        if (count == 0)
            return (0, default, 0);

        var bounds = new Box(area.Left + left, area.Top + top, area.Left + right, area.Top + bottom);
        var sample = now[((top + bottom) / 2 * area.Width) + ((left + right) / 2)];
        return (count, bounds, sample & 0xFFFFFF);
    }

    private static bool Near(uint a, uint b)
    {
        for (var shift = 0; shift < 24; shift += 8)
        {
            if (Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF)) > 24)
                return false;
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint window, nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint dc, int width, int height);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint item);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, int operation);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] uint[] bits, ref BitmapInfoHeader info, uint usage);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint item);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);
}
