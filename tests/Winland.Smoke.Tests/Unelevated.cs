using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace Winland.Smoke.Tests;

internal static partial class Unelevated
{
    private const uint SaferScopeUser = 2;
    private const uint SaferLevelNormalUser = 0x20000;
    private const uint SaferLevelOpen = 1;

    public static bool Elevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static CliResult Run(string executable, string dotnetRoot, params string[] arguments)
    {
        if (!Elevated())
            return SmokeCli.Run(executable, dotnetRoot, null, Path.GetTempPath(), arguments, TimeSpan.FromMinutes(5), environment: null);

        var folder = Path.Combine(Path.GetTempPath(), "winland-unelevated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var stdout = Path.Combine(folder, "out.txt");
            var stderr = Path.Combine(folder, "err.txt");
            var command = $"/d /s /c \"set DOTNET_ROOT={dotnetRoot}&& \"{executable}\" {string.Join(' ', arguments.Select(Quote))} > \"{stdout}\" 2> \"{stderr}\"\"";
            var exitCode = Start(Path.Combine(Environment.SystemDirectory, "cmd.exe"), command, folder);
            return new CliResult(exitCode, ReadText(stdout), ReadText(stderr));
        }
        finally
        {
            try
            {
                SmokeDaemon.DeleteTree(folder);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static unsafe int Start(string application, string arguments, string directory)
    {
        if (!SaferCreateLevel(SaferScopeUser, SaferLevelNormalUser, SaferLevelOpen, out var level, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "SaferCreateLevel failed.");

        try
        {
            if (!SaferComputeTokenFromLevel(level, IntPtr.Zero, out var token, 0, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "SaferComputeTokenFromLevel failed.");

            try
            {
                var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
                var line = ("\"" + application + "\" " + arguments + "\0").ToCharArray();
                PROCESS_INFORMATION info;
                fixed (char* commandLine = line)
                fixed (char* folder = directory)
                {
                    if (!PInvoke.CreateProcessAsUser(
                        new HANDLE(token),
                        null,
                        new PWSTR(commandLine),
                        null,
                        null,
                        false,
                        PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW,
                        null,
                        folder,
                        &startup,
                        &info))
                    {
                        throw new Win32Exception(Marshal.GetLastPInvokeError(), "CreateProcessAsUser failed.");
                    }
                }

                try
                {
                    if (PInvoke.WaitForSingleObject(info.hProcess, 300_000) != WAIT_EVENT.WAIT_OBJECT_0)
                    {
                        _ = PInvoke.TerminateProcess(info.hProcess, 1);
                        throw new TimeoutException("The unelevated command did not finish in five minutes.");
                    }

                    uint exitCode;
                    if (!PInvoke.GetExitCodeProcess(info.hProcess, &exitCode))
                        throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetExitCodeProcess failed.");

                    return (int)exitCode;
                }
                finally
                {
                    PInvoke.CloseHandle(info.hThread);
                    PInvoke.CloseHandle(info.hProcess);
                }
            }
            finally
            {
                PInvoke.CloseHandle(new HANDLE(token));
            }
        }
        finally
        {
            _ = SaferCloseLevel(level);
        }
    }

    private static string Quote(string argument) =>
        argument.Length > 0 && !argument.Any(char.IsWhiteSpace) && !argument.Contains('"', StringComparison.Ordinal) ? argument : "\"" + argument + "\"";

    private static string ReadText(string path)
    {
        if (!File.Exists(path))
            return "";

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SaferCreateLevel(uint scopeId, uint levelId, uint openFlags, out IntPtr level, IntPtr reserved);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SaferComputeTokenFromLevel(IntPtr level, IntPtr inAccessToken, out IntPtr outAccessToken, uint flags, IntPtr reserved);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SaferCloseLevel(IntPtr level);
}
