namespace Winland.Application.Tests;

[TestClass]
public class FocusTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void AnApplicationCannotStealFocus()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var stolen = core.Handle(new WindowFocused(new WindowId(1), FocusCause.Activated));

        Assert.AreEqual(new WindowId(2), stolen.Focus);
        Assert.AreSequenceEqual(["urgent>>1"], Lines(stolen));
        Assert.AreEqual(new WindowId(2), Active(core));
    }

    [TestMethod]
    public void AWindowTakingTheForegroundWithoutInputIsRefusedEvenWithFocusOnActivate()
    {
        var core = new WindowManager(ConfigParser.Parse("misc:focus_on_activate = true\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var stolen = core.Handle(new WindowFocused(new WindowId(1), FocusCause.Activated));

        Assert.AreEqual(new WindowId(2), stolen.Focus);
        Assert.AreEqual(new WindowId(2), Active(core));
    }

    [TestMethod]
    public void AWindowTakingTheForegroundAfterAClickInAnotherProgramNeedsFocusOnActivate()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area) { ProcessId = 10 });
        core.Handle(new WindowAppeared(new WindowId(2), Area) { ProcessId = 20 });

        var refused = core.Handle(new WindowFocused(new WindowId(1), FocusCause.User) { Clicked = new WindowId(2) });

        Assert.AreEqual(new WindowId(2), refused.Focus);
        Assert.AreSequenceEqual(["urgent>>1"], Lines(refused));
    }

    [TestMethod]
    public void AClickOnTheWindowItsOwnProgramOrAnUnmanagedWindowIsTheUsersChange()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area) { ProcessId = 10 });
        core.Handle(new WindowAppeared(new WindowId(2), Area) { ProcessId = 10 });
        core.Handle(new WindowAppeared(new WindowId(3), Area) { ProcessId = 30 });

        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User) { Clicked = new WindowId(1) });
        Assert.AreEqual(new WindowId(1), Active(core));

        core.Handle(new WindowFocused(new WindowId(2), FocusCause.User) { Clicked = new WindowId(1) });
        Assert.AreEqual(new WindowId(2), Active(core));

        core.Handle(new WindowFocused(new WindowId(3), FocusCause.User) { Clicked = new WindowId(99) });
        Assert.AreEqual(new WindowId(3), Active(core));

        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));
        Assert.AreEqual(new WindowId(1), Active(core));
    }

    [TestMethod]
    public void AClickStillChangesFocusWhenActivationIsRefused()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var clicked = core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));

        Assert.IsNull(clicked.Focus);
        Assert.AreSequenceEqual(["focus>>,1,1,1,"], Lines(clicked));
    }

    [TestMethod]
    [DataRow("no_focus")]
    public void NoFocusRefusesTheClient(string effect)
    {
        var core = new WindowManager(ConfigParser.Parse($"windowrule = {effect}, class:bar\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var opened = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { Class = "bar" },
        });
        Assert.AreEqual(new WindowId(1), opened.Focus);

        core.Handle(new WindowFocused(new WindowId(2), FocusCause.User));
        var clicked = core.Handle(new WindowFocused(new WindowId(2), FocusCause.User));

        Assert.AreEqual(new WindowId(1), clicked.Focus);
        Assert.AreEqual(new WindowId(1), Active(core));
    }

    [TestMethod]
    public void ALaterRuleCanAllowFocusAgain()
    {
        var core = new WindowManager(ConfigParser.Parse(
            "windowrule = no_focus, class:bar\n" +
            "windowrule = no_focus 0, class:bar\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var opened = core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { Class = "bar" },
        });

        Assert.IsNull(opened.Focus);
        Assert.AreEqual(new WindowId(2), Active(core));
    }

    [TestMethod]
    public void OurOwnForegroundRequestIsAccepted()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = stayfocused, class:pin\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "pin" },
        });
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var requested = core.Handle(new WindowFocused(new WindowId(2), FocusCause.Requested));

        Assert.IsNull(requested.Focus);
        Assert.AreEqual(new WindowId(2), Active(core));
    }

    [TestMethod]
    public void FocusSkipsAClientThatRefusesFocus()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = no_focus, class:bar\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { Class = "bar" },
        });
        core.Handle(new WindowFocused(new WindowId(2), FocusCause.User));

        var moved = core.Handle(new Dispatch("focus", "r"));

        Assert.IsNull(moved.Focus);
        Assert.AreEqual(new WindowId(1), Active(core));
    }

    [TestMethod]
    public void AnUnknownWindowTakingFocusIsLeftToWindows()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var stolen = core.Handle(new WindowFocused(new WindowId(99), FocusCause.Activated));

        Assert.IsNull(stolen.Focus);
        Assert.IsNull(Active(core));
    }

    [TestMethod]
    public void AWorkspaceShownAgainDoesNotFocusAWindowThatTakesNoFocus()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = no_focus, class:quiet\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Execute("workspace 2");
        core.Handle(new WindowAppeared(new WindowId(2), Area) { Traits = WindowTraits.Normal with { Class = "quiet" } });
        core.Execute("workspace 1");

        core.Execute("workspace 2");

        Assert.AreEqual("null", core.Execute("j/get window").Text);
    }

    [TestMethod]
    public void ADwindleSplitGoesNextToTheClientTheUserClicked()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));

        var opened = core.Handle(new WindowAppeared(new WindowId(3), Area));

        Assert.AreEqual(new Rectangle(0, 0, 960, 540), BoundsOf(opened, 1));
        Assert.AreEqual(new Rectangle(0, 540, 960, 540), BoundsOf(opened, 3));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(opened, 2));
    }

    [TestMethod]
    public void TheForegroundReportOfTheFocusedClientChangesNothing()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var reported = core.Handle(new WindowFocused(new WindowId(2), FocusCause.Requested));

        Assert.IsEmpty(reported.Placements);
        Assert.IsEmpty(reported.Show);
        Assert.IsEmpty(reported.Hide);
    }

    [TestMethod]
    public void TheForegroundReportAfterAFocusDispatcherDoesNotScrollTheStripAgain()
    {
        var core = new WindowManager(ConfigParser.Parse("general:layout = scrolling\n"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new WindowAppeared(new WindowId(3), Area));
        var focused = core.Handle(new Dispatch("focus", "address:0x1"));
        Assert.IsNotEmpty(focused.Placements);

        var reported = core.Handle(new WindowFocused(new WindowId(1), FocusCause.Requested));

        Assert.IsEmpty(reported.Placements);
    }

    private static WindowId? Active(WindowManager core)
    {
        var outcome = core.Handle(new Dispatch("close"));
        return outcome.Close;
    }

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);
}
