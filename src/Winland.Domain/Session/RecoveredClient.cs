namespace Winland.Domain;

public sealed record RecoveredClient(
    WindowId Window,
    WorkspaceId Workspace,
    MonitorId Monitor,
    bool Hidden,
    string Class,
    string Title,
    long ProcessId,
    bool Layered = false,
    bool? Floating = null,
    Rectangle? Bounds = null,
    bool? Pinned = null,
    FullscreenMode? Fullscreen = null)
{
    public uint? FrameStyle { get; init; }

    public bool Region { get; init; }

    public string MonitorIdentity { get; init; } = "";
}
