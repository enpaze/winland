namespace Winland.Domain;

public closed record Input;

public closed record Event : Input;

public closed record Command : Input;

public sealed record KeyPressed(string Key) : Event
{
    public int ScanCode { get; init; }
}

public sealed record KeyReleased(string Key) : Event
{
    public int ScanCode { get; init; }
}

public sealed record PointerMoved(Point Position) : Event
{
    public long? TimeMs { get; init; }

    public WindowId? Under { get; init; }
}

public sealed record PointerButtonPressed(string Button) : Event
{
    public Point Position { get; init; }
}

public sealed record PointerButtonReleased(string Button) : Event
{
    public Point Position { get; init; }
}

public sealed record WheelTurned(string Key, long TimeMs) : Event;

public sealed record SwipeBegan(int Fingers) : Event;

public sealed record SwipeUpdated(int Fingers, double DeltaX, double DeltaY) : Event
{
    public long TimeMs { get; init; }

    public int Steps { get; init; } = 1;
}

public sealed record SwipeEnded(bool Cancelled = false) : Event
{
    public long TimeMs { get; init; }
}

public sealed record WindowClosed(WindowId Window) : Event
{
    public bool Cloaked { get; init; }
}

public sealed record WindowFocused(WindowId Window, FocusCause Cause = FocusCause.User) : Event
{
    public WindowId? Clicked { get; init; }
}

public enum FocusCause
{
    Requested,

    User,

    Activated,
}

public sealed record WindowMinimized(WindowId Window) : Event;

public sealed record WindowRestored(WindowId Window) : Event;

public sealed record WindowResized(WindowId Window, Rectangle Bounds) : Event;

public sealed record WindowDrifted(WindowId Window, Rectangle Bounds) : Event
{
    public bool TiledOnly { get; init; }
}

public sealed record WindowMoveStarted(WindowId Window, Point Pointer) : Event;

public sealed record WindowMoveEnded(WindowId Window, Point Pointer, Rectangle? Bounds = null) : Event;

public sealed record WindowSizeStarted(WindowId Window, Point Pointer, Rectangle? Bounds = null) : Event;

public sealed record WindowSizing(WindowId Window, Point Pointer, Rectangle? Bounds = null) : Event;

public sealed record WindowSizeEnded(WindowId Window, Point Pointer, Rectangle? Bounds = null) : Event;

public sealed record WindowMaximizeRequested(WindowId Window) : Event;

public sealed record WindowFullscreenRequested(WindowId Window, bool Fullscreen) : Event;

public sealed record WindowTitleChanged(WindowId Window, string Title) : Event;

public sealed record WindowClassChanged(WindowId Window, string Class) : Event;

public sealed record WindowEnabledChanged(WindowId Window, bool Enabled) : Event;

public sealed record WindowFlashed(WindowId Window) : Event;

public sealed record WindowLimitsChanged(WindowId Window, Size? MinSize, Size? MaxSize) : Event;

public sealed record MonitorsChanged(IReadOnlyList<Monitor> Monitors) : Event;

public sealed record AccentChanged(BorderColor Color) : Event
{
    public HighContrast? Contrast { get; init; }

    public double TextScale { get; init; } = 1;
}

public sealed record RestoredAdopted : Event;

public sealed record ConfigLoaded(ConfigDocument Config) : Command;

public sealed record Configure(ConfigDocument Document) : Command;

public sealed record Dispatch(string Name, string Arguments = "") : Command;

public sealed record GroupBarScrolled(WindowId Shown, bool Forward) : Command;

public sealed record GroupTabClosed(WindowId Window) : Command;

public sealed record PointerGesture(string Dispatcher, Point Position, PointerDragPhase Phase, string Button) : Command
{
    public string Arguments { get; init; } = "";
}

public sealed record WindowAppeared(WindowId Window, Rectangle WorkArea) : Event
{
    public Point? Pointer { get; init; }

    public WindowTraits Traits { get; init; } = WindowTraits.Normal;

    public Rectangle Bounds { get; init; }

    public long ProcessId { get; init; }

    public IReadOnlyList<int> AncestorProcessIds { get; init; } = [];

    public WindowId? Owner { get; init; }

    public bool Opened { get; init; }

    public bool Foreground { get; init; }

    public bool Dragging { get; init; }

    public bool Disabled { get; init; }

    public bool ShownMaximized { get; init; }

    public string Executable { get; init; } = "";
}
