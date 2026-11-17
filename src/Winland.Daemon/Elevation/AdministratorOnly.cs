namespace Winland.Daemon;

public readonly record struct PrincipalAccess(string Sid, bool CanWrite, bool AppliesToTarget, bool CanRemove = false);

public readonly record struct AccessList(string? Owner, IReadOnlyList<PrincipalAccess> Grants);

public static class AdministratorOnly
{
    public const string SystemSid = "S-1-5-18";

    public const string AdministratorsSid = "S-1-5-32-544";

    public const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    public static bool IsAllowed(string sid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        return sid.Equals(SystemSid, StringComparison.OrdinalIgnoreCase)
            || sid.Equals(AdministratorsSid, StringComparison.OrdinalIgnoreCase)
            || sid.Equals(TrustedInstallerSid, StringComparison.OrdinalIgnoreCase);
    }

    public static string? NonAdministrator(AccessList access)
    {
        ArgumentNullException.ThrowIfNull(access.Grants);
        if (string.IsNullOrWhiteSpace(access.Owner))
            return "unknown owner";

        if (!IsAllowed(access.Owner))
            return "owner " + access.Owner;

        foreach (var grant in access.Grants)
        {
            if (!grant.AppliesToTarget || !grant.CanWrite || string.IsNullOrWhiteSpace(grant.Sid))
                continue;

            if (!IsAllowed(grant.Sid))
                return grant.Sid;
        }

        return null;
    }

    public static string? NonAdministratorAbove(AccessList access)
    {
        ArgumentNullException.ThrowIfNull(access.Grants);
        if (string.IsNullOrWhiteSpace(access.Owner))
            return "unknown owner";

        if (!IsAllowed(access.Owner))
            return "owner " + access.Owner;

        foreach (var grant in access.Grants)
        {
            if (!grant.AppliesToTarget || !grant.CanRemove || string.IsNullOrWhiteSpace(grant.Sid))
                continue;

            if (!IsAllowed(grant.Sid))
                return grant.Sid;
        }

        return null;
    }

    public static (string Path, string Who)? FirstNonAdministratorAbove(IEnumerable<(string Path, AccessList? Access)> ancestors)
    {
        ArgumentNullException.ThrowIfNull(ancestors);
        foreach (var (path, access) in ancestors)
        {
            if (access is not AccessList list)
                return (path, "unreadable access list");

            var who = NonAdministratorAbove(list);
            if (who is not null)
                return (path, who);
        }

        return null;
    }

    public static (string Path, string Who)? FirstNonAdministrator(IEnumerable<(string Path, AccessList? Access)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var (path, access) in entries)
        {
            if (access is not AccessList list)
                return (path, "unreadable access list");

            var who = NonAdministrator(list);
            if (who is not null)
                return (path, who);
        }

        return null;
    }
}
