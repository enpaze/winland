using Microsoft.Win32;

namespace Winland.Infrastructure;

public static class TrayFiles
{
    public static void Open(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Error.WriteLine("There is no file to open.");
            return;
        }

        var association = FileAssociation.OpenCommand(Path.GetExtension(path));
        _ = ProcessLauncher.TryLaunch(TrayOpener.Open(path, association));
    }
}

internal static class FileAssociation
{
    internal static string? OpenCommand(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        try
        {
            return Lookup(extension);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Lookup(string extension)
    {
        var progId = UserChoice(extension) ?? Registered(extension);
        if (string.IsNullOrWhiteSpace(progId))
            return null;

        using var command = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command");
        return command?.GetValue(null) as string;
    }

    private static string? UserChoice(string extension)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice");
        return key?.GetValue("ProgId") as string;
    }

    private static string? Registered(string extension)
    {
        using var key = Registry.ClassesRoot.OpenSubKey(extension);
        return key?.GetValue(null) as string;
    }
}
