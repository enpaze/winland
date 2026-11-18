namespace Winland.Infrastructure;

internal readonly record struct WindowChrome(uint ExStyle, bool ColorKey, byte Alpha);

internal interface IWindowStyles
{
    bool TryRead(WindowId window, out WindowChrome chrome);

    bool TryWriteExStyle(WindowId window, uint exStyle);

    bool TrySetAlpha(WindowId window, byte alpha);
}
