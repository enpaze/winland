namespace Winland.Infrastructure;

internal enum Delivery
{
    Wait,

    Post,

    PostKeepingSize,
}

internal enum ShowAs
{
    Hide,
    Show,
    ShowMinimized,
}

internal interface IDesktop : IWindowStyles, IWindowFrame, IDwmSurface
{
    bool Exists(WindowId window);

    bool Shown(WindowId window);

    bool Minimized(WindowId window);

    bool Hung(WindowId window);

    bool InMoveSize(WindowId window);

    int Dpi(WindowId window);

    bool Repainting(WindowId window);

    bool TryOuter(WindowId window, out Rectangle outer);

    bool TryVisible(WindowId window, out Rectangle visible);

    void SetAttribute(WindowId window, int attribute, uint value);

    void SetBounds(WindowId window, Rectangle outer, Delivery delivery);

    void Show(WindowId window, ShowAs show);

    void ShowAsync(WindowId window, ShowAs show);

    void Raise(WindowId window, bool top);

    void RaiseAsync(WindowId window, bool top);

    void Under(WindowId window, WindowId above);

    bool Painted(WindowId window);

    bool HoldOnTop(WindowId window);

    void LetGo(WindowId window);

    bool Foreground(WindowId window);

    bool IsForeground(WindowId window);

    void ForegroundDesktop();

    void Close(WindowId window);

    void Pace();

    IStandInSurface? OpenStandIn(WindowId window);

    void StopStandIns();
}

internal enum StandInSlot
{
    Picture,
    Right,
    Bottom,
}

internal interface IStandInSurface
{
    nint Handle { get; }

    Size? PictureSize();

    void Place(Rectangle bounds);

    void Show(StandInSlot slot, StandInPiece? piece, byte alpha);

    void SetAlpha(byte alpha);

    void Hide();

    void Close();
}
