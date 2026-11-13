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
    public void ADraftRuleMatchesAnAppByItsAumidRatherThanItsSharedFrame()
    {
        var calculator = WindowTraits.Normal with
        {
            Class = "ApplicationFrameWindow",
            Exe = "ApplicationFrameHost.exe",
            Aumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
        };

        Assert.Equal(@"windowrule = float, aumid:Microsoft\.WindowsCalculator_8wekyb3d8bbwe!App", WindowRuleParser.Draft(floating: true, calculator));
        Assert.Equal(@"windowrule = tile, class:a\.b\x2C c", WindowRuleParser.Draft(floating: false, WindowTraits.Normal with { Class = "a.b, c" }));
    }

    [Theory]
    [InlineData(@" a.b, title:x #1 $HOME {{1+1}} \ (c)* [d]+? ^e| ")]
    [InlineData("tab\there \"quoted\" é中 😀")]
    [InlineData("negative:x")]
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

    [Fact]
    public void Windowrulev2IsAnAliasForWindowrule()
    {
        var document = ConfigParser.Parse("windowrulev2 = float on, class:kitty\n");

        Assert.Empty(document.Warnings);
        Assert.Equal(WindowRuleEffect.Float, Assert.Single(document.WindowRules).Effect);
        Assert.Equal("windowrule", Assert.Single(document.Settings).Keyword);
    }

    [Theory]
    [InlineData("notepad.exe", "Notepad.exe", true)]
    [InlineData("negative:notepad.exe", "Notepad.exe", false)]
    [InlineData("negative:notepad.exe", "other.exe", true)]
    public void ExePatternsIgnoreCase(string pattern, string exe, bool floating)
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
    public void APatternThatClosesAGroupItNeverOpenedIsRejected()
    {
        Assert.False(WindowRuleParser.TryParse("float, class:a)|(b", out _, out var error));
        Assert.Equal("Invalid window rule pattern.", error);
    }

    [Theory]
    [InlineData("no_blur banana, class:x")]
    [InlineData("opacity, class:x")]
    public void AnArgumentThatDoesNotReadDropsTheRule(string text)
    {
        Assert.False(WindowRuleParser.TryParse(text, out _, out var error));
        Assert.Equal("Malformed window rule.", error);
    }
}
