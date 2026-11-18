namespace Winland.Application;

public static partial class CommandProtocol
{
    public const string PipeName = "winland";

    private const string Unknown = "error: unknown request";

    private static readonly Verb[] VerbList =
    [
        new("dispatch", "<name> [args]", rest => Dispatch(rest)),
        new("clients", "", rest => Bare(rest, new ClientsRequest())),
        new("workspaces", "", rest => Bare(rest, new WorkspacesRequest())),
        new("monitors", "[all]", rest => rest.Length == 0 ? new MonitorsRequest()
            : rest.Equals("all", StringComparison.OrdinalIgnoreCase) ? new MonitorsRequest(All: true)
            : null),
        new("activeworkspace", "", rest => Bare(rest, new ActiveWorkspaceRequest())),
        new("activewindow", "", rest => Bare(rest, new ActiveWindowRequest())),
        new("inspect", "[selector]", rest => new InspectRequest(rest)),
        new("reload", "", rest => Bare(rest, new ReloadRequest())),
        new("events", "", rest => Bare(rest, new EventsRequest())),
        new("binds", "", rest => Bare(rest, new BindsRequest())),
        new("version", "", rest => Bare(rest, new VersionRequest())),
        new("configerrors", "", rest => Bare(rest, new ConfigErrorsRequest())),
        new("files", "", rest => Bare(rest, new FilesRequest())),
    ];

    private static readonly Dictionary<string, Verb> Verbs =
        VerbList.ToDictionary(verb => verb.Name, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> VerbForms =>
        VerbList.Select(verb => verb.Arguments.Length == 0 ? verb.Name : verb.Name + " " + verb.Arguments);

    public static CommandParse Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new CommandRejected(Unknown);

        var text = line.Trim();
        var flags = CommandFlagReader.Strip(ref text);
        if (text.Length == 0)
            return new CommandRejected(Unknown);

        var (name, rest) = SplitOnce(text);
        if (!Verbs.TryGetValue(name, out var verb))
            return new CommandRejected(Unknown);

        return verb.Read(rest) is CommandRequest request
            ? new CommandParsed(request with { Flags = flags })
            : new CommandRejected(rest.Length == 0 && name.Equals("dispatch", StringComparison.OrdinalIgnoreCase)
                ? "error: missing dispatcher"
                : Unknown);
    }

    private sealed record Verb(string Name, string Arguments, Func<string, CommandRequest?> Read);

    private static DispatchRequest? Dispatch(string rest)
    {
        if (rest.Length == 0)
            return null;

        var (name, arguments) = SplitOnce(rest);
        return new DispatchRequest(name, arguments);
    }

    private static CommandRequest? Bare(string rest, CommandRequest request) => rest.Length == 0 ? request : null;

    private static (string First, string After) SplitOnce(string text)
    {
        var end = IndexOfWhitespace(text);
        return end < 0 ? (text, "") : (text[..end], text[(end + 1)..].TrimStart());
    }

    private static readonly string[] NoStrings = [];

    private static readonly WorkspaceJson NoSpecial = new("", null, "", "", "");
}

public closed record CommandParse;

public sealed record CommandParsed(CommandRequest Request) : CommandParse;

public sealed record CommandRejected(string Error) : CommandParse;

public closed record CommandRequest
{
    public CommandFlags Flags { get; init; }
}

public sealed record DispatchRequest(string Name, string Arguments) : CommandRequest;

public sealed record ClientsRequest : CommandRequest;

public sealed record WorkspacesRequest : CommandRequest;


public sealed record MonitorsRequest(bool All = false) : CommandRequest;

public sealed record ActiveWorkspaceRequest : CommandRequest;

public sealed record ActiveWindowRequest : CommandRequest;

public sealed record InspectRequest(string Selector) : CommandRequest;

public sealed record EventsRequest : CommandRequest;

public sealed record ReloadRequest : CommandRequest;

public sealed record BindsRequest : CommandRequest;

public sealed record VersionRequest : CommandRequest;

public sealed record ConfigErrorsRequest : CommandRequest;

public sealed record FilesRequest : CommandRequest;

public readonly record struct CommandReply(string Text, Outcome Effect)
{
    public bool Stream { get; init; }
}
