namespace Winland.Infrastructure.Tests;

[TestClass]
public class GroupBarPainterTests
{
    private static readonly BorderColor Orange = new(0xFF, 0xFF, 0x80, 0x40);

    [TestMethod]
    public void TheBarIsNamedByItsTabsWithTheShownOneMarked()
    {
        var bar = new GroupBar(new WindowId(2), new Rectangle(0, 0, 100, 20),
            [new GroupTab(new WindowId(1), "one", false), new GroupTab(new WindowId(2), "two", true)], GroupBarStyle.Documented);

        Assert.AreEqual("one, Two live (shown)", GroupBarPainter.Name(bar, ["one", "Two live"]));
    }

    [TestMethod]
    public void TheInsertionMarkIsDrawnInTheActiveColorWhereTheDropJoins()
    {
        var style = GroupBarStyle.Documented with { Active = Orange, IndicatorHeight = 1 };
        var tabs = new[] { new GroupTab(new WindowId(1), "one", false), new GroupTab(new WindowId(2), "two", true) };
        var bar = new GroupBar(new WindowId(2), new Rectangle(0, 0, 6, 2), tabs, style) { Insertion = 1 };
        var pixels = new uint[12];
        Array.Fill(pixels, 0x01000000u);

        GroupBarPainter.MarkInsertion(pixels, bar);

        uint[] row = [0x01000000, 0x01000000, 0x01000000, 0xFFFF8040, 0x01000000, 0x01000000];
        Assert.AreSequenceEqual([.. row, .. row], pixels);

        var none = new uint[12];
        Array.Fill(none, 0x01000000u);
        GroupBarPainter.MarkInsertion(none, bar with { Insertion = null });
        foreach (var pixel in none)
{
    Assert.AreEqual(0x01000000u, pixel);
}
    }

    [TestMethod]
    public void FullCoverageTakesTheTitleColorAndNoCoverageLeavesTheStrip()
    {
        uint[] pixels = [0x01000000, 0x01000000];
        uint[] mask = [0x00FFFFFF, 0x00000000];

        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(0, 0, 2, 1), Orange);

        Assert.AreEqual(0xFFFF8040u, pixels[0]);
        Assert.AreEqual(0x01000000u, pixels[1]);
    }

    [TestMethod]
    public void AnEdgeLaysThePartItCoversOverWhatIsUnder()
    {
        uint[] pixels = [0xFF0000FF];
        uint[] mask = [0x00808080];

        GroupBarPainter.ComposeText(pixels, mask, 1, 1, new Rectangle(0, 0, 1, 1), Orange);

        Assert.AreEqual(0xFF80409Fu, pixels[0]);
    }

    [TestMethod]
    public void TheColorsAlphaScalesTheCoverageAndAClearTitleStillTakesClicks()
    {
        uint[] pixels = [0x01000000, 0x01000000];
        uint[] mask = [0x00FFFFFF, 0x00FFFFFF];

        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(0, 0, 1, 1), Orange with { A = 128 });
        GroupBarPainter.ComposeText(pixels, mask, 2, 1, new Rectangle(1, 0, 1, 1), Orange with { A = 0 });

        Assert.AreEqual(0x80804020u, pixels[0]);
        Assert.AreEqual(0x01000000u, pixels[1]);
    }
}
