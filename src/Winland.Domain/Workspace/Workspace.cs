namespace Winland.Domain;

public sealed record Workspace(WorkspaceId Id, MonitorId Monitor)
{
    public WindowId? LastWindow { get; init; }


    public string TiledLayout { get; init; } = "dwindle";
}
