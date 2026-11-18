using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Infrastructure.Tests;
[TestClass]
public class WindowOpacityTests
{
    [TestMethod]
    public void AlphaBelowOpaqueAddsLayeredAndResetRestoresTheOriginalStyle()
    {
        var styles = new FakeStyles();
        var window = new WindowId(42);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);

        adapter.Apply(window, 255);
        Assert.AreEqual(0, styles.Writes);
        Assert.IsFalse(adapter.Owns(window));

        adapter.Apply(window, 128);
        Assert.AreEqual((uint)WINDOW_EX_STYLE.WS_EX_LAYERED | 0x80, styles.ExStyle[window]);
        Assert.AreEqual(128, styles.Alpha[window]);
        Assert.AreEqual(1, styles.Writes);
        Assert.IsTrue(adapter.Owns(window));

        adapter.Apply(window, 64);
        Assert.AreEqual((uint)WINDOW_EX_STYLE.WS_EX_LAYERED | 0x80, styles.ExStyle[window]);
        Assert.AreEqual(64, styles.Alpha[window]);
        Assert.AreEqual(1, styles.Writes);

        adapter.Reset(window);
        Assert.AreEqual(0x80u, styles.ExStyle[window]);
        Assert.IsFalse(adapter.Owns(window));
    }

    [TestMethod]
    public void ResetTakesOffOnlyTheLayeredStyle()
    {
        var styles = new FakeStyles();
        var window = new WindowId(42);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(window, 128);
        styles.ExStyle[window] |= 0x8;

        adapter.Reset(window);

        Assert.AreEqual(0x88u, styles.ExStyle[window]);
    }

    [TestMethod]
    public void ReturningToOpaqueRestoresTheStyleWithoutASeparateReset()
    {
        var styles = new FakeStyles();
        var window = new WindowId(7);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);

        adapter.Apply(window, 100);
        adapter.Apply(window, 255);

        Assert.AreEqual(0x80u, styles.ExStyle[window]);
        Assert.IsFalse(adapter.Owns(window));
    }

    [TestMethod]
    public void AnAlreadyLayeredWindowIsLeftAloneAndReportedOnce()
    {
        var styles = new FakeStyles();
        var window = new WindowId(42);
        styles.ExStyle[window] = (uint)WINDOW_EX_STYLE.WS_EX_LAYERED | 0x80;
        styles.Alpha[window] = 200;
        styles.ColorKey.Add(window);
        var adapter = new WindowOpacity(styles);

        var stderr = Console.Error;
        using var log = new StringWriter();
        Console.SetError(log);
        try
        {
            adapter.Apply(window, 10);
            adapter.Apply(window, 10);
            Assert.AreEqual("Window 0x2a was already layered and was left alone." + Environment.NewLine, log.ToString());
        }
        finally
        {
            Console.SetError(stderr);
        }

        Assert.AreEqual(200, styles.Alpha[window]);
        Assert.AreEqual(0, styles.Writes);
        Assert.AreEqual(0, styles.AlphaWrites);
        Assert.IsFalse(adapter.Owns(window));
    }

    [TestMethod]
    public void AWindowThatCannotBeLayeredIsNotTriedAgain()
    {
        var styles = new FakeStyles { FailAlpha = true };
        var window = new WindowId(3);
        styles.ExStyle[window] = 0;
        var adapter = new WindowOpacity(styles);

        adapter.Apply(window, 128);
        var writes = styles.Writes;
        var alphaWrites = styles.AlphaWrites;
        adapter.Apply(window, 128);

        Assert.AreEqual(writes, styles.Writes);
        Assert.AreEqual(alphaWrites, styles.AlphaWrites);
        Assert.AreEqual(0u, styles.ExStyle[window]);
        Assert.IsFalse(adapter.Owns(window));
        Assert.IsTrue(adapter.Failed(window));
    }

    [TestMethod]
    public void StampRecordsAddedLayeredWindowsAndReleaseClearsTheBit()
    {
        var styles = new FakeStyles();
        var window = new WindowId(5);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(window, 128);

        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(window, WorkspaceId.Initial, MonitorId.Primary, false, "Notepad", "Notes", 10),
        ]);
        var stamped = adapter.Stamp(snapshot);
        Assert.IsTrue(Assert.ContainsSingle(stamped.Clients).Layered);

        var restarted = new WindowOpacity(styles);
        restarted.ReleaseAdded(window);
        Assert.AreEqual(0x80u, styles.ExStyle[window]);
    }

    [TestMethod]
    public void ReleasingARecoveredHungWindowDefersTheStyleWriteAndKeepsItRecorded()
    {
        var styles = new FakeStyles();
        var window = new WindowId(5);
        styles.ExStyle[window] = (uint)WINDOW_EX_STYLE.WS_EX_LAYERED | 0x80;
        styles.Alpha[window] = 0;
        var hung = true;
        var adapter = new WindowOpacity(styles, _ => hung);

        adapter.ReleaseAdded(window);

        Assert.AreEqual(0, styles.Writes);
        Assert.AreEqual(255, styles.Alpha[window]);
        Assert.IsTrue(adapter.Owns(window));
        Assert.AreEqual(window, Assert.ContainsSingle(adapter.Stamp(new RecoverySnapshot([])).Held).Window);

        hung = false;
        adapter.ReleaseAdded(window);

        Assert.AreEqual(0x80u, styles.ExStyle[window]);
        Assert.IsFalse(adapter.Owns(window));
        Assert.IsEmpty(adapter.Stamp(new RecoverySnapshot([])).Held);
    }

    [TestMethod]
    public void ResetAllLeavesAWindowThatStoppedAnsweringOpaqueAndRecorded()
    {
        var styles = new FakeStyles();
        var window = new WindowId(5);
        styles.ExStyle[window] = 0x80;
        var stopped = false;
        var adapter = new WindowOpacity(styles, _ => stopped);
        adapter.Apply(window, 128);
        stopped = true;
        var writes = styles.Writes;

        adapter.ResetAll();

        Assert.AreEqual(writes, styles.Writes);
        Assert.AreEqual(255, styles.Alpha[window]);
        var clean = new RecoverySnapshot(
        [
            new RecoveredClient(window, WorkspaceId.Initial, MonitorId.Primary, false, "Notepad", "Notes", 10),
        ]) { Clean = true };
        var stamped = adapter.Stamp(clean);
        Assert.IsTrue(Assert.ContainsSingle(stamped.Clients).Layered);
        Assert.IsTrue(stamped.Clean);
    }

    [TestMethod]
    public void StampRecordsAHeldWindowThatIsNoClient()
    {
        var styles = new FakeStyles();
        var client = new WindowId(5);
        var held = new WindowId(6);
        styles.ExStyle[client] = 0x80;
        styles.ExStyle[held] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(client, 128);
        adapter.Apply(held, 0);

        var stamped = adapter.Stamp(new RecoverySnapshot(
        [
            new RecoveredClient(client, WorkspaceId.Initial, MonitorId.Primary, false, "Notepad", "Notes", 10),
        ]));

        Assert.AreSequenceEqual([held], stamped.Held.Select(window => window.Window));
        Assert.IsTrue(Assert.ContainsSingle(stamped.Clients).Layered);
    }

    [TestMethod]
    public void ResetAllRestoresEveryOwnedWindow()
    {
        var styles = new FakeStyles();
        var first = new WindowId(1);
        var second = new WindowId(2);
        styles.ExStyle[first] = 0x80;
        styles.ExStyle[second] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(first, 128);
        adapter.Apply(second, 64);

        adapter.ResetAll();

        Assert.AreEqual(0x80u, styles.ExStyle[first]);
        Assert.AreEqual(0x80u, styles.ExStyle[second]);
        Assert.IsFalse(adapter.Owns(first));
        Assert.IsFalse(adapter.Owns(second));
    }

    [TestMethod]
    public void AfterResetAllNoWindowIsMadeLayered()
    {
        var styles = new FakeStyles();
        var window = new WindowId(3);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.ResetAll();

        adapter.Apply(window, 128);

        Assert.AreEqual(0x80u, styles.ExStyle[window]);
        Assert.IsFalse(adapter.Owns(window));
    }

    [TestMethod]
    public void AWindowThatRefusesTheLayeredStyleIsNotOwned()
    {
        var styles = new FakeStyles { FailStyle = true };
        var window = new WindowId(4);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);

        adapter.Apply(window, 128);

        Assert.IsFalse(adapter.Owns(window));
        Assert.IsTrue(adapter.Failed(window));
        Assert.AreEqual(0, styles.AlphaWrites);
    }

    [TestMethod]
    public void AResetForgetsAFailureAndAWindowLeftAlone()
    {
        var styles = new FakeStyles { FailStyle = true };
        var failed = new WindowId(4);
        var layered = new WindowId(5);
        styles.ExStyle[failed] = 0x80;
        styles.ExStyle[layered] = (uint)WINDOW_EX_STYLE.WS_EX_LAYERED;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(failed, 128);
        adapter.Apply(layered, 128);

        adapter.Reset(failed);
        adapter.Reset(layered);
        styles.FailStyle = false;
        styles.ExStyle[layered] = 0x80;
        adapter.Apply(failed, 128);
        adapter.Apply(layered, 128);

        Assert.IsFalse(adapter.Failed(failed));
        Assert.IsTrue(adapter.Owns(failed));
        Assert.IsTrue(adapter.Owns(layered));
    }

    [TestMethod]
    public void RestoringTheStyleSetsNoAlpha()
    {
        var styles = new FakeStyles();
        var window = new WindowId(6);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(window, 128);
        var alphaWrites = styles.AlphaWrites;

        adapter.Reset(window);

        Assert.AreEqual(alphaWrites, styles.AlphaWrites);
        Assert.AreEqual(0x80u, styles.ExStyle[window]);
    }

    [TestMethod]
    public void StampChangesOnlyTheMarksThatAreWrong()
    {
        var styles = new FakeStyles();
        var window = new WindowId(5);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        var right = new RecoverySnapshot(
        [
            new RecoveredClient(window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { Layered = true },
            new RecoveredClient(new WindowId(9), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
        ]);
        var wrong = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(10), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { Layered = true },
            new RecoveredClient(new WindowId(11), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { Layered = true },
        ]);
        var beforeAny = adapter.Stamp(wrong);
        adapter.Apply(window, 128);

        var unchanged = adapter.Stamp(right);
        var corrected = adapter.Stamp(wrong);

        Assert.AreSame(wrong, beforeAny);
        Assert.AreSame(right, unchanged);
        foreach (var client in corrected.Clients)
{
    Assert.IsFalse(client.Layered);
}
        Assert.ThrowsExactly<ArgumentNullException>(() => adapter.Stamp(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WindowOpacity(null!));
    }

    [TestMethod]
    public void ReleasingForgetsTheWindowAndLeavesAnUnlayeredOneAlone()
    {
        var styles = new FakeStyles { FailStyle = true };
        var owned = new WindowId(1);
        var failed = new WindowId(2);
        var plain = new WindowId(3);
        styles.ExStyle[failed] = 0x80;
        styles.ExStyle[plain] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(failed, 128);
        styles.FailStyle = false;
        styles.ExStyle[owned] = 0x80;
        adapter.Apply(owned, 128);
        var writes = styles.Writes;

        adapter.ReleaseAdded(failed);
        adapter.ReleaseAdded(plain);
        adapter.ReleaseAdded(new WindowId(99));
        var plainWrites = styles.Writes - writes;
        adapter.ReleaseAdded(owned);

        Assert.AreEqual(0, plainWrites);
        Assert.IsFalse(adapter.Failed(failed));
        Assert.IsFalse(adapter.Owns(owned));
        Assert.AreEqual(0x80u, styles.ExStyle[owned]);
    }

    [TestMethod]
    public void AForgottenWindowIsNotTouchedAndStartsOverIfItComesBack()
    {
        var styles = new FakeStyles();
        var window = new WindowId(11);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(window, 0);
        var writes = styles.Writes;

        adapter.Forget(window);

        Assert.AreEqual(writes, styles.Writes);
        Assert.IsFalse(adapter.Owns(window));
        Assert.AreEqual(255, adapter.Wanted(window));
    }

    [TestMethod]
    public void AVeiledWindowIsTransparentAndKeepsTheAlphaItIsGivenForLater()
    {
        var styles = new FakeStyles();
        var window = new WindowId(9);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);
        adapter.Apply(window, 200);

        Assert.IsTrue(adapter.Veil(window));
        Assert.AreEqual(0, styles.Alpha[window]);
        Assert.AreEqual(200, adapter.Wanted(window));

        adapter.Apply(window, 150);
        Assert.AreEqual(0, styles.Alpha[window]);
        Assert.AreEqual(150, adapter.Wanted(window));

        adapter.Unveil(window);
        Assert.AreEqual(150, styles.Alpha[window]);
        Assert.AreEqual(150, adapter.Wanted(window));
    }

    [TestMethod]
    public void AnOpaqueWindowIsLayeredToBeVeiledAndGetsItsStyleBackAfter()
    {
        var styles = new FakeStyles();
        var window = new WindowId(10);
        styles.ExStyle[window] = 0x80;
        var adapter = new WindowOpacity(styles);

        Assert.IsTrue(adapter.Veil(window));
        Assert.AreEqual((uint)WINDOW_EX_STYLE.WS_EX_LAYERED | 0x80, styles.ExStyle[window]);
        Assert.AreEqual(255, adapter.Wanted(window));

        adapter.Unveil(window);
        Assert.AreEqual(0x80u, styles.ExStyle[window]);
        Assert.IsFalse(adapter.Owns(window));

        adapter.Unveil(window);
        Assert.AreEqual(0x80u, styles.ExStyle[window]);
    }

    [TestMethod]
    public void AWindowItsProgramLayeredCannotBeVeiled()
    {
        var styles = new FakeStyles();
        var window = new WindowId(11);
        styles.ExStyle[window] = (uint)WINDOW_EX_STYLE.WS_EX_LAYERED;
        styles.Alpha[window] = 255;
        var adapter = new WindowOpacity(styles);

        Assert.IsFalse(adapter.Veil(window));
        Assert.AreEqual(255, styles.Alpha[window]);
        Assert.AreEqual(0, styles.AlphaWrites);
    }

    [TestMethod]
    public void NoWindowIsVeiledAfterResetAllAndAResetForgetsTheVeil()
    {
        var styles = new FakeStyles();
        var first = new WindowId(12);
        var second = new WindowId(13);
        styles.ExStyle[first] = 0;
        styles.ExStyle[second] = 0;
        var adapter = new WindowOpacity(styles);

        Assert.IsTrue(adapter.Veil(first));
        adapter.Reset(first);
        Assert.AreEqual(0u, styles.ExStyle[first]);
        Assert.AreEqual(255, adapter.Wanted(first));

        adapter.ResetAll();
        Assert.IsFalse(adapter.Veil(second));
        Assert.AreEqual(0u, styles.ExStyle[second]);
    }

    private sealed class FakeStyles : IWindowStyles
    {
        public Dictionary<WindowId, uint> ExStyle { get; } = [];

        public Dictionary<WindowId, byte> Alpha { get; } = [];

        public HashSet<WindowId> ColorKey { get; } = [];

        public bool FailAlpha { get; init; }

        public bool FailStyle { get; set; }

        public int Writes { get; private set; }

        public int AlphaWrites { get; private set; }

        public bool TryRead(WindowId window, out WindowChrome chrome)
        {
            chrome = default;
            if (!ExStyle.TryGetValue(window, out var style))
                return false;

            chrome = new WindowChrome(style, ColorKey.Contains(window), Alpha.GetValueOrDefault(window));
            return true;
        }

        public bool TryWriteExStyle(WindowId window, uint exStyle)
        {
            Writes++;
            if (FailStyle)
                return false;

            ExStyle[window] = exStyle;
            return true;
        }

        public bool TrySetAlpha(WindowId window, byte alpha)
        {
            AlphaWrites++;
            if (FailAlpha)
                return false;

            Alpha[window] = alpha;
            return true;
        }
    }
}
