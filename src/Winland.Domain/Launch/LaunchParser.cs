namespace Winland.Domain;

internal static class LaunchParser
{
    internal const string UnsupportedBlock = "Unsupported rule block. Only [workspace N] is supported.";

    internal static LaunchParse Parse(string arguments)
    {
        var text = arguments.Trim();
        long? workspace = null;
        if (text.StartsWith('['))
        {
            var end = text.IndexOf(']', StringComparison.Ordinal);
            if (end < 0 || !TryWorkspace(text[1..end], out var number))
                return new LaunchParse(null, null, [UnsupportedBlock]);

            workspace = number;
            text = text[(end + 1)..].TrimStart();
        }

        if (text.Length == 0)
            return new LaunchParse(null, null, []);

        return new LaunchParse(new Launch(text, null), workspace, []);
    }

    internal static IReadOnlyList<Launch> FromConfig(ConfigDocument config, Func<long, WorkspaceId> workspace)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(workspace);
        var launches = new List<Launch>();
        foreach (var setting in config.Settings)
        {
            if (setting.Keyword != "exec-once")
                continue;

            var parsed = Parse(setting.Value);
            if (parsed.Launch is not { } launch)
                continue;

            launches.Add(parsed.Workspace is long number ? launch with { InitialWorkspace = workspace(number) } : launch);
        }

        return launches;
    }

    private static bool TryWorkspace(string block, out long number)
    {
        number = 0;
        return block.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is ["workspace", var value]
            && new WorkspaceId(value).TryGetNumber(out number);
    }
}

internal readonly record struct LaunchParse(Launch? Launch, long? Workspace, IReadOnlyList<string> Warnings);
