namespace Winland.Domain;

public sealed record Launch(string CommandLine, string? WorkingDirectory)
{
    public static IReadOnlyList<WindowRule> NoRules { get; } = [];

    public IReadOnlyList<WindowRule> Rules { get; init; } = NoRules;

    public WorkspaceId? InitialWorkspace { get; init; }
}
