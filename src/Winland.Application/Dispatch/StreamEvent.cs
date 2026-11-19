namespace Winland.Application;

public closed record StreamEvent;

public sealed record ClientOpened(WindowId Window, WorkspaceId Workspace, string Class, string Title) : StreamEvent;

public sealed record ClientClosed(WindowId Window) : StreamEvent;

public sealed record FocusChanged(string Monitor, WorkspaceId Workspace, WindowId? Window) : StreamEvent;

public sealed record ClientUrgent(WindowId Window) : StreamEvent;

public sealed record ClientMoved(WindowId Window, WorkspaceId Workspace) : StreamEvent;

public sealed record ClientRetitled(WindowId Window, string Title) : StreamEvent;

public sealed record MonitorLayoutChanged : StreamEvent;

public sealed record FloatingChanged(WindowId Window, bool Floating) : StreamEvent;

public sealed record ClientMinimized(WindowId Window, bool Minimized) : StreamEvent;

public sealed record ClientPinned(WindowId Window, bool Pinned) : StreamEvent;

public sealed record WorkspaceActivated(WorkspaceId Workspace) : StreamEvent;

public sealed record WorkspaceCreated(WorkspaceId Workspace) : StreamEvent;

public sealed record WorkspaceDestroyed(WorkspaceId Workspace, long Id, string Name, string Monitor) : StreamEvent;

public sealed record WorkspaceMoved(WorkspaceId Workspace, string Monitor) : StreamEvent;

public sealed record ActiveSpecial(WorkspaceId? Workspace, string Monitor) : StreamEvent;

public sealed record ConfigWarned(int Line, string Message, string? Source = null) : StreamEvent;

public sealed record ConfigReloaded : StreamEvent;

public sealed record MonitorAdded(int Id, string Name, string Description) : StreamEvent;

public sealed record MonitorRemoved(int Id, string Name, string Description) : StreamEvent;

public sealed record FullscreenChanged(bool Entered, WindowId? Window = null) : StreamEvent;

public sealed record GroupToggled(WindowId Window, bool Created) : StreamEvent;

public sealed record ClientIntoGroup(WindowId Window) : StreamEvent;

public sealed record ClientOutOfGroup(WindowId Window) : StreamEvent;

