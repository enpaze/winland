namespace Winland.Application;

public static partial class CommandProtocol
{
    public const string PipeName = "winland";

    private const string Unknown = "error: unknown request";

    private static readonly Verb[] VerbList =
    [
        new("get windows", "", rest => Bare(rest, new WindowsRequest())),
        new("get window", "", rest => Bare(rest, new WindowRequest())),
        new("get workspaces", "", rest => Bare(rest, new WorkspacesRequest())),
        new("get workspace", "", rest => Bare(rest, new WorkspaceRequest())),
        new("get monitors", "[all]", rest => rest.Length == 0 ? new MonitorsRequest()
            : rest.Equals("all", StringComparison.OrdinalIgnoreCase) ? new MonitorsRequest(All: true)
            : null),
        new("get binds", "", rest => Bare(rest, new BindsRequest())),
        new("get errors", "", rest => Bare(rest, new ErrorsRequest())),
        new("get files", "", rest => Bare(rest, new FilesRequest())),
        new("inspect", "[selector]", rest => new InspectRequest(rest)),
        new("events", "", rest => Bare(rest, new EventsRequest())),
    ];

    private static readonly Dictionary<string, Verb> Verbs =
        VerbList.ToDictionary(verb => verb.Name, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> VerbNames => VerbList.Select(verb => verb.Name);

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
        if (name.Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            var (thing, more) = SplitOnce(rest);
            (name, rest) = (name + " " + thing, more);
        }

        if (Verbs.TryGetValue(name, out var verb))
        {
            return verb.Read(rest) is CommandRequest request
                ? new CommandParsed(request with { Flags = flags })
                : new CommandRejected(Unknown);
        }

        if (WindowManager.IsDispatcher(name))
            return new CommandParsed(new DispatchRequest(name, rest) { Flags = flags });

        return new CommandRejected(name.StartsWith("get ", StringComparison.OrdinalIgnoreCase)
            ? Unknown
            : DidYouMean.After("error: unknown command '" + name + "'", name, [.. WindowManager.DispatcherNames, "get", "inspect", "events"]));
    }

    private sealed record Verb(string Name, string Arguments, Func<string, CommandRequest?> Read);

    private static CommandRequest? Bare(string rest, CommandRequest request) => rest.Length == 0 ? request : null;

    private static (string First, string After) SplitOnce(string text)
    {
        var end = IndexOfWhitespace(text);
        return end < 0 ? (text, "") : (text[..end], text[(end + 1)..].TrimStart());
    }

    private static readonly string[] NoStrings = [];

    private static readonly WorkspaceJson NoSpecial = new(null, "", "", "");
}

public closed record CommandParse;

public sealed record CommandParsed(CommandRequest Request) : CommandParse;

public sealed record CommandRejected(string Error) : CommandParse;

public closed record CommandRequest
{
    public CommandFlags Flags { get; init; }
}

public sealed record DispatchRequest(string Name, string Arguments) : CommandRequest;

public sealed record WindowsRequest : CommandRequest;

public sealed record WindowRequest : CommandRequest;

public sealed record WorkspacesRequest : CommandRequest;

public sealed record WorkspaceRequest : CommandRequest;

public sealed record MonitorsRequest(bool All = false) : CommandRequest;

public sealed record InspectRequest(string Selector) : CommandRequest;

public sealed record EventsRequest : CommandRequest;

public sealed record BindsRequest : CommandRequest;

public sealed record ErrorsRequest : CommandRequest;

public sealed record FilesRequest : CommandRequest;

public readonly record struct CommandReply(string Text, Outcome Effect)
{
    public bool Stream { get; init; }
}
