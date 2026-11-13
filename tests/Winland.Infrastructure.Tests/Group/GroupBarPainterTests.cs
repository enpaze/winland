namespace Winland.Infrastructure.Tests;

public class GroupBarPainterTests
{
    private static readonly BorderColor Orange = new(0xFF, 0xFF, 0x80, 0x40);

    // A screen reader reads the bar window's title, so it lists the tabs and marks the shown one.
    [Fact]
    public void TheBarIsNamedByItsTabsWithTheShownOneMarked()
    {
        var bar = new GroupBar(new WindowId(2), new Rectangle(0, 0, 100, 20),
            [new GroupTab(new WindowId(1), "one", false), new GroupTab(new WindowId(2), "two", true)], GroupBarStyle.Documented);

        Assert.Equal("one, Two live (shown)", GroupBarPainter.Name(bar, ["one", "Two live"]));
    }

    // The place a held window would join is drawn in the active indicator color over whatever the strip has there.
    [Fact]
    public void TheInsertionMarkIsDrawnInTheActiveColorWhereTheDropJoins()
    {
        var style = GroupBarStyle.Documented with { Active = Orange, IndicatorHeight = 1 };
        var tabs = new[] { new GroupTab(new WindowId(1), "one", false), new GroupTab(new WindowId(2), "two", true) };
        var bar = new GroupBar(new WindowId(2), new Rectangle(0, 0, 6, 2), tabs, style) { Insertion = 1 };
        var pixels = new uint[12];
        Array.Fill(pixels, 0x01000000u);

        GroupBarPainter.MarkInsertion(pixels, bar);

        // Tabs of 2 with gaps_in 2: the gap is pixels 2 and 3, and the one-pixel mark is on its middle line, at 3.
        uint[] row = [0x01000000, 0x01000000, 0x01000000, 0xFFFF8040, 0x01000000, 0x01000000];
        Assert.Equal([.. row, .. row], pixels);

        var none = new uint[12];
        Array.Fill(none, 0x01000000u);
        GroupBarPainter.MarkInsertion(none, bar with { Insertion = null });
        Assert.All(none, pixel => Assert.Equal(0x01000000u, pixel));
    }

    // Where the mask is white the title's color covers the pixel. Where it is black the strip keeps what it had.
    [Fact]
    public void FullCoverageTakesTheTitleColorAndNoCoverageLeavesTheStrip()
    {
        uint[] pixels = [0x01000000, 0x01000000];
        uint[] mask = [0x00FFFFFF, 0x00000000];

        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(0, 0, 2, 1), Orange);

        Assert.Equal(0xFFFF8040u, pixels[0]);
        Assert.Equal(0x01000000u, pixels[1]);
    }

    // An antialiased edge covers part of a pixel, so the color goes over the strip at that share, premultiplied.
    [Fact]
    public void AnEdgeLaysThePartItCoversOverWhatIsUnder()
    {
        uint[] pixels = [0xFF0000FF];
        uint[] mask = [0x00808080];

        GroupBarPainter.ComposeText(pixels, mask, 1, 1, new Rectangle(0, 0, 1, 1), Orange);

        Assert.Equal(0xFF80409Fu, pixels[0]);
    }

    // The title color's own alpha scales the coverage, and a clear title still leaves the strip's alpha of 1 for clicks.
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
