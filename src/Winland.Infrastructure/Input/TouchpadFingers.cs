namespace Winland.Infrastructure;

internal static class TouchpadFingers
{
    private const long Never = long.MinValue / 4;

    private static int _count;
    private static long _reportMs = Never;
    private static long _twoFingersMs = Never;

    internal static void Note(int count, long now)
    {
        Volatile.Write(ref _count, count);
        Volatile.Write(ref _reportMs, now);
        if (count >= 2)
            Volatile.Write(ref _twoFingersMs, now);
    }

    internal static bool Moves(long now) => TouchpadPointer.Moves(Volatile.Read(ref _count), now - Volatile.Read(ref _reportMs));

    internal static bool Touched(long now) => Moves(now);

    internal static bool Scrolls(long now, int delta) =>
        TouchpadPointer.Scrolls(Volatile.Read(ref _count), now - Volatile.Read(ref _reportMs), now - Volatile.Read(ref _twoFingersMs), delta);
}
