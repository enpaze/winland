namespace Winland.Application.Tests;

public class ConfigFileOnlyTests
{
    [Theory]
    [InlineData("keyword general:hide_taskbar true")]
    [InlineData("keyword general:gaps_out 0")]
    [InlineData("keyword windowrule float, class:b")]
    [InlineData("getoption general:gaps_out")]
    [InlineData("setprop address:0x1 border_size 9")]
    [InlineData("getprop address:0x1 border_size")]
    [InlineData("dispatch setprop address:0x1 border_size 9")]
    [InlineData("[[BATCH]]keyword general:gaps_out 0 ; keyword general:border_size 9")]
    [InlineData("[[BATCH]]j/clients")]
    [InlineData("[[BATCH]]/reload")]
    public void NoRequestReadsOrChangesASetting(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        var core = desk.WindowManager;
        var applied = (core.HideTaskbar, core.RuleRevision);
        var windows = desk.Send("j/clients");
        var placed = desk.PlaceOf("a");

        Assert.StartsWith("error:", desk.Send(line), StringComparison.Ordinal);

        desk.Spawn("b");
        Assert.False(TestDesktop.Floating(desk.Client("b")));
        desk.Close("b");
        Assert.Equal(applied, (core.HideTaskbar, core.RuleRevision));
        Assert.Equal(placed, desk.PlaceOf("a"));
        Assert.Equal(windows, desk.Send("j/clients"));
    }

    [Fact]
    public void AReloadGivesEveryWindowTheRuleEffectsOfTheFileAsItIsNow()
    {
        const string options = "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 2\n";
        var rules = "windowrule = opacity 0.5, class:b\n";
        var core = new WindowManager(ConfigParser.Parse(options + rules));
        core.ReadConfig = () => new ConfigRead(ConfigParser.Parse(options + rules));
        var area = new Rectangle(0, 0, 1000, 800);
        core.Handle(new WindowAppeared(new WindowId(1), area) { Traits = WindowTraits.Normal with { Class = "a" } });
        var opened = core.Handle(new WindowAppeared(new WindowId(2), area) { Traits = WindowTraits.Normal with { Class = "b" } });

        rules = "windowrule = opacity 0.5, class:a\n";
        var reloaded = core.Execute("reload");

        Assert.Equal((255, 128), (AlphaOf(opened, 1), AlphaOf(opened, 2)));
        Assert.Equal("ok", reloaded.Text);
        Assert.Equal((128, 255), (AlphaOf(reloaded.Effect, 1), AlphaOf(reloaded.Effect, 2)));
    }

    private static int AlphaOf(Outcome outcome, long window) =>
        Assert.Single(outcome.Decorations, decoration => decoration.Client == new ClientId(window)).Alpha;
}
