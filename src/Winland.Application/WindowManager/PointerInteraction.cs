namespace Winland.Application;

public sealed partial class WindowManager
{
    private PointerInteraction? _interaction;

    private abstract class PointerInteraction(WindowId window)
    {
        public WindowId Window { get; } = window;
    }
}
