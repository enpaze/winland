namespace Winland.Infrastructure.Tests;

[TestClass]
public class SessionNoticeTests
{
    [TestMethod]
    public void OnlyAnEndSessionThatIsTrueEndsTheSession()
    {
        Assert.IsTrue(SessionNotices.EndsSession(SessionNotices.EndSessionMessage, 1));
        Assert.IsFalse(SessionNotices.EndsSession(SessionNotices.EndSessionMessage, 0));
        Assert.IsFalse(SessionNotices.EndsSession(0x0011, 1));
    }

    [TestMethod]
    public void PowerBroadcastMapsSuspendAndAutomaticResume()
    {
        Assert.AreEqual(SessionSignal.Suspended, Power(SessionNotices.Suspend));
        Assert.AreEqual(SessionSignal.Resumed, Power(SessionNotices.ResumeAutomatic));
        Assert.IsFalse(SessionNotices.TryPower(0x0007, out _));
        Assert.IsFalse(SessionNotices.TryFromMessage(SessionNotices.PowerMessage, SessionNotices.PowerSettingChange, out _));
    }

    [TestMethod]
    public void SessionChangeMapsLockUnlockConsoleAndRemote()
    {
        Assert.AreEqual(SessionSignal.ConsoleConnected, Session(1));
        Assert.AreEqual(SessionSignal.ConsoleDisconnected, Session(2));
        Assert.AreEqual(SessionSignal.RemoteConnected, Session(3));
        Assert.AreEqual(SessionSignal.RemoteDisconnected, Session(4));
        Assert.AreEqual(SessionSignal.Locked, Session(7));
        Assert.AreEqual(SessionSignal.Unlocked, Session(8));
        Assert.IsFalse(SessionNotices.TrySession(5, out _));
        Assert.IsFalse(SessionNotices.TrySession(6, out _));
    }

    [TestMethod]
    public void DisplayPowerMapsTheConsoleAndTheMonitor()
    {
        Assert.AreEqual(SessionSignal.DisplayOff, Display(SessionNotices.ConsoleDisplayState, 0));
        Assert.AreEqual(SessionSignal.DisplayOn, Display(SessionNotices.ConsoleDisplayState, 1));
        Assert.AreEqual(SessionSignal.DisplayOn, Display(SessionNotices.ConsoleDisplayState, 2));
        Assert.AreEqual(SessionSignal.DisplayOff, Display(SessionNotices.MonitorPowerOn, 0));
        Assert.AreEqual(SessionSignal.DisplayOn, Display(SessionNotices.MonitorPowerOn, 1));
        Assert.IsFalse(SessionNotices.TryDisplayPower(SessionNotices.ConsoleDisplayState, 3, out _));
        Assert.IsFalse(SessionNotices.TryDisplayPower(SessionNotices.MonitorPowerOn, 2, out _));
        Assert.IsFalse(SessionNotices.TryDisplayPower(Guid.Empty, 1, out _));
    }

    private static SessionSignal Power(nuint code)
    {
        Assert.IsTrue(SessionNotices.TryPower(code, out var signal));
        return signal;
    }

    private static SessionSignal Session(nuint reason)
    {
        Assert.IsTrue(SessionNotices.TrySession(reason, out var signal));
        return signal;
    }

    private static SessionSignal Display(Guid setting, int value)
    {
        Assert.IsTrue(SessionNotices.TryDisplayPower(setting, value, out var signal));
        return signal;
    }

    [TestMethod]
    public void ASessionStartedRemotelyRecoversWhenTheConsoleConnects()
    {
        var phase = new SessionPhase();
        foreach (var signal in SessionNotices.AtStart(remote: true))
            phase.Notice(signal);

        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.ShellReady));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.RemoteDisconnected));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.ConsoleConnected));
        Assert.IsEmpty(SessionNotices.AtStart(remote: false));
    }
}
