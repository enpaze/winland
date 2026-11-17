namespace Winland.Install;

public static class ReleaseVersion
{
    public static bool IsCurrent(string running, string published)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(running);
        ArgumentException.ThrowIfNullOrWhiteSpace(published);
        return running.Equals(published, StringComparison.Ordinal);
    }

    public static bool IsSameCommit(string running, string published)
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(published);
        if (string.IsNullOrWhiteSpace(running) || string.IsNullOrWhiteSpace(published))
            return false;

        return running.Equals(published, StringComparison.OrdinalIgnoreCase);
    }
}
