using Windows.Win32;

namespace Winland.Infrastructure;

internal sealed class Win32Cursor : ICursorSurface
{
    private bool _hidden;

    public void Warp(Point position) => _ = PInvoke.SetCursorPos(position.X, position.Y);

    public void Hide()
    {
        if (_hidden)
            return;

        _hidden = true;
        Magnifier.HideCursor(true);
    }

    public void Show()
    {
        if (!_hidden)
            return;

        _hidden = false;
        Magnifier.HideCursor(false);
    }
}
