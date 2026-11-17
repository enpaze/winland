namespace Winland.Infrastructure;

public static class SessionEffects
{
    private static readonly CursorIdle Idle = new(NullCursor.Instance);

    private static int _installed;

    public static void InstallSystem()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
            return;

        Idle.Use(new Win32Cursor());
    }

    public static void Apply(Outcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        CursorShape.Show(outcome.ResizeCursor);

        if (outcome.Cursor is CursorBehavior behavior)
            Idle.Configure(behavior);

        if (outcome.Warp is WarpTo warp)
            Idle.Warp(warp.Position);
    }

    public static void NoteMove(Point position, PointerSource source = PointerSource.Mouse)
    {
        if (source == PointerSource.Touch)
            Idle.Touched();
        else if (source != PointerSource.Pen)
            Idle.Moved();
    }

    public static void NoteTouch() => Idle.Touched();

    public static void NoteKey() => Idle.KeyPressed();

    public static void RestoreCursors() => CursorShape.Restore();

    public static void Restore()
    {
        Magnifier.Stop();
        CursorShape.Reset();
        Idle.Reveal();
    }

    private sealed class NullCursor : ICursorSurface
    {
        public static readonly NullCursor Instance = new();

        public void Warp(Point position)
        {
        }

        public void Hide()
        {
        }

        public void Show()
        {
        }
    }
}
