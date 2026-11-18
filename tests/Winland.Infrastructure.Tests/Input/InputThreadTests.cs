using System.Collections.Concurrent;
using Windows.Win32;

namespace Winland.Infrastructure.Tests;

[TestClass]
public class InputThreadTests
{
    private const uint Probe = 0x8000 + 90;

    [TestMethod]
    public void StartRunsOnItsOwnThreadAndReturnsOnceStarted()
    {
        var caller = Environment.CurrentManagedThreadId;
        int? started = null;
        using var thread = new InputThread(() => started = Environment.CurrentManagedThreadId, _ => false, () => { });

        thread.Start();

        Assert.IsNotNull(started);
        Assert.AreNotEqual(caller, started);
    }

    [TestMethod]
    public void AFailedStartIsThrownToTheCaller()
    {
        using var thread = new InputThread(() => throw new InvalidOperationException("no hook"), _ => false, () => { });

        var error = Assert.ThrowsExactly<InvalidOperationException>(thread.Start);

        Assert.AreEqual("no hook", error.Message);
    }

    [TestMethod]
    public void ThreadMessagesAreServicedOnTheInputThread()
    {
        int? startedOn = null;
        var serviced = new BlockingCollection<(uint Message, int Thread)>();
        using var thread = new InputThread(
            () => startedOn = Environment.CurrentManagedThreadId,
            message =>
            {
                serviced.Add((message, Environment.CurrentManagedThreadId));
                return true;
            },
            () => { });
        thread.Start();

        Assert.IsTrue(PInvoke.PostThreadMessage(thread.Id, Probe, default, default));

        Assert.IsTrue(serviced.TryTake(out var seen, TimeSpan.FromSeconds(10)));
        Assert.AreEqual(Probe, seen.Message);
        Assert.AreEqual(startedOn, seen.Thread);
    }

    [TestMethod]
    public void DisposeStopsOnTheInputThread()
    {
        int? startedOn = null;
        int? stoppedOn = null;
        var thread = new InputThread(
            () => startedOn = Environment.CurrentManagedThreadId,
            _ => false,
            () => stoppedOn = Environment.CurrentManagedThreadId);
        thread.Start();

        thread.Dispose();

        Assert.IsNotNull(stoppedOn);
        Assert.AreEqual(startedOn, stoppedOn);
    }
}
