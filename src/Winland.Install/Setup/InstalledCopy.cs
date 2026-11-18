namespace Winland.Install;

public enum InstallKind
{
    None,
    Machine,
    User,
}

public static class InstalledCopy
{
    public static InstallKind Of(string executable, string machineDirectory, string userDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDirectory);

        var folder = Path.GetDirectoryName(Path.GetFullPath(executable));
        if (folder is null)
            return InstallKind.None;

        if (InstallLocations.SameFolder(folder, machineDirectory))
            return InstallKind.Machine;

        if (InstallLocations.SameFolder(folder, userDirectory))
            return InstallKind.User;

        return InstallKind.None;
    }
}
