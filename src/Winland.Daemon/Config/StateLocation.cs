namespace Winland.Daemon;

static class StateLocation
{
    public const string Variable = "WINLAND_STATE";

    public static (string Path, string? Ignored) Choose(string? variable, string defaultPath, bool elevated, Func<string, string?> refuseFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultPath);
        ArgumentNullException.ThrowIfNull(refuseFolder);
        if (string.IsNullOrWhiteSpace(variable))
            return (defaultPath, null);

        if (!elevated)
            return (variable, null);

        var folder = Path.GetDirectoryName(Path.GetFullPath(variable));
        var refused = folder is null ? $"'{variable}' names no folder." : refuseFolder(folder);
        return refused is null
            ? (variable, null)
            : (defaultPath, $"Ignoring {Variable}, so the state file is '{defaultPath}': {refused}");
    }

    public static string? Refusal(string folder, AccessList? access, string user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        if (access is not AccessList list)
            return $"Winland could not check who can write '{folder}'.";

        if (user.Equals(list.Owner, StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var grant in list.Grants)
        {
            if (grant.AppliesToTarget && grant.CanRemove && user.Equals(grant.Sid, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return $"'{folder}' is not a folder the user can already empty, and Winland writes the state file there as an administrator.";
    }
}
