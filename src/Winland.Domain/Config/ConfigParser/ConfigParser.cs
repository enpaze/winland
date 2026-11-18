namespace Winland.Domain;

public static partial class ConfigParser
{
    public static ConfigDocument Parse(
        string text,
        IConfigSource? source = null,
        KeyLayout? layout = null,
        string? path = null,
        IReadOnlyCollection<string>? dispatchers = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new Parser(source, layout ?? KeyLayout.UnitedStates, dispatchers).ParseRoot(text, path);
    }

    private sealed partial class Parser(IConfigSource? sourceReader, KeyLayout layout, IReadOnlyCollection<string>? dispatchers)
    {
        private static readonly HashSet<string> KeptHandlers = new(StringComparer.Ordinal)
        {
            "exec-once",
            "windowrule",
        };

        private static readonly string[] Keywords =
            ["source", "unbind", "bind", "gesture", .. KeptHandlers];

        private readonly List<Setting> _settings = [];
        private readonly List<BindDirective> _binds = [];
        private readonly List<TrackpadGesture> _gestures = [];
        private readonly List<ConfigWarning> _warnings = [];
        private readonly List<WindowRule> _windowRules = [];
        private readonly List<string> _sources = [];
        private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

        private readonly HashSet<string> _openSources = new(StringComparer.OrdinalIgnoreCase);
        private string? _source;
        private string? _rootFolder;

        public ConfigDocument ParseRoot(string text, string? path)
        {
            _source = null;
            if (path is not null && Path.IsPathFullyQualified(path))
            {
                _rootFolder = Path.GetDirectoryName(path);
                _openSources.Add(SourceKey(path));
            }

            ParseLines(text);
            return new ConfigDocument([.. _settings], [.. _binds], [.. _warnings])
            {
                WindowRules = [.. _windowRules],
                Sources = [.. _sources],
                Gestures = [.. _gestures],
                Path = path,
                KeyLayout = layout,
            };
        }

        private void ParseLines(string text)
        {
            foreach (var (lineNumber, raw) in LogicalLines(text))
                ParseLine(lineNumber, raw);
        }

        private List<(int Line, string Text)> LogicalLines(string text)
        {
            var physical = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            var lines = new List<(int, string)>(physical.Length);
            for (var i = 0; i < physical.Length; i++)
            {
                var start = i + 1;
                var current = physical[i];
                var dangling = false;
                while (current.Length > 0 && current[^1] == '\\')
                {
                    if (HasComment(current))
                    {
                        if (current.TrimStart()[0] != '#')
                            Warn(start, "A backslash at the end of a comment does not join the next line.");
                        break;
                    }

                    var stem = current[..^1].TrimEnd(' ', '\t');
                    if (i + 1 >= physical.Length)
                    {
                        Warn(start, "Line ends with a backslash.");
                        dangling = true;
                        break;
                    }

                    i++;
                    current = stem + physical[i];
                }

                if (!dangling)
                    lines.Add((start, current));
            }

            return lines;
        }

        private void ParseLine(int lineNumber, string raw)
        {
            var line = CutComment(raw);
            if (line.Length == 0)
                return;

            var equals = line.IndexOf('=');
            if (equals < 0 || line[..equals].Trim() is not { Length: > 0 } lhs)
            {
                Warn(lineNumber, "Malformed line.");
                return;
            }

            var rhs = line[(equals + 1)..].Trim();

            var definingVariable = lhs[0] == '$';
            if (!TryExpand(ref rhs) || (!definingVariable && !TryExpand(ref lhs)))
            {
                Warn(lineNumber, "Variable expansion did not finish.");
                return;
            }

            if (definingVariable)
            {
                if (lhs.Length == 1)
                    Warn(lineNumber, "Malformed line.");
                else
                    _variables[lhs[1..]] = rhs;
                return;
            }

            if (lhs == "source")
            {
                HandleSource(rhs, lineNumber);
                return;
            }

            if (lhs == "unbind")
            {
                HandleUnbind(rhs, lineNumber);
                return;
            }

            if (lhs.StartsWith("gesture", StringComparison.Ordinal) && !lhs.Contains(':'))
            {
                HandleGesture(lhs, rhs, lineNumber);
                return;
            }

            if (lhs == "bind")
            {
                HandleBind(rhs, lineNumber);
                return;
            }

            if (KeptHandlers.Contains(lhs))
            {
                _settings.Add(new Setting(lhs, rhs));
                if (lhs == "windowrule")
                    RememberRule(rhs, lineNumber);
                else if (IsLaunchKeyword(lhs))
                {
                    foreach (var warning in LaunchParser.Parse(rhs).Warnings)
                        Warn(lineNumber, warning);
                }

                return;
            }

            if (OptionTable.Find(lhs) is not IOption option)
            {
                Warn(lineNumber, $"Unsupported keyword '{lhs}'.{DidYouMean.For(lhs, OptionTable.Names.Concat(Keywords))}");
                return;
            }

            if (!option.Reads(rhs))
            {
                Warn(lineNumber, $"Invalid value '{rhs}' for {lhs}.");
                return;
            }

            _settings.Add(new Setting(lhs, rhs));
        }

        private bool TryExpand(ref string text)
        {
            const int maximumLength = 64 * 1024;
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var any = false;
                foreach (var variable in _variables.OrderByDescending(variable => variable.Key.Length))
                {
                    var token = "$" + variable.Key;
                    if (text.IndexOf(token, StringComparison.Ordinal) < 0)
                        continue;

                    var length = (long)text.Length;
                    for (var index = text.IndexOf(token, StringComparison.Ordinal); index >= 0; index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal))
                    {
                        length += variable.Value.Length - token.Length;
                        if (length > maximumLength)
                            return false;
                    }

                    text = text.Replace(token, variable.Value, StringComparison.Ordinal);
                    any = true;
                }

                if (!any)
                    return true;
            }

            return false;
        }

        private void RememberRule(string value, int lineNumber)
        {
            var parsed = WindowRuleParser.Parse(value);
            foreach (var warning in parsed.Warnings)
                Warn(lineNumber, warning);

            if (parsed.Rule is { } rule)
                _windowRules.Add(rule);
        }

        private void Warn(int line, string message)
        {
            _warnings.Add(new ConfigWarning(line, message, _source));
        }

        private static bool IsLaunchKeyword(string keyword) =>
            keyword == "exec-once";

        private static bool HasComment(string line)
        {
            if (line.TrimStart().StartsWith('#'))
                return true;

            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] != '#')
                    continue;

                if (i + 1 < line.Length && line[i + 1] == '#')
                {
                    i++;
                    continue;
                }

                return true;
            }

            return false;
        }

        private static string CutComment(string line)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
                return "";

            var searchFrom = 0;
            while (true)
            {
                var hash = line.IndexOf('#', searchFrom);
                if (hash < 0)
                    break;

                if (hash + 1 < line.Length && line[hash + 1] == '#')
                {
                    line = string.Concat(line.AsSpan(0, hash + 1), line.AsSpan(hash + 2));
                    searchFrom = hash + 1;
                    continue;
                }

                line = line[..hash];
                break;
            }

            return line.Trim();
        }

        private static List<string> SplitArgs(string value, int? maxFields)
        {
            var parts = new List<string>();
            var start = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != ',')
                    continue;

                if (maxFields is int max && parts.Count == max - 1)
                    break;

                parts.Add(value[start..i].Trim());
                start = i + 1;
            }

            parts.Add(value[start..].Trim());
            return parts;
        }
    }
}
