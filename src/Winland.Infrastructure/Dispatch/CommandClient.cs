using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using Windows.Win32;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace Winland.Infrastructure;

public static class CommandClient
{
    public const string PipeVariable = "WINLAND_PIPE";

    public static string DefaultName
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return CommandProtocol.PipeName + "-" + process.SessionId.ToString(CultureInfo.InvariantCulture);
        }
    }

    public static string? ConfiguredName()
    {
        var configured = Environment.GetEnvironmentVariable(PipeVariable);
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    public static int? ProcessId(string? pipeName = null, int timeoutMs = 2000)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName ?? DefaultName, PipeDirection.InOut);
        try
        {
            pipe.Connect(timeoutMs);
        }
        catch (TimeoutException)
        {
            return null;
        }

        return VerifyServer(pipe);
    }

    public static string Transact(string command, string? pipeName = null, int timeoutMs = 2000, bool lendForeground = false, int replyTimeoutMs = 10_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        using var pipe = new NamedPipeClientStream(".", pipeName ?? DefaultName, PipeDirection.InOut, PipeOptions.Asynchronous);
        var server = Connect(pipe, timeoutMs);
        if (lendForeground)
            _ = PInvoke.AllowSetForegroundWindow((uint)server);
        using var timeout = new CancellationTokenSource(replyTimeoutMs);
        try
        {
            var outbound = Encoding.UTF8.GetBytes(command + "\n");
            pipe.WriteAsync(outbound, timeout.Token).AsTask().GetAwaiter().GetResult();
            pipe.FlushAsync(timeout.Token).GetAwaiter().GetResult();

            using var buffered = new BufferedStream(pipe);
            return ReadReply(buffered, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new InvalidOperationException("Winland is not answering.");
        }
    }

    public static void Follow(string command, Action<string> onLine, string? pipeName = null, int timeoutMs = 2000, CancellationToken cancel = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(onLine);
        using var pipe = new NamedPipeClientStream(".", pipeName ?? DefaultName, PipeDirection.InOut, PipeOptions.Asynchronous);
        Connect(pipe, timeoutMs);
        using var registration = cancel.Register(static state =>
        {
            var stream = (NamedPipeClientStream)state!;
            try
            {
                stream.Dispose();
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
            }
        }, pipe);

        var outbound = Encoding.UTF8.GetBytes(command + "\n");
        pipe.Write(outbound);
        pipe.Flush();
        using var buffered = new BufferedStream(pipe);
        try
        {
            while (!cancel.IsCancellationRequested && TryReadLine(buffered, out var line))
                onLine(line);
        }
        catch (Exception exception) when (cancel.IsCancellationRequested && exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private static int Connect(NamedPipeClientStream pipe, int timeoutMs)
    {
        try
        {
            pipe.Connect(timeoutMs);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Winland is not running.");
        }

        return VerifyServer(pipe);
    }

    private static int VerifyServer(NamedPipeClientStream pipe)
    {
        if (!PInvoke.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var id))
            throw new Win32Exception();

        using var server = Process.GetProcessById((int)id);
        using var current = Process.GetCurrentProcess();
        if (server.SessionId != current.SessionId)
            throw new InvalidOperationException("The command pipe belongs to another session.");

        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, id);
        if (process.IsInvalid || !PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
            throw new Win32Exception();

        using (token)
        using (var owner = new WindowsIdentity(token.DangerousGetHandle()))
        using (var identity = WindowsIdentity.GetCurrent())
        {
            if (owner.User != identity.User)
                throw new InvalidOperationException("The command pipe belongs to another user.");
        }

        return (int)id;
    }

    private static string ReadReply(Stream stream, CancellationToken cancel)
    {
        try
        {
            return PipeFrame.ReadReply(stream, cancel);
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException("The reply is too large.");
        }
    }

    private static bool TryReadLine(Stream stream, out string line)
    {
        line = "";
        try
        {
            if (PipeFrame.ReadLineAsync(stream, PipeFrame.ReplyLimit).GetAwaiter().GetResult() is not string read)
                return false;

            line = read;
            return true;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException("The reply is too large.");
        }
    }
}
