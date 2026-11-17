namespace Winland.Domain;

public sealed record WorkspaceRule
{
    public static IReadOnlyList<WorkspaceRule> None { get; } = [];

    public string WorkspaceString { get; init; } = "";

    public string Monitor { get; init; } = "";

    public bool? Default { get; init; }

    public bool? Persistent { get; init; }

    public EdgeInsets? GapsIn { get; init; }

    public EdgeInsets? GapsOut { get; init; }

    public string? Layout { get; init; }

    internal WorkspaceRule Merge(WorkspaceRule other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return this with
        {
            Monitor = other.Monitor.Length > 0 ? other.Monitor : Monitor,
            Default = other.Default ?? Default,
            Persistent = other.Persistent ?? Persistent,
            GapsIn = other.GapsIn ?? GapsIn,
            GapsOut = other.GapsOut ?? GapsOut,
            Layout = other.Layout ?? Layout,
        };
    }
}
