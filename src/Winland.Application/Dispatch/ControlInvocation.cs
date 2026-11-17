using System.Text.Json;

namespace Winland.Application;

public static class ControlInvocation
{
    public static string Usage { get; } =
        string.Concat(CommandProtocol.VerbForms.Select(form => "       winland " + (form == "events" ? "" : "[-j] ") + form + "\n"));

    public static bool TryListNames(string[] args, out string list)
    {
        ArgumentNullException.ThrowIfNull(args);
        list = "";
        var words = args.Where(arg => arg != "-j").ToArray();
        if (words.Length != 1 || !words[0].Equals("dispatch", StringComparison.OrdinalIgnoreCase))
            return false;

        var sorted = WindowManager.DispatcherNames.Order(StringComparer.Ordinal).ToArray();
        list = words.Length < args.Length ? JsonSerializer.Serialize(sorted) : string.Join('\n', sorted);
        return true;
    }

    public static bool TryCreate(string[] args, out string line, out bool events)
    {
        ArgumentNullException.ThrowIfNull(args);
        line = "";
        events = false;
        var flags = "";
        var rest = new List<string>();

        if (args.Any(arg => arg.Contains('\n', StringComparison.Ordinal) || arg.Contains('\r', StringComparison.Ordinal)))
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (IsCommandLine(rest) || !arg.StartsWith('-') || IsNumber(arg))
            {
                rest.Add(arg);
                continue;
            }

            if (arg == "--")
            {
                rest.AddRange(args.Skip(i + 1));
                break;
            }

            if (arg is not "-j")
                return false;

            if (!flags.Contains(arg[1], StringComparison.Ordinal))
                flags += arg[1];
        }

        var prefix = flags.Length == 0 ? "" : flags + "/";
        if (rest.Count == 0)
            return false;

        var request = string.Join(' ', Quoted(rest));
        if (CommandProtocol.Parse(prefix + request) is not CommandParsed parsed)
            return false;

        events = parsed.Request is EventsRequest;
        if (events && prefix.Length > 0)
            return false;

        line = prefix + request;
        return true;
    }

    private static bool IsCommandLine(List<string> words) =>
        words.Count >= 2
        && words[0].Equals("dispatch", StringComparison.OrdinalIgnoreCase)
        && words[1].Equals("exec", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Quoted(List<string> words)
    {
        var command = IsCommandLine(words) && words.Count > 3;
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            yield return command && i >= 2 && word.Contains(' ', StringComparison.Ordinal)
                && !word.Contains('"', StringComparison.Ordinal) && !word.StartsWith('[')
                ? "\"" + word + "\""
                : word;
        }
    }

    private static bool IsNumber(string text)
    {
        var i = 0;
        if (text.Length > 1 && text[0] == '-')
            i = 1;

        if (i >= text.Length || !char.IsAsciiDigit(text[^1]))
            return false;

        var point = false;
        for (; i < text.Length; i++)
        {
            if (text[i] == '.' && !point)
            {
                point = true;
                continue;
            }

            if (!char.IsAsciiDigit(text[i]))
                return false;
        }

        return true;
    }
}
