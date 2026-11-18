using System.ComponentModel;
using System.Diagnostics;

namespace Winland.Smoke.Tests;

internal readonly record struct CliResult(int ExitCode, string Output, string Error)
{
    public override string ToString() =>
        $"exit {ExitCode}" + (Output.Length > 0 ? " stdout: " + Output.Trim() : "") + (Error.Length > 0 ? " stderr: " + Error.Trim() : "");
}

internal static class SmokeCli
{
    public static CliResult Run(SmokePaths paths, string dotnetRoot, string? pipe, string directory, IEnumerable<string> arguments, TimeSpan? timeout = null) =>
        Run(paths.Daemon, dotnetRoot, pipe, directory, arguments, timeout, environment: null);

    public static CliResult Run(string executable, string dotnetRoot, string? pipe, string directory, IEnumerable<string> arguments, TimeSpan? timeout, IReadOnlyDictionary<string, string>? environment)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        if (pipe is not null)
            info.Environment["WINLAND_PIPE"] = pipe;

        info.Environment["DOTNET_ROOT"] = dotnetRoot;
        info.Environment["WINLAND_NO_ELEVATE"] = "1";
        if (environment is not null)
        {
            foreach (var (name, value) in environment)
                info.Environment[name] = value;
        }

        using var process = new Process { StartInfo = info };
        if (!process.Start())
            return new CliResult(-1, "", "winland did not start.");

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(30)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
            }

            return new CliResult(-1, "", $"winland {string.Join(' ', info.ArgumentList)} timed out.");
        }

        return new CliResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
