namespace Winland.Domain;

internal static class LaunchParser
{
    internal static LaunchParse Parse(string arguments)
    {
        var text = arguments.Trim();
        if (text.StartsWith('['))
            return new LaunchParse(null, ["Unsupported rule block."]);

        if (text.Length == 0)
            return new LaunchParse(null, []);

        return new LaunchParse(new Launch(text, null), []);
    }

    internal static IReadOnlyList<Launch> FromConfig(ConfigDocument config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var launches = new List<Launch>();
        foreach (var setting in config.Settings)
        {
            if (setting.Keyword != "exec-once")
                continue;

            var parsed = Parse(setting.Value);
            if (parsed.Launch is not { } launch)
                continue;

            launches.Add(launch);
        }

        return launches;
    }

}

internal readonly record struct LaunchParse(Launch? Launch, IReadOnlyList<string> Warnings);
