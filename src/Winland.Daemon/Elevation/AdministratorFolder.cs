using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Winland.Daemon;

[SupportedOSPlatform("windows")]
public static class AdministratorFolder
{
    public static string CreateNew(string parent, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var folder = Path.Combine(Path.GetFullPath(parent), prefix + Guid.NewGuid().ToString("N"));
        new DirectoryInfo(folder).Create(Security(owner: false));
        if (WindowsAccessReader.FirstNonAdministrator(folder) is var (path, who))
        {
            throw new InvalidOperationException(
                $"Could not make a folder only administrators can write: a non-administrator ({who}) can write '{path}'.");
        }

        return folder;
    }

    public static string CreateInWindowsTemp(string prefix) =>
        CreateNew(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), prefix);

    public static string CreateTemp(string prefix)
    {
        if (UserAccess.IsElevated)
            return CreateInWindowsTemp(prefix);

        var folder = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    internal static DirectorySecurity Security(bool owner)
    {
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var security = new DirectorySecurity();
        if (owner)
            security.SetOwner(administrators);

        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            administrators,
            FileSystemRights.FullControl,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute,
            inherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        return security;
    }
}
