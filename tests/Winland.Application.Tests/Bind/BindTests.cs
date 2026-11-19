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
    public void SuperQRunsCloseAndAsksToCloseTheActiveClient()
    {
        var core = Load("bind = Win+Q, close");
        Appear(core, 1);
        Appear(core, 2);
        Press(core, "Super_L");

        var outcome = Press(core, "Q");

        AssertRan(outcome, 2);
        Assert.AreEqual(2, outcome.State.Clients.Count);
        Assert.Contains(client => client.Window == new WindowId(2), outcome.State.Clients);
    }

    [TestMethod]
    public void CloseDoesNotRemoveTheClientUntilTheWindowCloses()
    {
        var core = Load("bind = Win+Q, close");
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
    public void FocusedClientIsTheOneCloseCloses()
    {
        var core = Load("bind = Win+Q, close");
        Appear(core, 1);
        Appear(core, 2);
        core.Handle(new WindowFocused(new WindowId(1)));
        Press(core, "Super_L");

        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void CloseWithNoClientRunsAndClosesNothing()
    {
        var core = Load("bind = Win+Q, close");
        Press(core, "Super_L");

        var outcome = Press(core, "Q");

        AssertRan(outcome, null);
        Assert.IsEmpty(outcome.State.Clients);
    }

    [TestMethod]
    public void UnmanagedFocusIsNotAClientToClose()
    {
        var core = Load("bind = Win+Q, close");
        Appear(core, 1);
        core.Handle(new WindowFocused(new WindowId(99)));
        Press(core, "Super_L");

        AssertRan(Press(core, "Q"), null);
        Assert.Contains(client => client.Window == new WindowId(1), core.Handle(new KeyReleased("Q")).State.Clients);
    }

    [TestMethod]
    public void KeyWithoutTheModifierDoesNotMatch()
    {
        var core = Load("bind = Win+Q, close");
        Appear(core, 1);

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void ExtraModifierDoesNotMatch()
    {
        var core = Load("bind = Win+Q, close");
        Appear(core, 1);
        Press(core, "Super_L");
        Press(core, "Shift_L");

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    [DataRow("Super_L", "Win")]
    [DataRow("Super_R", "Win")]
    [DataRow("Alt_L", "Alt")]
    [DataRow("Alt_R", "Alt")]
    [DataRow("Control_L", "Ctrl")]
    [DataRow("Control_R", "Ctrl")]
    [DataRow("Shift_L", "Shift")]
    [DataRow("Shift_R", "Shift")]
    public void EachModifierKeyMatchesItsBind(string key, string modifier)
    {
        var core = Load($"bind = {modifier}+Q, close");
        Appear(core, 1);
        Press(core, key);

        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void EveryModifierInTheChordIsRequired()
    {
        var core = Load("bind = Ctrl+Alt+Shift+Q, close");
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
        var core = Load("bind = Q, close");
        Appear(core, 1);

        AssertRan(Press(core, "Q"), 1);

        Release(core, "Q");
        Press(core, "Super_L");
        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void BoundSuperComboConsumesTheKeyAndPassesTheSuperRelease()
    {
        var core = Load("bind = Win+L, close");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertRan(Press(core, "L"), 1);
        AssertCaught(Release(core, "L"));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void UnboundSuperComboPassesTheKeyAndTheSuperKey()
    {
        var core = Load("bind = Win+Q, close");
        Press(core, "Super_L");

        AssertPassed(Press(core, "E"));
        AssertPassed(Release(core, "E"));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void SuperAlonePassesThrough()
    {
        var core = Load("bind = Win+Q, close");

        AssertPassed(Press(core, "Super_L"));
        AssertPassed(Release(core, "Super_L"));
    }

    [TestMethod]
    public void PressBindFiresOnceAndConsumesTheRestOfTheSequence()
    {
        var core = Load("bind = Win+Q, close");
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
        var core = Load("bind = Win+Q, close\n");
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
        var core = Load("bind = Win+mouse:left, move\nbind = Win+Q, close\n");
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
        var core = Load("bind = Win+Q, close");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(core.Handle(new PointerButtonPressed("Q")));
    }

    [TestMethod]
    public void OtherDispatcherIsReportedAndDoesNotClose()
    {
        var core = Load("bind = Win+E, exec notepad");
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
        var core = Load("bind = Win+E, exec a\nbind = Win+E, exec b");
        Press(core, "Super_L");

        var outcome = Press(core, "E");

        Assert.AreSequenceEqual(["a", "b"], outcome.Launches.Select(launch => launch.CommandLine));
    }

    [TestMethod]
    public void KeyNameMatchIsCaseInsensitive()
    {
        var core = Load("bind = Win+q, close");
        Appear(core, 1);
        Press(core, "Super_R");

        AssertRan(Press(core, "Q"), 1);
    }

    [TestMethod]
    public void ScanCodeMatchesACodeBind()
    {
        var core = Load("bind = Win+code:28, close");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Return"));
        Release(core, "Return");
        AssertRan(Press(core, "Return", 28), 1);
    }

    [TestMethod]
    public void UnbindRemovesOnlyTheMatchingBind()
    {
        var core = Load("bind = Win+Q, close\nunbind = Win+Q\nbind = Win+R, close\n");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Q"));
        AssertRan(Press(core, "R"), 1);
    }

    [TestMethod]
    public void UnbindAllClearsBinds()
    {
        var core = Load("bind = Win+Q, close\nunbind = all\n");
        Appear(core, 1);
        Press(core, "Super_L");

        AssertPassed(Press(core, "Q"));
    }

    [TestMethod]
    public void EveryBindOnAChordRunsInConfigOrder()
    {
        var core = Load("bind = Q, close\nbind = Q, workspace 2\n");
        Appear(core, 1);
        Appear(core, 2);

        var outcome = Press(core, "Q");

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new WindowId(2), outcome.Close);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
    }

    [TestMethod]
    public void ModifierKeyBindClosesTheActiveClient()
    {
        var core = Load("bind = Super_L, close");
        Appear(core, 1);

        AssertRan(Press(core, "Super_L"), 1);
    }

    [TestMethod]
    public void LoadingConfigReplacesBinds()
    {
        var core = Load("bind = Win+Q, close");
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
