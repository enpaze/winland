namespace Winland.Infrastructure;

internal static class ShellFlyouts
{
    private static readonly string[] Hosts =
    [
        "StartMenuExperienceHost.exe",
        "SearchHost.exe",
        "SearchApp.exe",
        "ShellExperienceHost.exe",
        "ShellHost.exe",
        "TextInputHost.exe",
        "Widgets.exe",
        "LockApp.exe",
    ];

    private static readonly string[] ExplorerClasses =
    [
        "Xaml_WindowedPopupClass",
        "TopLevelWindowForOverflowXamlIsland",
        "Shell_InputSwitchTopLevelWindow",
    ];

    internal static bool Is(string exe, string className)
    {
        if (exe.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) && ExplorerClasses.Contains(className, StringComparer.Ordinal))
            return true;

        foreach (var host in Hosts)
        {
            if (host.Equals(exe, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
