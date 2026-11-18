using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Winland.Install;

namespace Winland.Daemon;

static class Elevation
{
    private const string OutputArgument = "--winland-output";

    private static NamedPipeClientStream[]? s_output;

    public static int Relaunch(IReadOnlyList<string> args)
    {
        var path = Environment.ProcessPath
            ?? throw new InvalidOperationException("The executable path is not available.");
        var launcher = InstallLocations.Launcher(path);
        if (File.Exists(launcher))
            path = launcher;
        var name = "winland-output-" + Guid.NewGuid().ToString("N");
        RelaunchSpec spec;
        try
        {
            spec = RelaunchPlan.Create(
                path,
                [OutputArgument, name, .. args],
                Environment.CurrentDirectory,
                RuntimeEnvironment.GetRuntimeDirectory(),
                Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                Environment.GetEnvironmentVariable(CommandClient.PipeVariable),
                Environment.GetEnvironmentVariable(StateLocation.Variable));
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        var start = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        using var output = OpenOutput(name + "-out");
        using var error = OpenOutput(name + "-err");
        using var unanswered = new CancellationTokenSource();
        try
        {
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Elevation did not start.");
            var copies = Task.WhenAll(Copy(output, Console.Out, unanswered.Token), Copy(error, Console.Error, unanswered.Token));
            process.WaitForExit();
            unanswered.Cancel();
            copies.Wait();
            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Console.Error.WriteLine("Elevation was cancelled.");
            return ex.NativeErrorCode;
        }
        catch (Win32Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static string[] SendOutputBack(string[] args)
    {
        if (args.Length < 2 || !args[0].Equals(OutputArgument, StringComparison.Ordinal))
            return args;

        try
        {
            var output = Connect(args[1] + "-out");
            var error = Connect(args[1] + "-err");
            s_output = [output, error];
            _ = SetStdHandle(StandardOutput, output.SafePipeHandle.DangerousGetHandle());
            _ = SetStdHandle(StandardError, error.SafePipeHandle.DangerousGetHandle());
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
        }

        return args[2..];
    }

    private static NamedPipeServerStream OpenOutput(string name) =>
        new(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private static NamedPipeClientStream Connect(string name)
    {
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Identification);
        pipe.Connect(5_000);
        return pipe;
    }

    private static async Task Copy(NamedPipeServerStream pipe, TextWriter target, CancellationToken unanswered)
    {
        try
        {
            await pipe.WaitForConnectionAsync(unanswered).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var reader = new StreamReader(pipe, Encoding.UTF8);
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
        {
            target.Write(buffer, 0, read);
            target.Flush();
        }
    }

    private const int StandardOutput = -11;
    private const int StandardError = -12;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int handle, IntPtr value);
}
