namespace Winland.Application.Tests;

public class WindowPropsTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("Yes please", true)]
    [InlineData("on", true)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("enabled", false)]
    public void TruthyReadsAsThePinnedReleaseDoes(string text, bool expected) =>
        Assert.Equal(expected, WindowProps.Truthy(text));

    [Fact]
    public void ABorderSizeRuleShrinksOnlyItsWindowsTile()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 2\nwindowrule = border_size 0, class:^(bare)$\n")));
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Class = "bare" } });
        var outcome = core.Handle(new WindowAppeared(new WindowId(2), Area) { Traits = WindowTraits.Normal with { Class = "framed" } });

        Assert.Equal(new Rectangle(0, 0, 960, 1080), Assert.Single(outcome.Placements, placement => placement.Client == new ClientId(1)).Bounds);
        Assert.Equal(new Rectangle(962, 2, 956, 1076), Assert.Single(outcome.Placements, placement => placement.Client == new ClientId(2)).Bounds);
    }

    [Fact]
    public void BorderColorAndRoundingRulesAreDrawn()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = border_color rgb(ff0000) rgb(00ff00), class:.*\nwindowrule = rounding 8, class:.*\n"));
        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area));

        var decoration = Assert.Single(outcome.Decorations);
        Assert.Equal(new BorderColor(0xFF, 0xFF, 0x00, 0x00), decoration.Border);
        Assert.Equal(CornerPreference.Round, decoration.Corners);
    }

    [Theory]
    [InlineData("windowrule = border_size wide, class:.*", "border_size rule \"wide\" failed with: bad input")]
    [InlineData("windowrule = border_color nothing, class:.*", "border_color rule \"nothing\" has invalid color \"nothing\"")]
    public void AMalformedPropRuleWarns(string line, string warning)
    {
        var document = ConfigParser.Parse(line + "\n");

        Assert.Empty(document.WindowRules);
        Assert.Equal(new ConfigWarning(1, warning), Assert.Single(document.Warnings));
    }

}
