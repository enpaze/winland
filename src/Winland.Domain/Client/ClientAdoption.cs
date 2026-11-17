namespace Winland.Domain;

internal static class ClientAdoption
{
    public static Client Adopted(
        ClientId id,
        WindowId window,
        WorkspaceId workspace,
        string windowClass,
        string title,
        bool floating,
        long processId,
        string? initialClass,
        string? initialTitle) =>
        new(
            id,
            window,
            Floating: floating,
            Workspace: workspace,
            Class: windowClass,
            Title: title,
            ProcessId: processId,
            InitialClass: initialClass ?? windowClass,
            InitialTitle: initialTitle ?? title);
}
