using System.Diagnostics;
using Winland.Install;

namespace Winland.Daemon;

sealed class DaemonSession : IDaemonSession
{
    public bool Stop()
    {
        try
        {
            return StopCurrent();
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidOperationException("Winland is running as another user. Quit it from its tray icon, then try again.", exception);
        }
    }

    private static bool StopCurrent()
    {
        var pipe = Pipe();
        if (PipeServer.ProcessId(pipe) is not int id)
            return false;

        Process daemon;
        try
        {
            daemon = Process.GetProcessById(id);
        }
        catch (ArgumentException)
        {
            return false;
        }

        using (daemon)
        {
            try
            {
                CommandClient.Transact("dispatch exit", pipe);
            }
            catch (InvalidOperationException exception) when (exception.Message == "Winland is not running.")
            {
            }

            if (!daemon.WaitForExit(20_000))
                throw new InvalidOperationException("Winland did not exit.");
        }

        return true;
    }

    public void Start(string executable, IReadOnlyList<string> arguments)
    {
        if (!arguments.Contains(ManualStartElevation.OptOutArgument, StringComparer.OrdinalIgnoreCase))
        {
            if (SchtasksCatalog.UseLauncher(executable))
            {
                SchtasksCatalog.Start();
                return;
            }
        }

        var start = new ProcessStartInfo
        {
            FileName = Elevation.IsElevated()
                ? InstallLocations.Launcher(executable)
                : executable,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Winland did not start.");
    }

    private static string? Pipe()
    {
        var configured = Environment.GetEnvironmentVariable("WINLAND_PIPE");
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
