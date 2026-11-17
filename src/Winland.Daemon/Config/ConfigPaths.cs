using Winland.Install;

namespace Winland.Daemon;

static class ConfigPaths
{
    public static string Default(string userProfile) =>
        Path.Combine(InstallLocations.Config(userProfile), "winland.conf");

    public static IReadOnlyList<string> Watched(string configPath, IEnumerable<string> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(sources);
        var full = Path.GetFullPath(configPath);
        var directory = Path.GetDirectoryName(full) ?? ".";
        var paths = new List<string> { full };
        foreach (var source in sources)
        {
            try
            {
                paths.Add(Path.GetFullPath(Path.Combine(directory, source)));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return paths;
    }
}
