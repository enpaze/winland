namespace Winland.Application.Tests;

[TestClass]
public class WindowManagerTests
{
    [TestMethod]
    public void AppearingWindowIsPlacedOnItsWorkArea()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(42), new Rectangle(0, 0, 1920, 1080)));

        var client = Assert.ContainsSingle(outcome.State.Clients);
        var placement = Assert.ContainsSingle(outcome.Placements);
        Assert.AreEqual(new WindowId(42), client.Window);
        Assert.AreEqual(client.Id, placement.Client);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), placement.Bounds);
    }

    [TestMethod]
    public void EveryOutcomeThatAsksForSomethingIsHandedOnInTheOrderTheCoreDecidedIt()
    {
        var core = new WindowManager();
        var decided = new List<Outcome>();
        core.Decided = decided.Add;

        var first = core.Handle(new WindowAppeared(new WindowId(42), new Rectangle(0, 0, 1920, 1080)));
        var second = core.Handle(new Dispatch("workspace", "2"));
        var third = core.Execute("workspace 1").Effect;

        Assert.AreSequenceEqual([first, second, third], decided);
    }

    [TestMethod]
    public void SecondWindowSplitsTheWorkAreaSideBySide()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);

        core.Handle(new WindowAppeared(new WindowId(1), area));
        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void ThirdWindowSplitsTheActiveHalfTopAndBottom()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);

        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        var outcome = core.Handle(new WindowAppeared(new WindowId(3), area));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 540), BoundsOf(outcome, 2));
        Assert.AreEqual(new Rectangle(960, 540, 960, 540), BoundsOf(outcome, 3));
    }

    [TestMethod]
    public void TallerWorkAreaSplitsTopAndBottom()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1080, 1920);

        core.Handle(new WindowAppeared(new WindowId(1), area));
        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area));

        Assert.AreEqual(new Rectangle(0, 0, 1080, 960), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 960, 1080, 960), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void PointerInTheFirstHalfPlacesTheNewWindowThere()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);

        core.Handle(new WindowAppeared(new WindowId(1), area));
        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area) { Pointer = new Point(10, 540) });

        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void PointerInTheTopHalfOfATallSplitPlacesTheNewWindowThere()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1080, 1920);

        core.Handle(new WindowAppeared(new WindowId(1), area));
        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area) { Pointer = new Point(540, 10) });

        Assert.AreEqual(new Rectangle(0, 960, 1080, 960), BoundsOf(outcome, 1));
        Assert.AreEqual(new Rectangle(0, 0, 1080, 960), BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void ClosingAWindowGivesItsSpaceToItsNeighbor()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        var outcome = core.Handle(new WindowClosed(new WindowId(2)));

        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 1));
        Assert.DoesNotContain(client => client.Window == new WindowId(2), outcome.State.Clients);
    }

    [TestMethod]
    public void MinimizingAWindowGivesItsSpaceBackAndRestoringReturnsIt()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        core.Handle(new WindowAppeared(new WindowId(3), area));

        var minimized = core.Handle(new WindowMinimized(new WindowId(3)));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(minimized, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(minimized, 2));
        var hidden = Assert.ContainsSingle(client => client.Window == new WindowId(3), minimized.State.Clients);
        Assert.IsTrue(hidden.Minimized);
        Assert.DoesNotContain(placement => placement.Client == hidden.Id, minimized.Placements);

        var restored = core.Handle(new WindowRestored(new WindowId(3)));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(restored, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 540), BoundsOf(restored, 2));
        Assert.AreEqual(new Rectangle(960, 540, 960, 540), BoundsOf(restored, 3));
        Assert.IsFalse(Assert.ContainsSingle(client => client.Window == new WindowId(3), restored.State.Clients).Minimized);
    }

    [TestMethod]
    public void RestoringTheOnlyWindowReturnsItToTheWorkArea()
    {
        var core = new WindowManager();
        var area = new Rectangle(10, 20, 800, 600);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var minimized = core.Handle(new WindowMinimized(new WindowId(1)));

        Assert.IsEmpty(minimized.Placements);
        Assert.IsTrue(Assert.ContainsSingle(minimized.State.Clients).Minimized);

        var restored = core.Handle(new WindowRestored(new WindowId(1)));

        Assert.AreEqual(area, BoundsOf(restored, 1));
        Assert.IsFalse(Assert.ContainsSingle(restored.State.Clients).Minimized);
    }

    [TestMethod]
    public void ClosingAMinimizedWindowRemovesIt()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        core.Handle(new WindowMinimized(new WindowId(2)));

        var outcome = core.Handle(new WindowClosed(new WindowId(2)));

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual(new WindowId(1), client.Window);
        Assert.IsEmpty(outcome.Placements);
    }

    [TestMethod]
    [DataRow("tool")]
    [DataRow("tooltip")]
    [DataRow("taskbar")]
    [DataRow("cloaked")]
    [DataRow("invisible")]
    [DataRow("untitled")]
    [DataRow("child")]
    [DataRow("overlay")]
    public void WindowsOutsideTheAdoptionFilterAreLeftAlone(string reason)
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(99), area) { Traits = Rejected(reason) });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual(new WindowId(1), client.Window);
        Assert.IsEmpty(outcome.Placements);
    }

    [TestMethod]
    public void ElevatedWindowIsTiled()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);

        var outcome = core.Handle(new WindowAppeared(new WindowId(7), area)
        {
            Traits = WindowTraits.Normal with { Elevated = true },
        });

        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 7));
        Assert.IsFalse(Assert.ContainsSingle(outcome.State.Clients).Floating);
    }

    [TestMethod]
    public void DialogFloatsCenteredAndLeavesTheTilesAlone()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        var bounds = new Rectangle(400, 300, 320, 200);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var floated = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = bounds,
        });

        var dialog = Assert.ContainsSingle(client => client.Window == new WindowId(2), floated.State.Clients);
        Assert.IsTrue(dialog.Floating);
        Assert.AreEqual(NewFloat.Centered(area, bounds), Assert.ContainsSingle(placement => placement.Client == dialog.Id, floated.Placements).Bounds);
        Assert.Contains(client => client.Window == new WindowId(1) && !client.Floating, floated.State.Clients);

        var split = core.Handle(new WindowAppeared(new WindowId(3), area));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(split, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(split, 3));
        Assert.IsTrue(Assert.ContainsSingle(client => client.Window == new WindowId(2), split.State.Clients).Floating);
    }

    [TestMethod]
    public void WindowIsAdoptedOnceItGainsATitle()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);

        var ignored = core.Handle(new WindowAppeared(new WindowId(5), area)
        {
            Traits = WindowTraits.Normal with { Titled = false },
        });
        Assert.IsEmpty(ignored.State.Clients);

        var outcome = core.Handle(new WindowAppeared(new WindowId(5), area));

        Assert.AreEqual(new Rectangle(0, 0, 1920, 1080), BoundsOf(outcome, 5));
    }

    [TestMethod]
    public void ClassRuleFloatsTheWindowAndLeavesTheTilesAlone()
    {
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\nwindowrule = float, class:kitty\n"));
        var area = new Rectangle(0, 0, 1920, 1080);
        var bounds = new Rectangle(40, 50, 320, 200);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
            Bounds = bounds,
        });

        var floated = Assert.ContainsSingle(client => client.Window == new WindowId(2), outcome.State.Clients);
        Assert.IsTrue(floated.Floating);
        Assert.AreEqual(NewFloat.Centered(area, bounds), Assert.ContainsSingle(outcome.Placements).Bounds);
        Assert.Contains(client => client.Window == new WindowId(1) && !client.Floating, outcome.State.Clients);

        var split = core.Handle(new WindowAppeared(new WindowId(3), area));

        Assert.AreEqual(new Rectangle(0, 0, 960, 1080), BoundsOf(split, 1));
        Assert.AreEqual(new Rectangle(960, 0, 960, 1080), BoundsOf(split, 3));
        Assert.IsTrue(Assert.ContainsSingle(client => client.Window == new WindowId(2), split.State.Clients).Floating);
    }

    [TestMethod]
    [DataRow("title:Editor")]
    public void TitleRuleFloatsAMatchingWindow(string match)
    {
        var core = new WindowManager(ConfigParser.Parse($"windowrule = float, {match}\n"));
        var bounds = new Rectangle(1, 2, 3, 4);
        var area = new Rectangle(0, 0, 800, 600);

        var missed = core.Handle(new WindowAppeared(new WindowId(1), area)
        {
            Traits = WindowTraits.Normal with { Title = "Release Notes" },
            Bounds = bounds,
        });
        Assert.IsFalse(Assert.ContainsSingle(missed.State.Clients).Floating);

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Title = "Editor" },
            Bounds = bounds,
        });

        var floated = Assert.ContainsSingle(client => client.Window == new WindowId(2), outcome.State.Clients);
        Assert.IsTrue(floated.Floating);
        Assert.AreEqual(NewFloat.Centered(area, bounds), Assert.ContainsSingle(placement => placement.Client == floated.Id, outcome.Placements).Bounds);
    }

    [TestMethod]
    public void CloakedFrameStaysUnmanagedWithAPackagedIdentity()
    {
        var core = new WindowManager();
        var outcome = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600))
        {
            Traits = WindowTraits.Normal with
            {
                Cloaked = true,
                Class = "Windows.UI.Core.CoreWindow",
                Title = "Calculator",
                Exe = "CalculatorApp.exe",
                Aumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            },
        });

        Assert.IsEmpty(outcome.State.Clients);
        Assert.IsEmpty(outcome.Placements);
    }

    [TestMethod]
    public void ExeRuleIgnoresTheWindow()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = ignore, exe:notepad.exe\n"));
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Exe = "notepad.exe" },
        });

        Assert.ContainsSingle(client => client.Window == new WindowId(1), outcome.State.Clients);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), outcome.State.Clients);
        Assert.IsEmpty(outcome.Placements);
    }

    [TestMethod]
    public void RuleMatchesOnlyWhenClassTitleAndExeAllMatch()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 4, class:code, title:Editor, exe:code.exe\n"));
        var area = new Rectangle(0, 0, 1000, 800);

        var missed = core.Handle(new WindowAppeared(new WindowId(1), area)
        {
            Traits = WindowTraits.Normal with { Class = "code", Title = "Editor", Exe = "other.exe" },
        });
        Assert.AreEqual(WorkspaceId.Initial, Assert.ContainsSingle(missed.State.Clients).Workspace);

        var hit = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Class = "code", Title = "Editor", Exe = "code.exe" },
        });
        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), hit.State.Clients);
        Assert.AreEqual(new WorkspaceId("4"), client.Workspace);
        Assert.IsFalse(client.Floating);
    }

    [TestMethod]
    public void VersionOneRuleMatchesClassOnly()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:kitty\n"));
        var area = new Rectangle(0, 0, 200, 100);
        var bounds = new Rectangle(5, 5, 20, 20);

        var byTitle = core.Handle(new WindowAppeared(new WindowId(1), area)
        {
            Traits = WindowTraits.Normal with { Class = "other", Title = "kitty" },
            Bounds = bounds,
        });
        Assert.IsFalse(Assert.ContainsSingle(byTitle.State.Clients).Floating);

        var byClass = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
            Bounds = bounds,
        });
        var floated = Assert.ContainsSingle(client => client.Window == new WindowId(2), byClass.State.Clients);
        Assert.IsTrue(floated.Floating);
        Assert.AreEqual(NewFloat.Centered(area, bounds), Assert.ContainsSingle(placement => placement.Client == floated.Id, byClass.Placements).Bounds);
    }

    [TestMethod]
    public void TileRuleAdoptsAWindowTheFilterWouldLeaveAlone()
    {
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\nwindowrule = tile, class:toolwindow\n"));
        var area = new Rectangle(0, 0, 1920, 1080);

        var outcome = core.Handle(new WindowAppeared(new WindowId(7), area)
        {
            Traits = WindowTraits.Normal with { Tool = true, Class = "toolwindow" },
        });

        Assert.IsFalse(Assert.ContainsSingle(outcome.State.Clients).Floating);
        Assert.AreEqual(WorkspaceId.Initial, Assert.ContainsSingle(outcome.State.Clients).Workspace);
        Assert.AreEqual(area, BoundsOf(outcome, 7));
    }

    [TestMethod]
    public void AWorkspaceRuleForAProgramLeavesItsPopupsAlone()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2, exe:chrome.exe\n"));
        var area = new Rectangle(0, 0, 1920, 1080);

        var menu = core.Handle(new WindowAppeared(new WindowId(7), area)
        {
            Traits = WindowTraits.Normal with { Tool = true, Titled = false, Title = "", Exe = "chrome.exe" },
        });
        var popup = core.Handle(new WindowAppeared(new WindowId(8), area)
        {
            Traits = WindowTraits.Normal with { Overlay = true, Exe = "chrome.exe" },
        });

        Assert.IsEmpty(menu.State.Clients);
        Assert.IsEmpty(popup.State.Clients);
    }

    [TestMethod]
    public void TileRuleTilesADialog()
    {
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\nwindowrule = tile, class:dialog\n"));
        var area = new Rectangle(0, 0, 1920, 1080);

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), area)
        {
            Traits = WindowTraits.Normal with { Dialog = true, Class = "dialog" },
            Bounds = new Rectangle(1, 1, 10, 10),
        });

        Assert.IsFalse(Assert.ContainsSingle(outcome.State.Clients).Floating);
        Assert.AreEqual(area, BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void FloatAndWorkspaceBothApply()
    {
        var text = "windowrule = workspace 4, class:kitty\nwindowrule = float, class:kitty\n";
        var core = new WindowManager(ConfigParser.Parse(text));
        var bounds = new Rectangle(10, 20, 30, 40);

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600))
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
            Bounds = bounds,
        });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.AreEqual(new WorkspaceId("4"), client.Workspace);
        Assert.AreEqual(new WorkspaceId("4"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(NewFloat.Centered(new Rectangle(0, 0, 800, 600), bounds), Assert.ContainsSingle(outcome.Placements).Bounds);
    }

    [TestMethod]
    public void WorkspaceRuleOnADialogKeepsItFloating()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 3, class:dialog\n"));
        var bounds = new Rectangle(4, 5, 6, 7);

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 900, 700))
        {
            Traits = WindowTraits.Normal with { Dialog = true, Class = "dialog" },
            Bounds = bounds,
        });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.AreEqual(new WorkspaceId("3"), client.Workspace);
        Assert.AreEqual(NewFloat.Centered(new Rectangle(0, 0, 900, 700), bounds), Assert.ContainsSingle(outcome.Placements).Bounds);
    }

    [TestMethod]
    public void WorkspaceRuleWithStayDoesNotSwitch()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2 stay, class:kitty\n"));
        var area = new Rectangle(0, 0, 640, 480);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), outcome.State.Clients);
        Assert.AreEqual(new WorkspaceId("2"), client.Workspace);
        Assert.IsFalse(client.Floating);
        Assert.AreEqual(WorkspaceId.Initial, outcome.State.ActiveWorkspace);
        Assert.Contains(candidate => candidate.Window == new WindowId(1) && candidate.Workspace == WorkspaceId.Initial, outcome.State.Clients);
        Assert.DoesNotContain(hidden => hidden.Window == new WindowId(1), outcome.State.Hidden);
        Assert.DoesNotContain(placement => placement.Client == client.Id, outcome.Placements);
        Assert.Contains(hidden => hidden.Window == new WindowId(2), outcome.State.Hidden);
    }

    [TestMethod]
    public void LaterRuleWinsAndDifferentEffectsCombine()
    {
        var text =
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\nwindowrule = float, class:kitty\n" +
            "windowrule = workspace 9, class:kitty\n" +
            "windowrule = tile, class:kitty\n";
        var core = new WindowManager(ConfigParser.Parse(text));
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), outcome.State.Clients);
        Assert.IsFalse(client.Floating);
        Assert.AreEqual(new WorkspaceId("9"), client.Workspace);
        Assert.AreEqual(new WorkspaceId("9"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(area, BoundsOf(outcome, 2));
        Assert.Contains(hidden => hidden.Window == new WindowId(1), outcome.State.Hidden);
    }

    [TestMethod]
    public void LaterIgnoreLeavesTheWindowUnmanaged()
    {
        var text = "windowrule = workspace 2, class:kitty\nwindowrule = ignore, class:kitty\n";
        var core = new WindowManager(ConfigParser.Parse(text));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 100, 100))
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        Assert.IsEmpty(outcome.State.Clients);
        Assert.IsEmpty(outcome.Placements);
    }

    [TestMethod]
    public void XwaylandMatchIsNotApplied()
    {
        var document = ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\nwindowrule = float, class:kitty, xwayland:1\n");
        var core = new WindowManager(document);

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 100, 100))
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
            Bounds = new Rectangle(1, 2, 3, 4),
        });

        Assert.IsFalse(Assert.ContainsSingle(outcome.State.Clients).Floating);
        Assert.AreEqual(new Rectangle(0, 0, 100, 100), BoundsOf(outcome, 1));
    }

    [TestMethod]
    public void PatternMustMatchTheWholeField()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, title:Notes\n"));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 300, 200))
        {
            Traits = WindowTraits.Normal with { Title = "Release Notes" },
            Bounds = new Rectangle(4, 5, 6, 7),
        });

        Assert.IsFalse(Assert.ContainsSingle(outcome.State.Clients).Floating);
    }

    [TestMethod]
    public void AppearingAgainDoesNotSplitAgain()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), area));

        Assert.ContainsSingle(outcome.State.Clients);
        Assert.IsEmpty(outcome.Placements);
    }

    [TestMethod]
    public void AClientShownAgainLeavesTheFocusWhereItWas()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        core.Handle(new WindowFocused(new WindowId(1)));

        var echo = Echo(core, 2, area);

        foreach (var outcome in echo)
{
    Assert.IsNull(outcome.Focus);
}
        Assert.AreEqual("0x1", Json(core.Execute("j/get window").Text).GetProperty("address").GetString());
    }

    [TestMethod]
    public void AClientShownAgainChangesNoPlacement()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        var echo = Echo(core, 1, area);

        foreach (var outcome in echo)
{
    {
            Assert.IsEmpty(outcome.Placements);
            Assert.IsEmpty(outcome.Show);
            Assert.IsEmpty(outcome.Hide);
        }
}
        Assert.AreEqual(2, echo[^1].State.Clients.Count);
    }

    [TestMethod]
    public void AClientThatShowsItselfOnAHiddenWorkspaceIsHiddenAgain()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new Dispatch("workspace", "2"));

        var echo = Echo(core, 1, area);

        Assert.AreSequenceEqual([new WindowId(1)], echo[^1].Hide);
    }

    private static Outcome[] Echo(WindowManager core, long window, Rectangle area)
    {
        var id = new WindowId(window);
        return
        [
            core.Handle(new WindowRestored(id)),
            core.Handle(new WindowClassChanged(id, WindowTraits.Normal.Class)),
            core.Handle(new WindowAppeared(id, area)),
        ];
    }

    private static WindowTraits Rejected(string reason) => reason switch
    {
        "tool" => WindowTraits.Normal with { Tool = true },
        "tooltip" => WindowTraits.Normal with { Tooltip = true },
        "taskbar" => WindowTraits.Normal with { Taskbar = true },
        "cloaked" => WindowTraits.Normal with { Cloaked = true },
        "invisible" => WindowTraits.Normal with { Visible = false },
        "untitled" => WindowTraits.Normal with { Titled = false },
        "child" => WindowTraits.Normal with { TopLevel = false },
        "overlay" => WindowTraits.Normal with { Overlay = true },
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown rejection."),
    };
}
