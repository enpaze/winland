namespace Winland.Infrastructure.Tests;
[TestClass]
public class LoopInvokerTests
{
    private const uint Quit = 0x0012;

    [TestMethod]
    public void AnActionFromAnotherThreadRunsOnTheLoopThread()
    {
        var invoker = new LoopInvoker();
        using var attached = new ManualResetEventSlim();
        var loopThread = 0;
        uint nativeThread = 0;
        var loop = new Thread(() =>
        {
            loopThread = Environment.CurrentManagedThreadId;
            nativeThread = ProbeNative.GetCurrentThreadId();
            _ = Peek();
            invoker.Attach(nativeThread);
            attached.Set();
            while (ProbeNative.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.message == LoopInvoker.Message)
                    invoker.Drain();
            }
        }) { IsBackground = true };
        loop.Start();
        Assert.IsTrue(attached.Wait(TimeSpan.FromSeconds(5)));

        var ranOn = 0;
        invoker.Invoke(() => ranOn = Environment.CurrentManagedThreadId, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        Assert.AreEqual(loopThread, ranOn);
        ProbeNative.PostThreadMessage(nativeThread, Quit, 0, 0);
        Assert.IsTrue(loop.Join(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void ALoopThatDoesNotAnswerRunsTheActionHereOnce()
    {
        var invoker = new LoopInvoker();
        using var attached = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        uint nativeThread = 0;
        var stuck = new Thread(() =>
        {
            nativeThread = ProbeNative.GetCurrentThreadId();
            _ = Peek();
            attached.Set();
            release.Wait();
        }) { IsBackground = true };
        stuck.Start();
        Assert.IsTrue(attached.Wait(TimeSpan.FromSeconds(5)));
        invoker.Attach(nativeThread);

        var runs = 0;
        invoker.Invoke(() => runs++, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, runs);

        invoker.Drain();
        Assert.AreEqual(1, runs);
        release.Set();
        Assert.IsTrue(stuck.Join(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void AnActionTheLoopStartedIsWaitedForPastTheAnswerTime()
    {
        var invoker = new LoopInvoker();
        using var attached = new ManualResetEventSlim();
        uint nativeThread = 0;
        var loop = new Thread(() =>
        {
            nativeThread = ProbeNative.GetCurrentThreadId();
            _ = Peek();
            invoker.Attach(nativeThread);
            attached.Set();
            while (ProbeNative.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.message == LoopInvoker.Message)
                    invoker.Drain();
            }
        }) { IsBackground = true };
        loop.Start();
        Assert.IsTrue(attached.Wait(TimeSpan.FromSeconds(5)));

        var finished = false;
        var runs = 0;
        invoker.Invoke(
            () =>
            {
                runs++;
                Thread.Sleep(500);
                finished = true;
            },
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromSeconds(5));

        Assert.IsTrue(finished);
        Assert.AreEqual(1, runs);
        ProbeNative.PostThreadMessage(nativeThread, Quit, 0, 0);
        Assert.IsTrue(loop.Join(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void AnActionTheLoopStartedIsLeftWhenTheLimitPasses()
    {
        var invoker = new LoopInvoker();
        using var attached = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        uint nativeThread = 0;
        var loop = new Thread(() =>
        {
            nativeThread = ProbeNative.GetCurrentThreadId();
            _ = Peek();
            invoker.Attach(nativeThread);
            attached.Set();
            while (ProbeNative.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.message == LoopInvoker.Message)
                    invoker.Drain();
            }
        }) { IsBackground = true };
        loop.Start();
        Assert.IsTrue(attached.Wait(TimeSpan.FromSeconds(5)));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        invoker.Invoke(() => release.Wait(), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));
        watch.Stop();

        Assert.IsInRange(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(3), watch.Elapsed);
        release.Set();
        ProbeNative.PostThreadMessage(nativeThread, Quit, 0, 0);
        Assert.IsTrue(loop.Join(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void WithNoLoopTheActionRunsHere()
    {
        var invoker = new LoopInvoker();
        var runs = 0;

        invoker.Invoke(() => runs++, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        Assert.AreEqual(1, runs);
    }

    [TestMethod]
    public void AnActionPostedOnTheLoopThreadWaitsForTheLoop()
    {
        var invoker = new LoopInvoker();
        _ = Peek();
        invoker.Attach(ProbeNative.GetCurrentThreadId());
        try
        {
            var runs = 0;
            invoker.Post(() => runs++);
            Assert.AreEqual(0, runs);

            invoker.Drain();
            Assert.AreEqual(1, runs);
        }
        finally
        {
            invoker.Detach();
            while (Peek())
            {
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void APostedFailureIsLoggedOnceAndDoesNotStopTheQueue(bool missingLoop)
    {
        var invoker = new LoopInvoker();
        _ = Peek();
        invoker.Attach(missingLoop ? uint.MaxValue : ProbeNative.GetCurrentThreadId());
        var stderr = Console.Error;
        using var log = new StringWriter();
        Console.SetError(log);
        try
        {
            var poster = new Thread(() => invoker.Post(() => throw new InvalidOperationException("posted failed")));
            poster.Start();
            Assert.IsTrue(poster.Join(TimeSpan.FromSeconds(5)));
            if (!missingLoop)
                Assert.IsEmpty(log.ToString());

            invoker.Drain();
            invoker.Drain();

            Assert.Contains("System.InvalidOperationException: posted failed", log.ToString(), StringComparison.Ordinal);
            Assert.Contains(nameof(APostedFailureIsLoggedOnceAndDoesNotStopTheQueue), log.ToString(), StringComparison.Ordinal);
            Assert.AreEqual(2, log.ToString().Split("posted failed", StringSplitOptions.None).Length);
            var runs = 0;
            invoker.Detach();
            invoker.Post(() => runs++);
            Assert.AreEqual(1, runs);
        }
        finally
        {
            Console.SetError(stderr);
            invoker.Detach();
        }
    }

    private static bool Peek() => ProbeNative.PeekMessage(out _, 0, 0, 0, 1);
}
