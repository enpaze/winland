using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winland.Application;

public static class ControlInvocation
{
    public static string Usage { get; } =
        string.Concat(CommandProtocol.VerbForms.Select(form => "       winland [-j] " + form + "\n"))
        + "       winland [-j] version\n";

    public static bool TryAnswerLocally(string[] args, out string reply)
    {
        ArgumentNullException.ThrowIfNull(args);
        reply = "";
        var words = args.Where(arg => arg != "-j").ToArray();
        if (words.Length != 1)
            return false;

        var json = words.Length < args.Length;
        if (words[0].Equals("dispatch", StringComparison.OrdinalIgnoreCase))
        {
            var sorted = WindowManager.DispatcherNames.Order(StringComparer.Ordinal).ToArray();
            reply = json ? JsonSerializer.Serialize(sorted) : string.Join('\n', sorted);
            return true;
        }

        if (words[0].Equals("version", StringComparison.OrdinalIgnoreCase))
        {
            reply = json ? VersionJson() : VersionText();
            return true;
        }

        return false;
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

    private static string VersionJson() =>
        JsonSerializer.Serialize(new VersionView(BuildStamp.Version, BuildStamp.Commit, BuildStamp.Dirty));

    private static string VersionText()
    {
        var details = new List<string>();
        if (BuildStamp.Commit.Length > 0)
            details.Add(BuildStamp.Commit.Length > 7 ? BuildStamp.Commit[..7] : BuildStamp.Commit);

        if (BuildStamp.Dirty)
            details.Add("dirty");

        var version = "winland " + BuildStamp.Version;
        return details.Count == 0 ? version : version + " (" + string.Join(", ", details) + ")";
    }

    private sealed record VersionView(
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("commit")] string Commit,
        [property: JsonPropertyName("dirty")] bool Dirty);
}
