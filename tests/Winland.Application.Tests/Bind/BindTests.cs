namespace Winland.Application.Tests;

[TestClass]
public class BindTests
{
    [TestMethod]
    public void UnboundKeyPassesThrough()
    {
        var core = new WindowManager();

        AssertPassed(Press(core, "Q"));
        AssertPassed(Release(core, "Q"));
    }

    [TestMethod]
    public void SuperQRunsKillActiveAndAsksToCloseTheActiveClient()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var outcome = Press(core, "Q");

        AssertRan(outcome, 2);
        Assert.AreEqual(2, outcome.State.Clients.Count);
        Assert.Contains(client => client.Window == new WindowId(2), outcome.State.Clients);
    }

    [TestMethod]
    public void KillActiveDoesNotRemoveTheClientUntilTheWindowCloses()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");
        Press(core, "Q");
        Release(core, "Q");

        var closed = core.Handle(new WindowClosed(new WindowId(2)));

        Assert.DoesNotContain(client => client.Window == new WindowId(2), closed.State.Clients);
        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void FocusedClientIsTheOneKillActiveCloses()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowFocused(new WindowId(1)));
        Press(core, "Super_L");

        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void KillActiveWithNoClientRunsAndClosesNothing()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Press(core, "Super_L");

        var outcome = Press(core, "Q");

        AssertRan(outcome, null);
        Assert.IsEmpty(outcome.State.Clients);
    }

    [TestMethod]
    public void UnmanagedFocusIsNotAClientToClose()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        core.Handle(new WindowFocused(new WindowId(99)));
        Press(core, "Super_L");

        AssertRan(Press(core, "Q"), null);
        Assert.Contains(client => client.Window == new WindowId(1), core.Handle(new KeyReleased("Q")).State.Clients);
    }

    [TestMethod]
    public void KeyWithoutTheModifierDoesNotMatch()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void ExtraModifierDoesNotMatch()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Press(core, "Super_L");
        Press(core, "Shift_L");

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    [DataRow("Super_L", "SUPER")]
    [DataRow("Super_R", "SUPER")]
    [DataRow("Alt_L", "ALT")]
    [DataRow("Alt_R", "ALT")]
    [DataRow("Control_L", "CTRL")]
    [DataRow("Control_R", "CTRL")]
    [DataRow("Shift_L", "SHIFT")]
    [DataRow("Shift_R", "SHIFT")]
    public void EachModifierKeyMatchesItsBind(string key, string modifier)
    {
        var core = Load($"bind = {modifier}, Q, killactive");
        Appear(core, 1);
        Press(core, key);

        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void EveryModifierInTheChordIsRequired()
    {
        var core = Load("bind = ALT CTRL SHIFT, Q, killactive");
        Appear(core, 1);
        Press(core, "Alt_L");
        Press(core, "Control_L");

        AssertPassed(Press(core, "Q"));
        Release(core, "Q");

        Press(core, "Shift_R");
        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void BindWithNoModifiersDoesNotMatchWhileAModifierIsHeld()
    {
        var core = Load("bind = , Q, killactive");
        Appear(core, 1);

        AssertRan(Press(core, "Q"), 1);

        Release(core, "Q");
        Press(core, "Super_L");
        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void BoundSuperComboConsumesTheKeyAndPassesTheSuperRelease()
    {
        var core = Load("bind = SUPER, L, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertRan(Press(core, "L"), 1);
        AssertCaught(Release(core, "L"));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void UnboundSuperComboPassesTheKeyAndTheSuperKey()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Press(core, "Super_L");

        AssertPassed(Press(core, "E"));
        AssertPassed(Release(core, "E"));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void SuperAlonePassesThrough()
    {
        var core = Load("bind = SUPER, Q, killactive");

        AssertPassed(Press(core, "Super_L"));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void PressBindFiresOnceAndConsumesTheRestOfTheSequence()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertRan(Press(core, "Q"), 1);
        AssertCaught(Press(core, "Q"));
        AssertCaught(Release(core, "Q"));
        AssertPassed(Release(core, "Super_L"));

        Press(core, "Super_L");
        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void ABindDoesNotRunWhileInputIsInhibitedAndRunsAgainAfter()
    {
        var core = Load("bind = SUPER, Q, killactive\n");
        Appear(core, 1);
        core.KeyState.Inhibit(true);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Q"));
        Release(core, "Q");

        core.KeyState.Inhibit(false);
        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void MouseBindFiresOnTheButtonAndNotOnAKey()
    {
        var core = Load("bind = SUPER, mouse:left, movewindow\nbind = SUPER, Q, killactive\n");
        Appear(core, 1);
        Press(core, "Super_L");

        var button = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(500, 400) });
        AssertRan(button, null);
        Assert.Contains(client => client.Window == new WindowId(1), button.State.Clients);

        AssertPassed(Press(core, "mouse:left"));
        AssertRan(Press(core, "Q"), 1);
        AssertCaught(core.Handle(new PointerButtonReleased("mouse:left")));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void KeyboardBindDoesNotFireOnAMouseButton()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(core.Handle(new PointerButtonPressed("Q")));
    }

    [TestMethod]
    public void OtherDispatcherIsReportedAndDoesNotClose()
    {
        var core = Load("bind = SUPER, E, exec, notepad");
        Appear(core, 1);
        Press(core, "Super_L");

        var outcome = Press(core, "E");

        AssertRan(outcome, null);
        Assert.IsNotNull(outcome.Launch);
        Assert.Contains(client => client.Window == new WindowId(1), outcome.State.Clients);
    }

    [TestMethod]
    public void TwoBindsOnOneKeyLaunchBothPrograms()
    {
        var core = Load("bind = SUPER, E, exec, a\nbind = SUPER, E, exec, b");
        Press(core, "Super_L");

        var outcome = Press(core, "E");

        Assert.AreSequenceEqual(["a", "b"], outcome.Launches.Select(launch => launch.CommandLine));
    }

    [TestMethod]
    public void KeyNameMatchIsCaseInsensitive()
    {
        var core = Load("bind = super, q, killactive");
        Appear(core, 1);
        Press(core, "Super_R");

        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void ScanCodeMatchesACodeBind()
    {
        var core = Load("bind = SUPER, code:28, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Return"));
        Release(core, "Return");
        AssertRan(Press(core, "Return", 28), 1);
    }

    [TestMethod]
    public void UnbindRemovesOnlyTheMatchingBind()
    {
        var core = Load("bind = SUPER, Q, killactive\nunbind = SUPER, Q\nbind = SUPER, R, killactive\n");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Q"));
        AssertRan(Press(core, "R"), 1);
    }

    [TestMethod]
    public void UnbindAllClearsBinds()
    {
        var core = Load("bind = SUPER, Q, killactive\nunbind = all\n");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void EveryBindOnAChordRunsInConfigOrder()
    {
        var core = Load("bind = , Q, killactive\nbind = , Q, workspace, 2\n");
        Appear(core, 1);
        Appear(core, 2);

        var outcome = Press(core, "Q");

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new WindowId(2), outcome.Close);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
    }

    [TestMethod]
    public void EmptyDispatcherConsumesTheKeyAndClosesNothing()
    {
        var core = Load("bind = , Q,\n");
        Appear(core, 1);

        var outcome = Press(core, "Q");

        AssertCaught(outcome);
        Assert.Contains(client => client.Window == new WindowId(1), outcome.State.Clients);
        AssertCaught(Release(core, "Q"));
    }

    [TestMethod]
    public void ModifierKeyBindClosesTheActiveClient()
    {
        var core = Load("bind = , Super_L, killactive");
        Appear(core, 1);

        AssertRan(Press(core, "Super_L"), 1);
    }

    [TestMethod]
    public void LoadingConfigReplacesBinds()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        core.Handle(new ConfigLoaded(ConfigParser.Parse("")));
        Press(core, "Super_L");

        AssertPassed(Press(core, "Q"));
    }

    private static void AssertPassed(Outcome outcome)
    {
        Assert.IsFalse(outcome.Consumed);
        Assert.IsNull(outcome.Close);
        Assert.IsEmpty(outcome.Placements);
    }

    private static void AssertCaught(Outcome outcome)
    {
        Assert.IsTrue(outcome.Consumed);
        Assert.IsNull(outcome.Close);
        Assert.IsEmpty(outcome.Placements);
    }

    private static void AssertRan(Outcome outcome, long? close)
    {
        Assert.IsTrue(outcome.Consumed);
        Assert.IsEmpty(outcome.Placements);
        if (close is long id)
            Assert.AreEqual(new WindowId(id), outcome.Close);
        else
            Assert.IsNull(outcome.Close);
    }
}
