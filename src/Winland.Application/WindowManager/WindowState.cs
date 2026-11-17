namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly WindowRegistry _windowState = new();

    private void TrackWindowState()
    {
        _windowState.Track(_props.Forget);
        _windowState.Track(_renamed);
        _windowState.Track(_awaitingMapFocus);
        _windowState.Track(ForgetFocusMarks);
        _windowState.Track(_restored);
        _windowState.Track(_restoredTiles);
        _windowState.Track(_mapPlaced);
        _windowState.Track(_pins.Unpin);
        _windowState.Track(_stacking);
        _windowState.Track(_cover.Forget);
        _windowState.Track(ForgetDrag);
        _windowState.Track(ForgetOwner);
        _windowState.Track(_shownMaximized);
        _windowState.Track(_putBacks);
    }
}
