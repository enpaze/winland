using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Winland.Infrastructure.Tests;
[TestClass]
public class CommandServerTests
{
    [TestMethod]
    public void MissingDaemonReportsThatWinlandIsNotRunning()
    {
        var missing = "winland-missing-" + Guid.NewGuid().ToString("N");

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => CommandClient.Transact("clients", missing, 200));

        Assert.AreEqual("Winland is not running.", error.Message);
    }

    [TestMethod]
    public void PipeNamesOnlyTheGivenUserAndAllowsMediumIntegrity()
    {
        var user = new SecurityIdentifier("S-1-5-21-1-2-3-1001");

        var security = CommandServer.SecurityFor(user);

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));
        var rule = Assert.ContainsSingle(rules.Cast<PipeAccessRule>());
        Assert.AreEqual(AccessControlType.Allow, rule.AccessControlType);
        Assert.AreEqual(user.Value, rule.IdentityReference.Value);
        Assert.IsTrue(rule.PipeAccessRights.HasFlag(PipeAccessRights.ReadData));
        Assert.IsTrue(rule.PipeAccessRights.HasFlag(PipeAccessRights.WriteData));

        var descriptor = CommandServer.DescriptorFor(user);
        var aces = descriptor.DiscretionaryAcl!.Cast<CommonAce>().ToList();
        var userAce = Assert.ContainsSingle(ace => ace.SecurityIdentifier == user, aces);
        Assert.AreEqual(0, userAce.AccessMask & (int)PipeAccessRights.CreateNewInstance);
        var admins = Assert.ContainsSingle(ace => ace.SecurityIdentifier.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid), aces);
        Assert.AreNotEqual(0, admins.AccessMask & (int)PipeAccessRights.CreateNewInstance);
        Assert.IsNotNull(descriptor.SystemAcl);
        var label = Assert.IsExactInstanceOfType<CustomAce>(Assert.ContainsSingle(descriptor.SystemAcl.Cast<GenericAce>()));
        var opaque = label.GetOpaque();
        Assert.IsNotNull(opaque);
        Assert.AreSequenceEqual(new byte[] { 1, 0, 0, 0 }, opaque[..4]);
        Assert.AreSequenceEqual(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0x10, 0, 0x20, 0, 0 }, opaque[^12..]);
    }

    [TestMethod]
    public void StartFailsWhenAnotherProcessHoldsThePipeName()
    {
        var name = PipeName();
        using var squatter = new NamedPipeServerStream(
            name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var server = new CommandServer(new WindowManager(), _ => { }, name);

        var error = Assert.ThrowsExactly<InvalidOperationException>(server.Start);

        Assert.Contains("already in use", error.InnerException?.Message ?? error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ASecondServerCannotTakeAPipeNameInUse()
    {
        var name = PipeName();
        using var first = new CommandServer(new WindowManager(), _ => { }, name);
        first.Start();
        using var second = new CommandServer(new WindowManager(), _ => { }, name);

        Assert.ThrowsExactly<InvalidOperationException>(second.Start);
        Assert.AreEqual("ok", CommandClient.Transact("dispatch workspace 1", name));
    }

    [TestMethod]
    public void ClientRoundTripsClientsAndDispatch()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(42), new Rectangle(0, 0, 10, 10)));
        var name = "winland-" + Guid.NewGuid().ToString("N");
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();

        var clients = JsonElement.Parse(CommandClient.Transact("j/clients", name)!);
        var client = Assert.ContainsSingle(clients.EnumerateArray());
        Assert.AreEqual("0x2a", client.GetProperty("address").GetString());
        Assert.AreEqual("ok", CommandClient.Transact("dispatch exec", name));
        Assert.AreEqual("error: unknown request", CommandClient.Transact("nope", name));
    }

    [TestMethod]
    public void AnEmptyReplyArrivesEmpty()
    {
        var name = PipeName();
        using var server = new CommandServer(new WindowManager(), _ => { }, name);
        server.Start();

        Assert.AreEqual("", CommandClient.Transact("activewindow", name));
        Assert.AreEqual("null", CommandClient.Transact("j/activewindow", name));
    }

    [TestMethod]
    public void SubscribersReceiveEachEventInOrder()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();
        using var first = new Listener(name);
        using var second = new Listener(name, "j/events");
        WaitUntilSubscribed(core, first);
        WaitUntilSubscribed(core, second);
        Sync(core, first, second);

        var area = new Rectangle(0, 0, 200, 100);
        core.Handle(new WindowAppeared(new WindowId(7), area)
        {
            Traits = WindowTraits.Normal with { Class = "app", Title = "title" },
        });
        Assert.AreSequenceEqual(
            ["openwindow>>7,1,,app,title", "activewindow>>app,title", "activewindowv2>>7"],
            Take(first, 3));
        Assert.AreSequenceEqual(
            ["openwindow>>7,1,,app,title", "activewindow>>app,title", "activewindowv2>>7"],
            Take(second, 3));

        Assert.AreEqual("ok", CommandClient.Transact("dispatch workspace 2", name));
        Assert.AreSequenceEqual(
            [
                "createworkspace>>2,",
                "createworkspacev2>>2,2,",
                "workspace>>2,",
                "workspacev2>>2,2,",
                "activewindow>>,",
                "activewindowv2>>",
            ],
            Take(first, 6));
        Assert.AreSequenceEqual(
            [
                "createworkspace>>2,",
                "createworkspacev2>>2,2,",
                "workspace>>2,",
                "workspacev2>>2,2,",
                "activewindow>>,",
                "activewindowv2>>",
            ],
            Take(second, 6));

        core.Handle(new WindowClosed(new WindowId(7)));
        Assert.AreSequenceEqual(["closewindow>>7"], Take(first, 1));
    }

    [TestMethod]
    public void ASubscriberReceivesALargeOutcomeWithoutBeingDropped()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();
        using var listener = new Listener(name);
        WaitUntilSubscribed(core, listener);
        Sync(core, listener);
        var warnings = Enumerable.Range(1, 256).Select(line => new ConfigWarning(line, "bad config")).ToArray();

        core.Handle(new ConfigLoaded(new ConfigDocument([], [], warnings)));

        var expected = warnings.Select(warning => $"warning>>{warning.Line},,bad config").Append("configreloaded>>").ToArray();
        Assert.AreSequenceEqual(expected, Take(listener, expected.Length));
        Assert.AreEqual("ok", CommandClient.Transact("dispatch workspace 2", name));
        Assert.Contains("workspacev2>>2,2,", Take(listener, 6));
    }

    [TestMethod]
    public async Task ASlowSubscriberDoesNotBlockTheCore()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();
        using var idle = Connect(name);
        Send(idle, "events");
        Assert.AreEqual("[]", CommandClient.Transact("j/clients", name));

        var finished = Task.Run(() =>
        {
            for (var i = 0; i < 600; i++)
                core.Execute(i % 2 == 0 ? "dispatch workspace 2" : "dispatch workspace 1");
        });

        await finished.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("ok", CommandClient.Transact("dispatch workspace 1", name));
    }

    [TestMethod]
    public void ADeadSubscriberIsDroppedAndThePipeKeepsServing()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();
        var idle = Connect(name);
        Send(idle, "events");
        Assert.AreEqual("[]", CommandClient.Transact("j/clients", name));
        idle.Dispose();

        using var listener = new Listener(name);
        WaitUntilSubscribed(core, listener);
        Sync(core, listener);
        core.Handle(new WindowAppeared(new WindowId(3), new Rectangle(0, 0, 10, 10)));
        Assert.AreSequenceEqual(["openwindow>>3,1,,,", "activewindow>>,", "activewindowv2>>3"], Take(listener, 3));
        Assert.AreEqual("ok", CommandClient.Transact("dispatch workspace 1", name));
    }

    [TestMethod]
    public void ThePipeServesAgainAfterEveryInstanceWasTaken()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name) { MaxInstances = 2, RequestTimeout = TimeSpan.FromMilliseconds(300) };
        server.Start();
        using (Connect(name))
        using (Connect(name))
        {
            Thread.Sleep(100);
        }

        Assert.AreEqual("[]", CommandClient.Transact("j/clients", name, timeoutMs: 5000));
    }

    [TestMethod]
    public void AClientThatSendsNothingDoesNotStallTheNext()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();
        using var silent = Connect(name);

        Assert.AreEqual("[]", CommandClient.Transact("j/clients", name));
        Assert.AreEqual("ok", CommandClient.Transact("dispatch workspace 1", name));
    }

    [TestMethod]
    public async Task AClientThatSendsNothingIsDroppedAfterTheRequestTimeout()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name) { RequestTimeout = TimeSpan.FromMilliseconds(200) };
        server.Start();
        using var silent = Connect(name);

        var one = new byte[1];
        var read = silent.ReadAsync(one).AsTask();
        Assert.AreEqual(0, await read.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public void IdleSubscribersHoldNoPoolThreads()
    {
        var core = new WindowManager();
        var name = PipeName();
        using var server = new CommandServer(core, _ => { }, name);
        server.Start();
        ThreadPool.GetMinThreads(out var workers, out _);
        var count = Math.Clamp(workers * 3, 48, 200);
        var before = ThreadPool.ThreadCount;
        var subscribers = new List<NamedPipeClientStream>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var pipe = Connect(name);
                subscribers.Add(pipe);
                Send(pipe, "events");
            }

            using var listener = new Listener(name);
            WaitUntilSubscribed(core, listener);
            var grown = ThreadPool.ThreadCount - before;
            Assert.IsTrue(grown < count / 2, $"The pool grew by {grown} threads for {count} idle subscribers.");
        }
        finally
        {
            foreach (var pipe in subscribers)
                pipe.Dispose();
        }
    }

    private static string PipeName() => "winland-" + Guid.NewGuid().ToString("N");

    private static NamedPipeClientStream Connect(string name)
    {
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        pipe.Connect(2000);
        return pipe;
    }

    private static void Send(Stream pipe, string command)
    {
        var bytes = Encoding.UTF8.GetBytes(command + "\n");
        pipe.Write(bytes);
        pipe.Flush();
    }

    private static void WaitUntilSubscribed(WindowManager core, Listener listener)
    {
        for (var i = 1; i <= 40; i++)
        {
            core.Handle(new WindowAppeared(new WindowId(100 + i), new Rectangle(0, 0, 10, 10)));
            if (listener.TryTake(TimeSpan.FromMilliseconds(500), out _))
                return;
        }

        throw new TimeoutException("The event subscriber did not connect.");
    }

    private static void Sync(WindowManager core, params Listener[] listeners)
    {
        const long marker = 0x270F;
        core.Handle(new WindowAppeared(new WindowId(marker), new Rectangle(0, 0, 10, 10)));
        foreach (var listener in listeners)
        {
            var seen = false;
            while (!seen)
            {
                Assert.IsTrue(listener.TryTake(TimeSpan.FromSeconds(10), out var line), "The marker window was never reported.");
                seen = line == "activewindowv2>>270f";
            }
        }
    }

    private static List<string> Take(Listener listener, int count)
    {
        var lines = new List<string>();
        while (lines.Count < count)
        {
            Assert.IsTrue(listener.TryTake(TimeSpan.FromSeconds(10), out var line), $"Only {lines.Count} of {count} events arrived: {string.Join(", ", lines)}");
            lines.Add(line);
        }

        while (listener.TryTake(TimeSpan.FromMilliseconds(200), out var extra))
            lines.Add(extra);

        return lines;
    }

    private sealed class Listener : IDisposable
    {
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
        private readonly CancellationTokenSource _cancel = new();
        private readonly Task _task;

        public Listener(string pipe, string command = "events") =>
            _task = Task.Run(() => CommandClient.Follow(command, line => _lines.Writer.TryWrite(line), pipe, cancel: _cancel.Token));

        public bool TryTake(TimeSpan timeout, out string line)
        {
            using var cancel = new CancellationTokenSource(timeout);
            try
            {
                line = _lines.Reader.ReadAsync(cancel.Token).AsTask().GetAwaiter().GetResult();
                return true;
            }
            catch (OperationCanceledException)
            {
                line = "";
                return false;
            }
        }

        public void Dispose()
        {
            _cancel.Cancel();
            try
            {
                _task.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _cancel.Dispose();
        }
    }
}
