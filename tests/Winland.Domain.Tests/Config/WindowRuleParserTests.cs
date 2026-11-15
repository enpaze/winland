namespace Winland.Domain.Tests;

public class WindowRuleParserTests
{
    [Theory]
    [InlineData("float")]
    [InlineData("tile")]
    [InlineData("ignore")]
    public void AdoptionEffectsAcceptAnExplicitBoolean(string effect)
    {
        Assert.True(WindowRuleParser.TryParse($"{effect} on, class:kitty", out var enabled, out _));
        Assert.True(enabled!.Enabled);
        Assert.True(WindowRuleParser.TryParse($"{effect} off, class:kitty", out var disabled, out _));
        Assert.False(disabled!.Enabled);
        Assert.Equal(
            WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, []),
            WindowRuleParser.Decide(WindowTraits.Normal with { Class = "kitty" }, [disabled]));
    }

    [Fact]
    public void ADraftRulePrefersTheExecutableThenTheClassThenTheTitle()
    {
        var calculator = WindowTraits.Normal with
        {
            Class = "ApplicationFrameWindow",
            Exe = "ApplicationFrameHost.exe",
            Title = "Calculator",
            Aumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
        };

        Assert.Equal("windowrule = float, exe:ApplicationFrameHost.exe", WindowRuleParser.Draft(floating: true, calculator));
        Assert.Equal("windowrule = tile, class:CabinetWClass", WindowRuleParser.Draft(floating: false, WindowTraits.Normal with { Class = "CabinetWClass", Title = "Home" }));
        Assert.Equal("windowrule = float, title:Release Notes", WindowRuleParser.Draft(floating: true, WindowTraits.Normal with { Title = "Release Notes" }));
        Assert.Null(WindowRuleParser.Draft(floating: false, WindowTraits.Normal with { Class = "a.b, c" }));
    }

    [Theory]
    [InlineData("Release Notes")]
    [InlineData("tab there quoted")]
    public void ADraftRuleMatchesOnlyTheTitleItWasWrittenFrom(string title)
    {
        var window = WindowTraits.Normal with { Title = title };

        var draft = WindowRuleParser.Draft(floating: true, window);

        Assert.NotNull(draft);
        var rules = ConfigParser.Parse(draft + "\n").WindowRules;
        Assert.True(WindowRuleParser.Decide(window, rules).Floating);
        Assert.False(WindowRuleParser.Decide(window with { Title = "X" + title }, rules).Floating);
        Assert.False(WindowRuleParser.Decide(window with { Title = title.Replace('a', 'b').Replace('x', 'y') + "z" }, rules).Floating);
    }

    [Fact]
    public void AWindowWithNothingToMatchGetsNoDraftRule() =>
        Assert.Null(WindowRuleParser.Draft(floating: true, WindowTraits.Normal));

    [Theory]
    [InlineData(false, false, false, FloatReason.Default)]
    [InlineData(true, false, false, FloatReason.Topmost)]
    [InlineData(false, true, false, FloatReason.Dialog)]
    [InlineData(false, false, true, FloatReason.FixedSize)]
    internal void ADecisionSaysWhyAWindowFloats(bool topmost, bool dialog, bool fixedSize, FloatReason reason)
    {
        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Topmost = topmost, Dialog = dialog, Resizable = !fixedSize }, []);

        Assert.Equal(reason, decision.FloatReason);
        Assert.Equal(reason != FloatReason.Default, decision.Floating);
    }

    [Fact]
    public void ARuleCannotAdoptAnOverlayAndADecisionSaysSo()
    {
        Assert.True(WindowRuleParser.TryParse("float, class:osd", out var rule, out _));

        var decision = WindowRuleParser.Decide(WindowTraits.Normal with { Class = "osd", Overlay = true }, [rule!]);

        Assert.False(decision.Adopt);
        Assert.Equal(Admission.Overlay, decision.Admission);
    }

    [Theory]
    [InlineData("match:class kitty, float on")]
    [InlineData("float on, match:class kitty")]
    public void MatchPrefixRulesWarnWithTheWinlandSyntax(string value)
    {
        var document = ConfigParser.Parse($"windowrule = {value}\n");

        Assert.Contains("windowrule = float on, class:kitty", Assert.Single(document.Warnings).Message);
        Assert.Empty(document.WindowRules);
    }

    [Theory]
    [InlineData("initial_class")]
    [InlineData("initial_title")]
    [InlineData("aumid")]
    [InlineData("negative")]
    public void ARemovedMatcherWarnsAndTheRuleBesideItApplies(string field)
    {
        var document = ConfigParser.Parse($"windowrule = float on, {field}:kitty\nwindowrule = pin on, class:notes\n");

        var warning = Assert.Single(document.Warnings);
        Assert.Equal(1, warning.Line);
        Assert.StartsWith($"Unsupported window rule match '{field}'.", warning.Message, StringComparison.Ordinal);
        Assert.Equal(WindowRuleEffect.Pin, Assert.Single(document.WindowRules).Effect);
    }

    [Fact]
    public void Windowrulev2WarnsAndTheWindowruleBesideItStillApplies()
    {
        var document = ConfigParser.Parse("windowrulev2 = float on, class:kitty\nwindowrule = pin on, class:notes\n");

        var warning = Assert.Single(document.Warnings);
        Assert.Equal(1, warning.Line);
        Assert.StartsWith("Unsupported keyword 'windowrulev2'.", warning.Message, StringComparison.Ordinal);
        Assert.Equal(WindowRuleEffect.Pin, Assert.Single(document.WindowRules).Effect);
    }

    [Theory]
    [InlineData("notepad.exe", "Notepad.exe", true)]
    [InlineData("NOTEPAD.EXE", "notepad.exe", true)]
    [InlineData("other.exe", "Notepad.exe", false)]
    [InlineData("*pad.exe", "Notepad.exe", true)]
    [InlineData("note*", "other.exe", false)]
    public void ExeClassAndTitleMatchTheWholeFieldWithoutCase(string pattern, string exe, bool floating)
    {
        var document = ConfigParser.Parse($"windowrule = float, exe:{pattern}\n");

        Assert.Equal(floating, WindowRuleParser.Decide(WindowTraits.Normal with { Exe = exe }, document.WindowRules).Floating);
    }

    [Fact]
    public void ACommaInsideAPatternStaysInThePattern()
    {
        Assert.True(WindowRuleParser.TryParse("float, title:a{1,2}, class:kitty", out var rule, out _));

        Assert.Equal("a{1,2}", rule!.TitlePattern);
        Assert.Equal("kitty", rule.ClassPattern);
    }

    [Fact]
    public void ATrailingOrDoubledCommaIsDropped()
    {
        Assert.True(WindowRuleParser.TryParse("float, class:foo,, title:bar,", out var rule, out _));

        Assert.Equal("foo", rule!.ClassPattern);
        Assert.Equal("bar", rule.TitlePattern);
    }

    [Fact]
    public void ALiteralPatternMatchesThatTextAndAStarMatchesARun()
    {
        Assert.True(WindowRuleParser.TryParse("float, class:a)|(b", out var rule, out _));
        var literal = rule!;
        Assert.True(WindowRuleParser.Decide(WindowTraits.Normal with { Class = "a)|(b" }, [literal]).Floating);
        Assert.False(WindowRuleParser.Decide(WindowTraits.Normal with { Class = "ab" }, [literal]).Floating);

        Assert.True(WindowRuleParser.TryParse("float, title:Release *", out var titled, out _));
        var star = titled!;
        Assert.True(WindowRuleParser.Decide(WindowTraits.Normal with { Title = "release notes" }, [star]).Floating);
        Assert.False(WindowRuleParser.Decide(WindowTraits.Normal with { Title = "Notes" }, [star]).Floating);
    }

    [Theory]
    [InlineData("no_blur", "class:x")]
    [InlineData("min_size 10 10", "class:x")]
    [InlineData("scrolling_width 0.25", "class:x")]
    public void ARemovedEffectWarnsAndIsSkipped(string effect, string match)
    {
        var document = ConfigParser.Parse($"windowrule = {effect}, {match}\nwindowrule = float, class:kept\n");

        Assert.StartsWith("Unsupported window rule '", Assert.Single(document.Warnings).Message, StringComparison.Ordinal);
        Assert.Equal(WindowRuleEffect.Float, Assert.Single(document.WindowRules).Effect);
    }

    [Theory]
    [InlineData("opacity, class:x")]
    [InlineData("size 10, class:x")]
    public void AnArgumentThatDoesNotReadDropsTheRule(string text)
    {
        Assert.False(WindowRuleParser.TryParse(text, out _, out var error));
        Assert.Equal("Malformed window rule.", error);
    }
}
