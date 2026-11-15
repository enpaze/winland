namespace Winland.Application;

/// <summary>A wheel turn over the ring around a window: the window, and the point inside it nearest the pointer.</summary>
public readonly record struct OffWindowScroll(WindowId Window, Point Inside);

public sealed partial class WindowManager
{
    /// <summary>
    /// The window a wheel turn at <paramref name="pointer"/> is meant for when the pointer is not inside it but within
    /// its border, or null when the pointer is inside
    /// a window or over none. It runs on the mouse hook for every wheel turn, so it never waits: with a ring of no size
    /// or the core busy it answers null at once.
    /// </summary>
    public OffWindowScroll? OffWindowTarget(Point pointer)
    {
        if (!_gate.TryEnter())
            return null;

        try
        {
            var reach = Math.Max(0, ActiveOptions().BorderSize);
            if (reach <= 0 || !TryRingTarget(pointer, reach, out var window, out var bounds))
                return null;

            return new OffWindowScroll(window, BorderPointer.Clamp(bounds, pointer));
        }
        finally
        {
            _gate.Exit();
        }
    }

}
