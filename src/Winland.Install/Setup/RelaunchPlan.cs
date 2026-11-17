using System.Buffers;

namespace Winland.Install;

public sealed record RelaunchSpec(string FileName, string Arguments, string WorkingDirectory);

public static class RelaunchPlan
{
    public static RelaunchSpec Create(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string? runtimeDirectory,
        string? dotnetRoot,
        string? pipe,
        string? state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var assignments = new List<string>();
        var root = RuntimeRoot(dotnetRoot, runtimeDirectory);
        if (root is not null)
            assignments.Add(Assignment("DOTNET_ROOT", root));

        AddIfPresent(assignments, "WINLAND_PIPE", pipe);
        AddIfPresent(assignments, "WINLAND_STATE", state);

        var launch = QuoteAlways(Checked(executable, "the executable path"));
        if (arguments.Count > 0)
            launch += " " + string.Join(' ', arguments.Select(argument => Quote(Checked(argument, "an argument"))));

        var steps = new List<string> { "cd /d " + QuoteAlways(DirectoryOf(workingDirectory)) };
        steps.AddRange(assignments);
        steps.Add(launch);

        var interpreter = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        return new RelaunchSpec(interpreter,"/d /v:off /s /c \"" + string.Join(" && ", steps) + "\"", workingDirectory);
    }

    private static string Checked(string value, string what)
    {
        if (value.AsSpan().IndexOfAny("\"%\r\n") >= 0)
            throw new ArgumentException($"Cannot pass {what} '{value}' to the elevated process: it contains a quote, a percent sign or a line break.");

        return value;
    }

    private static string DirectoryOf(string workingDirectory)
    {
        var directory = Checked(workingDirectory.Trim(), "the working directory");
        if (directory.Length > 3)
            directory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return directory;
    }

    public static bool IsFrameworkDependent(string? runtimeDirectory)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            return false;

        var version = new DirectoryInfo(Path.TrimEndingDirectorySeparator(runtimeDirectory.Trim()));
        var framework = version.Parent;
        var shared = framework?.Parent;
        return framework is not null
            && shared is not null
            && framework.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase)
            && shared.Name.Equals("shared", StringComparison.OrdinalIgnoreCase);
    }

    private static string? RuntimeRoot(string? dotnetRoot, string? runtimeDirectory)
    {
        if (!IsFrameworkDependent(runtimeDirectory))
            return null;

        if (!string.IsNullOrWhiteSpace(dotnetRoot))
            return dotnetRoot.Trim();

        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            return null;

        return Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", ".."));
    }

    private static void AddIfPresent(List<string> assignments, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        assignments.Add(Assignment(name, value));
    }

    private static string Assignment(string name, string value) =>
        "set \"" + name + "=" + Checked(value, name) + "\"";

    private static readonly SearchValues<char> MetaCharacters = SearchValues.Create(" \t&|<>^!()");

    private static string QuoteAlways(string value)
    {
        var trailing = value.Length - value.TrimEnd('\\').Length;
        return "\"" + value + new string('\\', trailing) + "\"";
    }

    private static string Quote(string value)
    {
        if (value.Length > 0 && value.AsSpan().IndexOfAny(MetaCharacters) < 0)
            return value;

        return QuoteAlways(value);
    }
}
