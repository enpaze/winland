namespace Winland.Application.Tests;

/// <summary>The config file is the only way to configure Winland: nothing sent to the Daemon changes a setting.</summary>
public class ConfigFileOnlyTests
{
    [Theory]
    [InlineData("keyword general:gaps_out 0")]
    [InlineData("keyword windowrule float, class:b")]
    [InlineData("setprop address:0x1 border_size 9")]
    [InlineData("dispatch setprop address:0x1 border_size 9")]
    [InlineData("[[BATCH]]keyword general:gaps_out 0 ; keyword general:border_size 9")]
    public void ARequestThatWouldChangeASettingIsRefusedAndChangesNothing(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        var before = desk.Send("j/clients");
        var placed = desk.PlaceOf("a");

        Assert.StartsWith("error:", desk.Send(line), StringComparison.Ordinal);
        desk.Spawn("b");
        desk.Close("b");

        Assert.Equal(placed, desk.PlaceOf("a"));
        Assert.Equal(before, desk.Send("j/clients"));
    }

    [Theory]
    [InlineData("getoption general:gaps_out")]
    [InlineData("getprop address:0x1 border_size")]
    public void ASettingIsReadFromTheFileNotFromTheDaemon(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        Assert.Equal("error: unknown request", desk.Send(line));
    }

    [Fact]
    public void AReloadGivesEveryWindowTheRuleEffectsOfTheFileAsItIsNow()
    {
        var text = "windowrule = border_size 9, class:a\n";
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 2\n" + text));
        core.ReadConfig = () => new ConfigRead(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 2\n" + text));
        var area = new Rectangle(0, 0, 1000, 800);
        core.Handle(new WindowAppeared(new WindowId(1), area) { Traits = WindowTraits.Normal with { Class = "a" } });
        var opened = core.Handle(new WindowAppeared(new WindowId(2), area) { Traits = WindowTraits.Normal with { Class = "b" } });

        text = "";
        var reloaded = core.Execute("reload");

        Assert.Equal(new Rectangle(9, 9, 482, 782), BoundsOf(opened, 1));
        Assert.Equal("ok", reloaded.Text);
        Assert.Equal(new Rectangle(2, 2, 496, 796), BoundsOf(reloaded.Effect, 1));
    }

    private static Rectangle BoundsOf(Outcome outcome, long client) =>
        Assert.Single(outcome.Placements, placement => placement.Client == new ClientId(client)).Bounds;
}
