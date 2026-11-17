namespace Winland.Daemon;

sealed class FileConfigSource(string directory) : IConfigSource
{
    public string? Read(string path) => UserAccess.Run(() => ReadFile(path));

    private string? ReadFile(string path)
    {
        try
        {
            var full = Path.IsPathRooted(path) ? path : Path.Combine(directory, path);

            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception exception) when (Unreadable(exception))
        {
            return null;
        }
    }

    private static bool Unreadable(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    public string? Variable(string name) => Environment.GetEnvironmentVariable(name);

    public IReadOnlyList<string> Match(string pattern) => UserAccess.Run(() => MatchFiles(pattern));

    private IReadOnlyList<string> MatchFiles(string pattern)
    {
        try
        {
            var full = Path.IsPathRooted(pattern) ? pattern : Path.Combine(directory, pattern);
            var (root, depth) = Plan(full);
            if (!Directory.Exists(root))
                return [];

            var options = new EnumerationOptions
            {
                AttributesToSkip = 0,
                IgnoreInaccessible = true,
                RecurseSubdirectories = depth > 0,
                MaxRecursionDepth = depth,
            };
            return Directory.EnumerateFiles(root, "*", options)
                .Where(file => SourcePath.Matches(full, file))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (Unreadable(exception))
        {
            return [];
        }
    }

    internal static (string Root, int Depth) Plan(string full)
    {
        var separator = Path.DirectorySeparatorChar;
        var normalized = full.Replace(Path.AltDirectorySeparatorChar, separator);
        var wildcard = normalized.IndexOfAny(['*', '?', '[']);
        var prefix = wildcard < 0 ? normalized : normalized[..wildcard];
        var slash = prefix.LastIndexOf(separator);
        if (slash < 0)
            return ("", 0);

        var root = prefix[..slash];
        var driveRoot = Path.GetPathRoot(normalized);
        if (!string.IsNullOrEmpty(driveRoot) && root.Length < driveRoot.Length)
            root = driveRoot;

        var depth = normalized[(slash + 1)..].Count(ch => ch == separator);
        return (root, depth);
    }
}
