namespace Winland.Domain.Tests;

[TestClass]
public class WindowRuleParserTests
{
    [TestMethod]
    [DataRow("float")]
    [DataRow("tile")]
    [DataRow("ignore")]
    public void AdoptionEffectsAcceptAnExplicitBoolean(string effect)
    {
        Assert.IsTrue(Assert.ContainsSingle(Rule($"{effect} on, class:kitty").Effects).Enabled);
        var disabled = Rule($"{effect} off, class:kitty");
        Assert.IsFalse(Assert.ContainsSingle(disabled.Effects).Enabled);
        Assert.AreEqual(
            WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, []),
            WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, [disabled]));
    }

    [TestMethod]
    public void ADraftRulePrefersTheExecutableThenTheClassThenTheTitle()
    {
        var calculator = WindowTraits.Normal with
        {
            Class = "ApplicationFrameWindow",
            Exe = "ApplicationFrameHost.exe",
            Title = "Calculator",
            Aumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
        };

        Assert.AreEqual("windowrule = float, exe:ApplicationFrameHost.exe", WindowRuleParser.Draft(floating: true, calculator));
        Assert.AreEqual("windowrule = tile, class:CabinetWClass", WindowRuleParser.Draft(floating: false, WindowTraits.Normal with { Class = "CabinetWClass", Title = "Home" }));
        Assert.AreEqual("windowrule = float, title:Release Notes", WindowRuleParser.Draft(floating: true, WindowTraits.Normal with { Title = "Release Notes" }));
        Assert.IsNull(WindowRuleParser.Draft(floating: false, WindowTraits.Normal with { Class = "a.b, c" }));
    }

    [TestMethod]
    [DataRow("Release Notes")]
    [DataRow("tab there quoted")]
    public void ADraftRuleMatchesOnlyTheTitleItWasWrittenFrom(string title)
    {
        var window = WindowTraits.Normal with { Title = title };

        var draft = WindowRuleParser.Draft(floating: true, window);

        Assert.IsNotNull(draft);
        var rules = ConfigParser.Parse(draft + "\n").WindowRules;
        Assert.IsTrue(WindowRuleParser.Decide(window, rules).Floating);
        Assert.IsFalse(WindowRuleParser.Decide(window with { Title = "X" + title }, rules).Floating);
        Assert.IsFalse(WindowRuleParser.Decide(window with { Title = title.Replace('a', 'b').Replace('x', 'y') + "z" }, rules).Floating);
    }

    [TestMethod]
    public void AWindowWithNothingToMatchGetsNoDraftRule() =>
        Assert.IsNull(WindowRuleParser.Draft(floating: true, WindowTraits.Normal));

    [TestMethod]
    [DataRow(false, false, false, nameof(FloatReason.Default))]
    [DataRow(true, false, false, nameof(FloatReason.Topmost))]
    [DataRow(false, true, false, nameof(FloatReason.Dialog))]
    [DataRow(false, false, true, nameof(FloatReason.FixedSize))]
    public void ADecisionSaysWhyAWindowFloats(bool topmost, bool dialog, bool fixedSize, string reason)
    {
        var expected = Enum.Parse<FloatReason>(reason);
        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Topmost = topmost, Dialog = dialog, Resizable = !fixedSize }, []);

        Assert.AreEqual(expected, decision.FloatReason);
        Assert.AreEqual(expected != FloatReason.Default, decision.Floating);
    }

    [TestMethod]
    public void ARuleCannotAdoptAnOverlayAndADecisionSaysSo()
    {
        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Class = "osd", Overlay = true }, [Rule("float, class:osd")]);

        Assert.IsFalse(decision.Adopt);
        Assert.AreEqual(Admission.Overlay, decision.Admission);
    }

    [TestMethod]
    [DataRow("match:class kitty, float on")]
    [DataRow("float on, match:class kitty")]
    public void MatchPrefixRulesWarnWithTheWinlandSyntax(string value)
    {
        var document = ConfigParser.Parse($"windowrule = {value}\n");

        Assert.Contains("windowrule = float on, class:kitty", Assert.ContainsSingle(document.Warnings).Message);
        Assert.IsEmpty(document.WindowRules);
    }

    [TestMethod]
    [DataRow("initial_class")]
    [DataRow("initial_title")]
    [DataRow("aumid")]
    [DataRow("negative")]
    public void ARemovedMatcherWarnsAndTheRuleBesideItApplies(string field)
    {
        var document = ConfigParser.Parse($"windowrule = float on, {field}:kitty\nwindowrule = pin on, class:notes\n");

        var warning = Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(1, warning.Line);
        Assert.StartsWith($"Unsupported window rule match '{field}'.", warning.Message, StringComparison.Ordinal);
        Assert.AreEqual(WindowRuleEffect.Pin, Assert.ContainsSingle(Assert.ContainsSingle(document.WindowRules).Effects).Kind);
    }

    [TestMethod]
    public void Windowrulev2WarnsAndTheWindowruleBesideItStillApplies()
    {
        var document = ConfigParser.Parse("windowrulev2 = float on, class:kitty\nwindowrule = pin on, class:notes\n");

        var warning = Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(1, warning.Line);
        Assert.StartsWith("Unsupported keyword 'windowrulev2'.", warning.Message, StringComparison.Ordinal);
        Assert.AreEqual(WindowRuleEffect.Pin, Assert.ContainsSingle(Assert.ContainsSingle(document.WindowRules).Effects).Kind);
    }

    [TestMethod]
    [DataRow("notepad.exe", "Notepad.exe", true)]
    [DataRow("NOTEPAD.EXE", "notepad.exe", true)]
    [DataRow("other.exe", "Notepad.exe", false)]
    [DataRow("*pad.exe", "Notepad.exe", true)]
    [DataRow("note*", "other.exe", false)]
    public void ExeClassAndTitleMatchTheWholeFieldWithoutCase(string pattern, string exe, bool floating)
    {
        var document = ConfigParser.Parse($"windowrule = float, exe:{pattern}\n");

        Assert.AreEqual(floating, WindowRuleParser.Decide(WindowTraits.Normal with { Exe = exe }, document.WindowRules).Floating);
    }

    [TestMethod]
    public void ACommaInsideAPatternStaysInThePattern()
    {
        var rule = Rule("float, title:a{1,2}, class:kitty");

        Assert.AreEqual("a{1,2}", rule.TitlePattern);
        Assert.AreEqual("kitty", rule.ClassPattern);
    }

    [TestMethod]
    public void ATrailingOrDoubledCommaIsDropped()
    {
        var rule = Rule("float, class:foo,, title:bar,");

        Assert.AreEqual("foo", rule.ClassPattern);
        Assert.AreEqual("bar", rule.TitlePattern);
    }

    [TestMethod]
    public void ALiteralPatternMatchesThatTextAndAStarMatchesARun()
    {
        var literal = Rule("float, class:a)|(b");
        Assert.IsTrue(WindowRuleParser.Decide(WindowTraits.Normal with { Class = "a)|(b" }, [literal]).Floating);
        Assert.IsFalse(WindowRuleParser.Decide(WindowTraits.Normal with { Class = "ab" }, [literal]).Floating);

        var star = Rule("float, title:Release *");
        Assert.IsTrue(WindowRuleParser.Decide(WindowTraits.Normal with { Title = "release notes" }, [star]).Floating);
        Assert.IsFalse(WindowRuleParser.Decide(WindowTraits.Normal with { Title = "Notes" }, [star]).Floating);
    }

    [TestMethod]
    [DataRow("no_blur", "class:x")]
    [DataRow("min_size 10 10", "class:x")]
    [DataRow("scrolling_width 0.25", "class:x")]
    public void ARemovedEffectWarnsAndIsSkipped(string effect, string match)
    {
        var document = ConfigParser.Parse($"windowrule = {effect}, {match}\nwindowrule = float, class:kept\n");

        Assert.StartsWith("Unsupported window rule '", Assert.ContainsSingle(document.Warnings).Message, StringComparison.Ordinal);
        Assert.AreEqual(WindowRuleEffect.Float, Assert.ContainsSingle(Assert.ContainsSingle(document.WindowRules).Effects).Kind);
    }

    [TestMethod]
    [DataRow("opacity, class:x")]
    [DataRow("size 10, class:x")]
    public void AnArgumentThatDoesNotReadDropsTheRule(string text)
    {
        var parsed = WindowRuleParser.Parse(text);

        Assert.IsNull(parsed.Rule);
        Assert.AreEqual("Malformed window rule.", Assert.ContainsSingle(parsed.Warnings));
    }

    [TestMethod]
    [DataRow("workspace 9 garbage")]
    [DataRow("workspace 2 silent")]
    [DataRow("workspace name:dev")]
    [DataRow("workspace dev")]
    [DataRow("workspace +1")]
    [DataRow("workspace 2 stay now")]
    public void AWorkspaceEffectThatDoesNotReadWarnsWhenTheConfigIsRead(string effect)
    {
        var document = ConfigParser.Parse($"windowrule = float; {effect}, class:kitty\n");

        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, document.WindowRules);

        Assert.AreEqual("Malformed window rule.", Assert.ContainsSingle(document.Warnings).Message);
        Assert.IsTrue(decision.Floating);
        Assert.IsNull(decision.Workspace);
    }

    [TestMethod]
    [DataRow("workspace 2 stay", "2 stay")]
    [DataRow("workspace special:notes", "special:notes")]
    [DataRow("workspace empty", "empty")]
    public void AWorkspaceEffectTakesAnAddressAndStay(string effect, string workspace)
    {
        var document = ConfigParser.Parse($"windowrule = {effect}, class:kitty\n");

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(workspace, WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, document.WindowRules).Workspace);
    }

    [TestMethod]
    public void EveryEffectInAChainAppliesToTheMatchedWindow()
    {
        var document = ConfigParser.Parse("windowrule = float; size 900 600; workspace 9, exe:Spotify.exe\n");
        var spotify = WindowTraits.Normal with { Exe = "Spotify.exe" };

        var decision = WindowRuleParser.Decide(spotify, document.WindowRules);

        Assert.IsEmpty(document.Warnings);
        Assert.IsTrue(decision.Floating);
        Assert.AreEqual("9", decision.Workspace);
        Assert.AreEqual(new RulePoint(new RuleLength(900, false), new RuleLength(600, false)), decision.Placement.Size);
        Assert.AreEqual(WindowRuleParser.Decide(spotify with { Exe = "Other.exe" }, []), WindowRuleParser.Decide(spotify with { Exe = "Other.exe" }, document.WindowRules));
    }

    [TestMethod]
    [DataRow("float; no_blur; workspace 9", "Unsupported window rule 'no_blur'.")]
    [DataRow("float; size 10; workspace 9", "Malformed window rule.")]
    public void AnEffectThatDoesNotReadWarnsAndTheRestOfTheChainApplies(string effects, string warning)
    {
        var document = ConfigParser.Parse($"windowrule = {effects}, class:kitty\n");

        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, document.WindowRules);

        Assert.StartsWith(warning, Assert.ContainsSingle(document.Warnings).Message, StringComparison.Ordinal);
        Assert.IsTrue(decision.Floating);
        Assert.AreEqual("9", decision.Workspace);
        Assert.IsNull(decision.Placement.Size);
    }

    [TestMethod]
    public void ALaterEffectInTheChainWinsOverAnEarlierOne()
    {
        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, [Rule("float; tile, class:kitty")]);

        Assert.IsFalse(decision.Floating);
    }

    [TestMethod]
    [DataRow("no_blur; min_size 10 10")]
    [DataRow(";")]
    public void AChainWithNoEffectThatReadsAddsNoRule(string effects)
    {
        var document = ConfigParser.Parse($"windowrule = {effects}, class:kitty\n");

        Assert.IsNotEmpty(document.Warnings);
        Assert.IsEmpty(document.WindowRules);
    }

    private static WindowRule Rule(string text)
    {
        var parsed = WindowRuleParser.Parse(text);
        Assert.IsEmpty(parsed.Warnings);
        return parsed.Rule!;
    }
}
