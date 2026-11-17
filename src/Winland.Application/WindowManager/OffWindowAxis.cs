namespace Winland.Application;

public readonly record struct OffWindowScroll(WindowId Window, Point Inside);

public sealed partial class WindowManager
{
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
