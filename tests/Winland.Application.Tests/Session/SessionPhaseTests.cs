namespace Winland.Application.Tests;

[TestClass]
public class SessionPhaseTests
{
    [TestMethod]
    public void TheFirstLayoutWaitsUntilTheShellIsReady()
    {
        var phase = new SessionPhase();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.ShellReady));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ShellReady));
    }

    [TestMethod]
    [DataRow(SessionSignal.Locked, SessionSignal.Unlocked)]
    [DataRow(SessionSignal.Suspended, SessionSignal.Resumed)]
    [DataRow(SessionSignal.ConsoleDisconnected, SessionSignal.ConsoleConnected)]
    [DataRow(SessionSignal.DisplayOff, SessionSignal.DisplayOn)]
    public void TheSessionIsLiveExceptWhileItIsAway(SessionSignal away, SessionSignal back)
    {
        var phase = new SessionPhase();
        phase.Notice(SessionSignal.ShellReady);
        Assert.IsTrue(phase.Live);

        phase.Notice(away);
        Assert.IsFalse(phase.Live);

        phase.Notice(back);
        Assert.IsTrue(phase.Live);
    }

    [TestMethod]
    public void TheFirstLayoutRunsWhenTheShellWaitTimesOut()
    {
        var phase = new SessionPhase();

        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.ShellTimedOut));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ShellTimedOut));
    }

    [TestMethod]
    public void AShellThatAppearsAfterTheTimeoutLaysOutAgain()
    {
        var phase = new SessionPhase();
        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.ShellTimedOut));

        Assert.AreEqual(SessionEffect.Reflow, phase.Notice(SessionSignal.ShellReady));
    }

    [TestMethod]
    public void SuspendHoldsTheFirstLayoutUntilResume()
    {
        var phase = new SessionPhase();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Suspended));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ShellReady));
        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.Resumed));
    }

    [TestMethod]
    public void ResumeAfterALayoutRecoversTheSession()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Suspended));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.Resumed));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Resumed));
    }

    [TestMethod]
    public void ResumeWhileLockedWaitsForUnlock()
    {
        var phase = LaidOut();
        phase.Notice(SessionSignal.Locked);
        phase.Notice(SessionSignal.Suspended);

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Resumed));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.Unlocked));
    }

    [TestMethod]
    public void LockHidesChangesUntilUnlock()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Locked));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.Unlocked));
    }

    [TestMethod]
    public void TheFirstLayoutWaitsForAnUnlock()
    {
        var phase = new SessionPhase();
        phase.Notice(SessionSignal.Locked);

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ShellReady));
        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.Unlocked));
    }

    [TestMethod]
    public void AShellTimeoutWhileLockedWaitsForUnlock()
    {
        var phase = new SessionPhase();
        phase.Notice(SessionSignal.Locked);

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ShellTimedOut));
        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.Unlocked));
    }

    [TestMethod]
    public void AnUnlockWithoutALockDoesNotLayOutAgain()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Unlocked));
    }

    [TestMethod]
    public void AConsoleReconnectRecoversTheSession()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ConsoleDisconnected));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.ConsoleConnected));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ConsoleConnected));
    }

    [TestMethod]
    public void ARemoteConnectRecoversTheSession()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.RemoteConnected));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.RemoteDisconnected));
        Assert.AreEqual(SessionEffect.Reflow, phase.Notice(SessionSignal.DisplayChanged));
    }

    [TestMethod]
    public void ARemoteDisconnectLeavesTheSessionUntilItReconnects()
    {
        var phase = LaidOut();
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.RemoteConnected));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.ConsoleDisconnected));

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.RemoteDisconnected));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.RemoteConnected));
    }

    [TestMethod]
    public void ADisplayChangeReflowsAfterTheFirstLayout()
    {
        var phase = new SessionPhase();
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        phase.Notice(SessionSignal.ShellReady);

        Assert.AreEqual(SessionEffect.Reflow, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.Reflow, phase.Notice(SessionSignal.DisplayChanged));
    }

    [TestMethod]
    public void DisplayPowerOnRecoversTheSession()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayOff));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.DisplayOn));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayOn));
    }

    [TestMethod]
    public void ResumeRecoversEvenIfTheDisplayWasOff()
    {
        var phase = LaidOut();
        phase.Notice(SessionSignal.DisplayOff);
        phase.Notice(SessionSignal.Suspended);

        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.Resumed));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.DisplayOn));
    }

    [TestMethod]
    public void ADisplayChangeWhileSuspendedWaitsForResume()
    {
        var phase = LaidOut();
        phase.Notice(SessionSignal.Suspended);

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.DisplayChanged));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.Resumed));
    }

    [TestMethod]
    public void ASecondSuspendDoesNotChangeThePhase()
    {
        var phase = LaidOut();

        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Suspended));
        Assert.AreEqual(SessionEffect.None, phase.Notice(SessionSignal.Suspended));
        Assert.AreEqual(SessionEffect.Recover, phase.Notice(SessionSignal.Resumed));
    }

    [TestMethod]
    public void TheShellWaitIsBounded()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(15), SessionPhase.ShellWait);
    }

    private static SessionPhase LaidOut()
    {
        var phase = new SessionPhase();
        Assert.AreEqual(SessionEffect.LayOut, phase.Notice(SessionSignal.ShellReady));
        return phase;
    }
}
