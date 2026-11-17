namespace Winland.Infrastructure.Tests;

public class GroupBarPainterTests
{
    private static readonly BorderColor Orange = new(0xFF, 0xFF, 0x80, 0x40);

    [Fact]
    public void TheBarIsNamedByItsTabsWithTheShownOneMarked()
    {
        var bar = new GroupBar(new WindowId(2), new Rectangle(0, 0, 100, 20),
            [new GroupTab(new WindowId(1), "one", false), new GroupTab(new WindowId(2), "two", true)], GroupBarStyle.Documented);

        Assert.Equal("one, Two live (shown)", GroupBarPainter.Name(bar, ["one", "Two live"]));
    }

    [Fact]
    public void TheInsertionMarkIsDrawnInTheActiveColorWhereTheDropJoins()
    {
        var style = GroupBarStyle.Documented with { Active = Orange, IndicatorHeight = 1 };
        var tabs = new[] { new GroupTab(new WindowId(1), "one", false), new GroupTab(new WindowId(2), "two", true) };
        var bar = new GroupBar(new WindowId(2), new Rectangle(0, 0, 6, 2), tabs, style) { Insertion = 1 };
        var pixels = new uint[12];
        Array.Fill(pixels, 0x01000000u);

        GroupBarPainter.MarkInsertion(pixels, bar);

        uint[] row = [0x01000000, 0x01000000, 0x01000000, 0xFFFF8040, 0x01000000, 0x01000000];
        Assert.Equal([.. row, .. row], pixels);

        var none = new uint[12];
        Array.Fill(none, 0x01000000u);
        GroupBarPainter.MarkInsertion(none, bar with { Insertion = null });
        Assert.All(none, pixel => Assert.Equal(0x01000000u, pixel));
    }

    [Fact]
    public void FullCoverageTakesTheTitleColorAndNoCoverageLeavesTheStrip()
    {
        uint[] pixels = [0x01000000, 0x01000000];
        uint[] mask = [0x00FFFFFF, 0x00000000];

        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(0, 0, 2, 1), Orange);

        Assert.Equal(0xFFFF8040u, pixels[0]);
        Assert.Equal(0x01000000u, pixels[1]);
    }

    [Fact]
    public void AnEdgeLaysThePartItCoversOverWhatIsUnder()
    {
        uint[] pixels = [0xFF0000FF];
        uint[] mask = [0x00808080];

        GroupBarPainter.ComposeText(pixels, mask, 1, 1, new Rectangle(0, 0, 1, 1), Orange);

        Assert.Equal(0xFF80409Fu, pixels[0]);
    }

    [Fact]
    public void TheColorsAlphaScalesTheCoverageAndAClearTitleStillTakesClicks()
    {
        uint[] pixels = [0x01000000, 0x01000000];
        uint[] mask = [0x00FFFFFF, 0x00FFFFFF];

        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(0, 0, 1, 1), Orange with { A = 128 });
        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(1, 0, 1, 1), Orange with { A = 0 });

        Assert.Equal(0x80804020u, pixels[0]);
        Assert.Equal(0x01000000u, pixels[1]);
    }
}
