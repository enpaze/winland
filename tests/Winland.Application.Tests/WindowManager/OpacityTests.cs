namespace Winland.Application.Tests;

[TestClass]
public class OpacityTests
{
    private static readonly Rectangle Area = new(0, 0, 800, 600);

    [TestMethod]
    public void OpacityFractionsClampAndABadValueKeepsThePreviousOne()
    {
        var options = DecorationOptions.From(ConfigParser.Parse("""
            decoration:active_opacity = 0.8
            decoration:active_opacity = 2
            decoration:inactive_opacity = -0.4
            decoration:inactive_opacity = no
            """));

        Assert.AreEqual(1f, options.ActiveOpacity);
        Assert.AreEqual(0f, options.InactiveOpacity);
    }

    [TestMethod]
    public void OpacityKeysAreHonored()
    {
        var document = ConfigParser.Parse("""
            decoration:active_opacity = 0.8
            decoration:inactive_opacity = 0.4
            decoration:blur:enabled = true
            """);

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(3, document.Settings.Count);
    }

    [TestMethod]
    public void OpacityRuleParsesOneOrTwoFractions()
    {
        var document = ConfigParser.Parse("""
            windowrule = opacity 0.8, class:one
            windowrule = opacity 0.8 0.6, class:two
            """);

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(2, document.WindowRules.Count);
        Assert.AreEqual(new OpacityRule(0.8f, 0.8f, 0.8f), document.WindowRules[0].Opacity);
        Assert.AreEqual(new OpacityRule(0.8f, 0.6f, 1f), document.WindowRules[1].Opacity);
    }

    [TestMethod]
    [DataRow("opacity 0.8 0.6 0.9")]
    [DataRow("opacity 0.8 override")]
    [DataRow("opacity nope")]
    [DataRow("opacity 0.8 extra")]
    public void MalformedOpacityRuleWarns(string effect)
    {
        var document = ConfigParser.Parse($"windowrule = {effect}, class:kitty\n");

        Assert.IsEmpty(document.WindowRules);
        Assert.AreEqual(new ConfigWarning(1, "Malformed window rule."), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void ActiveAndInactiveClientsUseTheirFractions()
    {
        var core = CoreWith("""
            decoration:active_opacity = 0.8
            decoration:inactive_opacity = 0.4
            """);

        var first = core.Handle(Appeared(1, Area));
        Assert.AreEqual(204, Alpha(first, 1));

        var second = core.Handle(Appeared(2, Area));
        Assert.AreEqual(102, Alpha(second, 1));
        Assert.AreEqual(204, Alpha(second, 2));

        var focused = core.Handle(new WindowFocused(new WindowId(1)));
        Assert.AreEqual(204, Alpha(focused, 1));
        Assert.AreEqual(102, Alpha(focused, 2));
    }

    [TestMethod]
    public void RuleOpacityMultipliesAndTheLastRuleWins()
    {
        var multiplied = CoreWith("""
            decoration:active_opacity = 0.5
            windowrule = opacity 0.5, class:kitty
            windowrule = opacity 0.8, class:kitty
            """);
        var both = multiplied.Handle(Appeared(1, Area, windowClass: "kitty"));
        Assert.AreEqual(102, Alpha(both, 1));

        var replaced = multiplied.Handle(new Configure(ConfigParser.Parse("""
            decoration:active_opacity = 0.8
            windowrule = opacity 0.8, class:kitty
            """)));
        Assert.AreEqual(163, Alpha(replaced, 1));
    }

    [TestMethod]
    public void AClosedClientsOpacityRuleDoesNotStickToTheNextWindow()
    {
        var core = CoreWith("""
            decoration:active_opacity = 1
            windowrule = opacity 0.8, class:kitty
            """);
        var first = core.Handle(Appeared(1, Area, windowClass: "kitty"));
        Assert.AreEqual(204, Alpha(first, 1));
        core.Handle(new WindowClosed(new WindowId(1)));
        core.Handle(new Configure(ConfigParser.Parse("decoration:active_opacity = 0.5\n")));

        var appeared = core.Handle(Appeared(1, Area, windowClass: "kitty"));

        Assert.AreEqual(128, Alpha(appeared, 1));
    }

    [TestMethod]
    public void OpacityDoesNotAdoptAWindowThatStaysUnmanaged()
    {
        var core = CoreWith("windowrule = opacity 0.5, class:*\n");

        var outcome = core.Handle(new WindowAppeared(new WindowId(9), Area)
        {
            Traits = WindowTraits.Normal with { Taskbar = true },
        });

        Assert.IsEmpty(outcome.State.Clients);
        Assert.IsEmpty(outcome.Decorations);
    }

    [TestMethod]
    public void AFullscreenClientIsOpaqueWhateverTheConfiguredOpacities()
    {
        var options = DecorationOptions.Documented with
        {
            ActiveOpacity = 0.8f,
            InactiveOpacity = 0.4f,
        };

        Assert.AreEqual(255, DecorationAlpha.Compute(options, new OpacityInput(Active: true, Fullscreen: true), null, false));
        Assert.AreEqual(255, DecorationAlpha.Compute(options, new OpacityInput(Active: false, Fullscreen: true), null, false));
        Assert.AreEqual(204, DecorationAlpha.Compute(options, new OpacityInput(Active: true), null, false));
    }

    [TestMethod]
    public void AnOpacityRuleStillAppliesToAFullscreenClient()
    {
        var rule = new OpacityRule(0.5f, 0.5f, 0.5f);

        Assert.AreEqual(128, DecorationAlpha.Compute(DecorationOptions.Documented, new OpacityInput(Active: true, Fullscreen: true), rule, false));
    }

    [TestMethod]
    public void LayeredFlagRoundTripsAndAnOlderSnapshotIsNotLayered()
    {
        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(5), WorkspaceId.Initial, MonitorId.Primary, false, "Notepad", "Notes", 10, true),
        ]);

        Assert.Contains("\"layered\":true", snapshot.ToJson(), StringComparison.Ordinal);
        Assert.IsTrue(RecoverySnapshot.TryParse(snapshot.ToJson(), out var parsed));
        Assert.IsTrue(Assert.ContainsSingle(parsed.Clients).Layered);

        var older = """{"clients":[{"window":5,"workspace":"1","monitor":"\\\\.\\DISPLAY1","hidden":false,"class":"Notepad","title":"Notes","process":10}]}""";
        Assert.IsTrue(RecoverySnapshot.TryParse(older, out var legacy));
        Assert.IsFalse(Assert.ContainsSingle(legacy.Clients).Layered);
    }


    private static WindowManager CoreWith(string text)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(text)));
        return core;
    }

    private static byte Alpha(Outcome outcome, long window)
    {
        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(window), outcome.State.Clients);
        return Assert.ContainsSingle(decoration => decoration.Client == client.Id, outcome.Decorations).Alpha;
    }
}
