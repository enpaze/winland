namespace Winland.Infrastructure.Tests;

[TestClass]
public class BorderlessFrameTests
{
    [TestMethod]
    public void ModeTwoDropsTheFrameAndLeavingRestoresIt()
    {
        var frames = new FakeFrames();
        var window = new WindowId(9);
        frames.Style[window] = 0x00CF0000;
        var borderless = new BorderlessFrame(frames);

        borderless.Apply(window, true);

        Assert.AreEqual(BorderlessStyle.Strip(0x00CF0000), frames.Style[window]);
        Assert.IsTrue(borderless.Owns(window));
        Assert.AreEqual(1, frames.Writes);

        borderless.Apply(window, true);
        Assert.AreEqual(1, frames.Writes);

        borderless.Apply(window, false);
        Assert.AreEqual(0x00CF0000u, frames.Style[window]);
        Assert.IsFalse(borderless.Owns(window));
    }

    [TestMethod]
    public void LeavingPutsBackOnlyTheFrameAndKeepsWhatChangedSince()
    {
        const uint Visible = 0x10000000;
        const uint Disabled = 0x08000000;
        var frames = new FakeFrames();
        var window = new WindowId(9);
        var crashed = new WindowId(10);
        frames.Style[window] = 0x00CF0000 | Visible;
        frames.Style[crashed] = BorderlessStyle.Strip(0x00CF0000) | Disabled;
        var borderless = new BorderlessFrame(frames);
        borderless.Apply(window, true);
        frames.Style[window] = (frames.Style[window] & ~Visible) | Disabled;

        borderless.Apply(window, false);
        borderless.PutBack(crashed, 0x00CF0000 | Visible);

        Assert.AreEqual(0x00CF0000u | Disabled, frames.Style[window]);
        Assert.AreEqual(0x00CF0000u | Disabled, frames.Style[crashed]);
    }

    [TestMethod]
    public void ReleasingAllSkipsAWindowThatStoppedAnsweringAndKeepsItForTheNextStart()
    {
        var frames = new FakeFrames();
        var hung = new WindowId(4);
        var answering = new WindowId(5);
        frames.Style[hung] = 0x00CF0000;
        frames.Style[answering] = 0x00CF0000;
        var stopped = false;
        var borderless = new BorderlessFrame(frames, window => stopped && window == hung);
        borderless.Apply(hung, true);
        borderless.Apply(answering, true);
        stopped = true;

        borderless.ReleaseAll();

        Assert.AreEqual(BorderlessStyle.Strip(0x00CF0000), frames.Style[hung]);
        Assert.AreEqual(0x00CF0000u, frames.Style[answering]);
        var snapshot = borderless.Stamp(new RecoverySnapshot([Client(hung), Client(answering)]));
        Assert.AreSequenceEqual([0x00CF0000u, null], snapshot.Clients.Select(client => client.FrameStyle));
    }

    [TestMethod]
    public void AWindowThatCannotChangeStyleIsNotRemembered()
    {
        var frames = new FakeFrames { FailWrite = true };
        var window = new WindowId(3);
        frames.Style[window] = 0x00CF0000;
        var borderless = new BorderlessFrame(frames);

        borderless.Apply(window, true);

        Assert.IsFalse(borderless.Owns(window));
        Assert.AreEqual(0x00CF0000u, frames.Style[window]);
    }

    [TestMethod]
    public void ReleasingAllPutsEveryFrameBack()
    {
        var frames = new FakeFrames();
        var first = new WindowId(4);
        var second = new WindowId(5);
        frames.Style[first] = 0x00CF0000;
        frames.Style[second] = 0x00C40000;
        var borderless = new BorderlessFrame(frames);
        borderless.Apply(first, true);
        borderless.Apply(second, true);

        borderless.ReleaseAll();

        Assert.AreEqual(0x00CF0000u, frames.Style[first]);
        Assert.AreEqual(0x00C40000u, frames.Style[second]);
        Assert.IsFalse(borderless.Owns(first));
        Assert.IsFalse(borderless.Owns(second));
    }

    [TestMethod]
    public void AfterReleasingAllNoFrameIsDropped()
    {
        var frames = new FakeFrames();
        var window = new WindowId(8);
        frames.Style[window] = 0x00CF0000;
        var borderless = new BorderlessFrame(frames);
        borderless.ReleaseAll();

        borderless.Apply(window, true);

        Assert.AreEqual(0x00CF0000u, frames.Style[window]);
        Assert.IsFalse(borderless.Owns(window));
    }

    [TestMethod]
    public void TheOriginalStyleIsRecordedAndAfterACrashWrittenBackInsteadOfTheStrippedOne()
    {
        var frames = new FakeFrames();
        var window = new WindowId(6);
        var other = new WindowId(7);
        frames.Style[window] = 0x00CF0000;
        var borderless = new BorderlessFrame(frames);
        borderless.Apply(window, true);

        var stamped = borderless.Stamp(new RecoverySnapshot(
        [
            new RecoveredClient(window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
            new RecoveredClient(other, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
        ]));
        Assert.AreEqual(0x00CF0000u, stamped.Clients[0].FrameStyle);
        Assert.IsNull(stamped.Clients[1].FrameStyle);

        var restarted = new BorderlessFrame(frames);
        restarted.PutBack(window, stamped.Clients[0].FrameStyle!.Value);
        Assert.AreEqual(0x00CF0000u, frames.Style[window]);

        restarted.Apply(window, true);
        restarted.Apply(window, false);
        Assert.AreEqual(0x00CF0000u, frames.Style[window]);
    }

    [TestMethod]
    [DataRow(0x00CF0000u, 0x800B0000u)]
    [DataRow(0x10000000u, 0x90000000u)]
    [DataRow(0x80000000u, 0x80000000u)]
    [DataRow(0x16CF0001u, 0x960B0001u)]
    public void StrippingKeepsEveryBitButTheFrame(uint style, uint stripped)
    {
        Assert.AreEqual(stripped, BorderlessStyle.Strip(style));
    }

    [TestMethod]
    public void AWindowWithoutAFrameIsRememberedButNotWritten()
    {
        var frames = new FakeFrames();
        var window = new WindowId(2);
        frames.Style[window] = BorderlessStyle.Popup;
        var borderless = new BorderlessFrame(frames);

        borderless.Apply(window, true);

        Assert.AreEqual(0, frames.Writes);
        Assert.IsTrue(borderless.Owns(window));
    }

    [TestMethod]
    public void AWindowWhoseStyleCannotBeReadIsLeftAlone()
    {
        var frames = new FakeFrames();
        var window = new WindowId(2);
        var borderless = new BorderlessFrame(frames);

        borderless.Apply(window, true);

        Assert.AreEqual(0, frames.Writes);
        Assert.IsFalse(borderless.Owns(window));
    }

    [TestMethod]
    public void PuttingBackForgetsWhatThisRunDropped()
    {
        var frames = new FakeFrames();
        var window = new WindowId(6);
        frames.Style[window] = 0x00CF0000;
        var borderless = new BorderlessFrame(frames);
        borderless.Apply(window, true);

        borderless.PutBack(window, 0x00C00000);
        var writes = frames.Writes;
        borderless.Release(window);

        Assert.IsFalse(borderless.Owns(window));
        Assert.AreEqual(writes, frames.Writes);

        Assert.AreEqual(0x00CB0000u, frames.Style[window]);
    }

    [TestMethod]
    public void StampChangesOnlyTheStylesThatAreWrong()
    {
        var frames = new FakeFrames();
        var window = new WindowId(6);
        frames.Style[window] = 0x00CF0000;
        var borderless = new BorderlessFrame(frames);
        borderless.Apply(window, true);
        var right = new RecoverySnapshot(
        [
            new RecoveredClient(window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { FrameStyle = 0x00CF0000 },
            new RecoveredClient(new WindowId(9), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1),
        ]);
        var wrong = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(10), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { FrameStyle = 1 },
            new RecoveredClient(new WindowId(11), WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1) { FrameStyle = 2 },
        ]);

        var unchanged = borderless.Stamp(right);
        var corrected = borderless.Stamp(wrong);

        Assert.AreSame(right, unchanged);
        foreach (var client in corrected.Clients)
{
    Assert.IsNull(client.FrameStyle);
}
        Assert.ThrowsExactly<ArgumentNullException>(() => borderless.Stamp(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BorderlessFrame(null!));
    }

    private static RecoveredClient Client(WindowId window) =>
        new(window, WorkspaceId.Initial, MonitorId.Primary, false, "", "", 1);

    private sealed class FakeFrames : IWindowFrame
    {
        internal readonly Dictionary<WindowId, uint> Style = [];
        internal int Writes;
        internal bool FailWrite;

        public bool TryReadStyle(WindowId window, out uint style) => Style.TryGetValue(window, out style);

        public bool TryWriteStyle(WindowId window, uint style)
        {
            Writes++;
            if (FailWrite)
                return false;

            Style[window] = style;
            return true;
        }
    }
}
