namespace Winland.Domain;

internal static class LaunchParser
{
    internal static LaunchParse Parse(string arguments)
    {
        var text = arguments.Trim();
        var warnings = new List<string>();
        IReadOnlyList<WindowRule> rules = Launch.NoRules;
        if (text.StartsWith('['))
        {
            var end = text.IndexOf(']');
            if (end < 0)
                return new LaunchParse(null, ["Unclosed rule block."]);

            if (!TryParseRules(text[1..end], out rules, warnings))
                return new LaunchParse(null, warnings);

            text = text[(end + 1)..].TrimStart();
        }

        if (text.Length == 0)
            return new LaunchParse(null, warnings);

        var launch = new Launch(text, null)
        {
            Rules = rules.Count == 0 ? Launch.NoRules : rules,
        };
        return new LaunchParse(launch, warnings);
    }

    internal static IReadOnlyList<Launch> FromConfig(ConfigDocument config, LaunchMoment moment, WorkspaceId? initialWorkspace = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var launches = new List<Launch>();
        foreach (var setting in config.Settings)
        {
            // exec runs at startup and on every reload. exec-once runs at startup only.
            var once = setting.Keyword == "exec-once";
            if (!once && setting.Keyword != "exec")
                continue;
            if (once && moment == LaunchMoment.Reload)
                continue;

            var parsed = Parse(setting.Value);
            if (parsed.Launch is not { } launch)
                continue;

            if (!once)
                launch = launch with { InitialWorkspace = initialWorkspace };
            launches.Add(launch);
        }

        return launches;
    }

    private static bool TryParseRules(string body, out IReadOnlyList<WindowRule> rules, List<string> warnings)
    {
        rules = Launch.NoRules;
        var parsed = new List<WindowRule>();
        var fatal = false;
        foreach (var piece in body.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (WindowRuleParser.TryParseBare(piece, out var rule, out var error) && rule is not null)
            {
                parsed.Add(rule);
                continue;
            }

            warnings.Add(error);
            if (!error.StartsWith("Unsupported window rule '", StringComparison.Ordinal))
                fatal = true;
        }

        if (parsed.Count > 0)
            rules = parsed;
        return !fatal;
    }

    internal enum LaunchMoment
    {
        Startup,
        Reload,
    }
}

internal readonly record struct LaunchParse(Launch? Launch, IReadOnlyList<string> Warnings);
