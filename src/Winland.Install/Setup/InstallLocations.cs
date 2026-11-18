namespace Winland.Install;

public static class InstallLocations
{
    public const string FolderName = "Winland";

    public const string AppUserModelId = "Winland.Winland";

    public const string ShortcutName = "Winland.lnk";

    public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Winland";

    public const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public const string RunValue = "Winland";

    public const string MachinePathKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    public const string UserPathKey = "Environment";

    public const string PathValue = "Path";

    public const string HomeFolder = ".winland";

    public static string Machine(string programFiles) => Path.Combine(programFiles, FolderName);

    public static string User(string localAppData) => Path.Combine(localAppData, "Programs", FolderName);

    public static string Config(string userProfile) => Path.Combine(userProfile, HomeFolder);

    public const string SettingsFileName = "windows-settings.json";

    public static string SettingsFile(string userProfile) => Path.Combine(Config(userProfile), SettingsFileName);

    public const string ExecutableName = "winland.exe";

    public static string Executable(string directory) => Path.Combine(directory, ExecutableName);

    public const string LauncherName = "winland-launcher.exe";

    public static string Launcher(string executable) => Path.Combine(Path.GetDirectoryName(executable)!, LauncherName);

    public static string Shortcut(string programsDirectory) => Path.Combine(programsDirectory, ShortcutName);

    public static bool SameFolder(string left, string right) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left))
            .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);

    public static bool IsPerUserInstall(string? executable, string userDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDirectory);
        if (string.IsNullOrWhiteSpace(executable))
            return false;

        var folder = Path.GetDirectoryName(Path.GetFullPath(executable));
        var expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userDirectory));
        return folder is not null && folder.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
