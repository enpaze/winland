using System.ComponentModel;
using System.Text;

namespace Winland.Daemon;

static class ControlCommand
{
    public static int Run(string line, bool events, string? pipeName)
    {
        WriteUtf8WhenRedirected();
        if (events)
            return FollowEvents(line, pipeName);

        try
        {
            var reply = CommandClient.Transact(line, pipeName, lendForeground: true);
            return WriteReply(line, reply);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or Win32Exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static int WriteReply(string line, string? reply)
    {
        if (reply is null || reply.StartsWith("error:", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(reply ?? "error: no reply");
            return 1;
        }

        if (reply.Length > 0)
            Console.WriteLine(reply);

        return 0;
    }

    private static int FollowEvents(string line, string? pipeName)
    {
        using var cancel = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, args) =>
        {
            args.Cancel = true;
            cancel.Cancel();
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            CommandClient.Follow(line, static streamed =>
            {
                Console.WriteLine(streamed);
                Console.Out.Flush();
            }, pipeName, cancel: cancel.Token);
            if (cancel.IsCancellationRequested)
                return 0;

            Console.Error.WriteLine("error: events stream ended");
            return 1;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or Win32Exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    private static void WriteUtf8WhenRedirected()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (Console.IsOutputRedirected)
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        if (Console.IsErrorRedirected)
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
    }
}
