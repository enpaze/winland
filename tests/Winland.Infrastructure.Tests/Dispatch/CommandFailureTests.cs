using System.IO.Pipes;

namespace Winland.Infrastructure.Tests;
[TestClass]
public class CommandFailureTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedCommandsLogTheException(bool afterCommand)
    {
        var name = "winland-test-" + Guid.NewGuid().ToString("N");
        using var error = new StringWriter();
        var previous = Console.Error;
        Console.SetError(error);
        try
        {
            using var server = new CommandServer(new WindowManager(), _ =>
            {
                if (!afterCommand)
                    throw new InvalidOperationException("apply failed");
            }, name)
            {
                AfterCommand = () => throw new InvalidOperationException("after command failed"),
            };
            server.Start();

            Exception? failure = null;
            try
            {
                CommandClient.Transact("workspace 2", name);
            }
            catch (Exception caught)
            {
                failure = caught;
            }
            Assert.IsTrue(failure is null or IOException, failure?.ToString());
            Assert.Contains(afterCommand ? "after command failed" : "apply failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(previous);
        }
    }

    [TestMethod]
    public void AProcessIdProbeDoesNotApplyACommand()
    {
        var name = "winland-test-" + Guid.NewGuid().ToString("N");
        var applied = 0;
        var followed = 0;
        using var server = new CommandServer(new WindowManager(), _ => Interlocked.Increment(ref applied), name)
        {
            AfterCommand = () => Interlocked.Increment(ref followed),
        };
        server.Start();

        Assert.AreEqual(Environment.ProcessId, CommandClient.ProcessId(name));
        Assert.IsFalse(SpinWait.SpinUntil(() => Volatile.Read(ref followed) != 0, TimeSpan.FromMilliseconds(500)));
        Assert.AreEqual(0, Volatile.Read(ref applied));
        Assert.AreEqual("ok", CommandClient.Transact("workspace 2", name));
        Assert.AreEqual(1, Volatile.Read(ref applied));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("#10\nabc")]
    public async Task AConnectedServerThatStopsAnsweringHasAReplyDeadline(string partialReply)
    {
        var name = "winland-test-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();
        var request = Task.Run(() => CommandClient.Transact("get windows", name, replyTimeoutMs: 200));
        await connected.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("get windows", await PipeFrame.ReadLineAsync(server, PipeFrame.RequestLimit));
        await server.WriteAsync(System.Text.Encoding.UTF8.GetBytes(partialReply));

        var finished = SpinWait.SpinUntil(() => request.IsCompleted, TimeSpan.FromSeconds(5));
        if (!finished)
            server.Dispose();

        Assert.IsTrue(finished);
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => request);
        Assert.AreEqual("Winland is not answering.", error.Message);
    }
}
