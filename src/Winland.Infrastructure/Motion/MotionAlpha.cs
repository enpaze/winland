using System.Collections.Concurrent;

namespace Winland.Infrastructure;

internal static class MotionAlpha
{
    private static readonly ConcurrentDictionary<WindowId, byte> Levels = new();

    internal static bool TryGet(WindowId window, out byte alpha) => Levels.TryGetValue(window, out alpha);

    internal static void Set(WindowId window, byte? alpha)
    {
        var changed = alpha is byte level
            ? !Levels.TryGetValue(window, out var before) || before != level
            : Levels.ContainsKey(window);
        if (alpha is byte now)
            Levels[window] = now;
        else
            Levels.TryRemove(window, out _);

        if (changed)
            StandFrames.Follow(window);
    }
}
