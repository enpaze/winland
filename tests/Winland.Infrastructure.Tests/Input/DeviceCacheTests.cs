namespace Winland.Infrastructure.Tests;

[TestClass]
public class DeviceCacheTests
{
    [TestMethod]
    public void AnOpenedDeviceIsOpenedOnce()
    {
        var opened = 0;
        using var cache = new DeviceCache<Probe>(_ => { opened++; return new Probe(); }, () => 0, retryMs: 1000);

        var first = cache.For(1);

        Assert.AreSame(first, cache.For(1));
        Assert.AreEqual(1, opened);
    }

    [TestMethod]
    public void AFailedOpenIsTriedAgainAfterTheRetryDelay()
    {
        var now = 0L;
        var answers = new Queue<Probe?>([null, new Probe()]);
        var opened = 0;
        using var cache = new DeviceCache<Probe>(_ => { opened++; return answers.Dequeue(); }, () => now, retryMs: 1000);

        Assert.IsNull(cache.For(1));
        now = 999;
        Assert.IsNull(cache.For(1));
        Assert.AreEqual(1, opened);

        now = 1000;
        Assert.IsNotNull(cache.For(1));
        Assert.AreEqual(2, opened);
    }

    [TestMethod]
    public void ARemovedDeviceIsDisposedAndOpenedAfreshWhenItsHandleComesBack()
    {
        var probes = new List<Probe>();
        using var cache = new DeviceCache<Probe>(_ => { var probe = new Probe(); probes.Add(probe); return probe; }, () => 0, retryMs: 1000);
        var first = cache.For(1);

        cache.Remove(1);

        Assert.IsTrue(first!.Disposed);
        Assert.AreNotSame(first, cache.For(1));
    }

    [TestMethod]
    public void ARemovedFailureIsTriedAgainAtOnce()
    {
        var answers = new Queue<Probe?>([null, new Probe()]);
        using var cache = new DeviceCache<Probe>(_ => answers.Dequeue(), () => 0, retryMs: 1000);
        Assert.IsNull(cache.For(1));

        cache.Remove(1);

        Assert.IsNotNull(cache.For(1));
    }

    [TestMethod]
    public void DisposingTheCacheDisposesEveryDevice()
    {
        var cache = new DeviceCache<Probe>(_ => new Probe(), () => 0, retryMs: 1000);
        var first = cache.For(1)!;
        var second = cache.For(2)!;

        cache.Dispose();

        Assert.IsTrue(first.Disposed);
        Assert.IsTrue(second.Disposed);
    }

    private sealed class Probe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
