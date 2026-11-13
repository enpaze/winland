namespace Winland.Domain.Tests;

public class StackedGroupBarTests
{
    // Each row: gaps_out 2 + indicator 3 + gap 0 + title 14 = 19. Three rows and the upper gap: 59.
    private static readonly GroupBarStyle Stacked = GroupBarStyle.Documented with { Stacked = true };

    private static GroupBar Bar(GroupBarStyle style, int height) =>
        new(
            new WindowId(1),
            new Rectangle(100, 50, 600, height),
            [new GroupTab(new WindowId(1), "a", true), new GroupTab(new WindowId(2), "b", false), new GroupTab(new WindowId(3), "c", false)],
            style);

    [Fact]
    public void StackedIsReadAndReservesOneRowPerTab()
    {
        Assert.True(GroupOptions.From(ConfigParser.Parse("group:groupbar:stacked = true\n")).Bar.Stacked);
        Assert.False(GroupBarStyle.Documented.Stacked);

        Assert.Equal(19, Stacked.OneBarHeight);
        Assert.Equal(59, Stacked.ReservedFor(3));
        var flat = Stacked with { Stacked = false };
        Assert.Equal(flat.Reserved, flat.ReservedFor(3));
    }

    [Fact]
    public void RowsRunFromTheFirstMemberAtTheTopToTheLastAtTheBottom()
    {
        var bar = Bar(Stacked, 59);

        Assert.Equal(600, bar.TabWidth);
        Assert.Equal(new Rectangle(0, 54, 600, 3), bar.IndicatorBounds(2));
        Assert.Equal(new Rectangle(0, 40, 600, 14), bar.TitleBounds(2));
        Assert.Equal(new Rectangle(0, 35, 600, 3), bar.IndicatorBounds(1));
        Assert.Equal(new Rectangle(0, 2, 600, 14), bar.TitleBounds(0));
        Assert.Equal(2, bar.DrawOrder(0));
        Assert.Equal(0, bar.DrawOrder(2));
    }

    [Fact]
    public void ClicksPickTheRowPastItsTopGap()
    {
        // Winland's row: ((59 - 2) - 2 * 3) / 3 = 17, plus gaps_out, is 19.
        var bar = Bar(Stacked, 59);

        Assert.Equal(-1, bar.TabAt(10, 1));
        Assert.Equal(0, bar.TabAt(10, 2));
        Assert.Equal(0, bar.TabAt(599, 18));
        Assert.Equal(-1, bar.TabAt(10, 19));
        Assert.Equal(1, bar.TabAt(10, 21));
        Assert.Equal(2, bar.TabAt(10, 56));
    }

    // The mark is as thick as the indicator, across the whole bar, on the line between the tabs a drop goes between.
    [Fact]
    public void TheInsertionMarkSitsWhereTheDropJoins()
    {
        // Rows meet at 2, 21, 40 and 59. The mark is centred on that line and kept inside the bar.
        var stacked = Bar(Stacked, 59);
        Assert.Null(stacked.Insertion);
        Assert.Equal(new Rectangle(0, 1, 600, 3), (stacked with { Insertion = 0 }).InsertionBounds());
        Assert.Equal(new Rectangle(0, 20, 600, 3), (stacked with { Insertion = 1 }).InsertionBounds());
        Assert.Equal(new Rectangle(0, 56, 600, 3), (stacked with { Insertion = 3 }).InsertionBounds());

        // Tabs of 198 with gaps_in 2: the gaps are at 198 and 398.
        var flat = Bar(GroupBarStyle.Documented, 21);
        Assert.Equal(new Rectangle(0, 0, 3, 21), (flat with { Insertion = 0 }).InsertionBounds());
        Assert.Equal(new Rectangle(198, 0, 3, 21), (flat with { Insertion = 1 }).InsertionBounds());
        Assert.Equal(new Rectangle(597, 0, 3, 21), (flat with { Insertion = 3 }).InsertionBounds());

        Assert.False(flat.SameAs(flat with { Insertion = 1 }));
    }

    [Fact]
    public void DropsCountRowsInsteadOfColumns()
    {
        var bar = Bar(Stacked, 59);

        Assert.Equal(0, bar.DropIndex(new Point(300, 50)));
        Assert.Equal(1, bar.DropIndex(new Point(300, 50 + 12)));
        Assert.Equal(3, bar.DropIndex(new Point(300, 50 + 58)));

        var flat = Bar(GroupBarStyle.Documented, 21);
        Assert.Equal(0, flat.DropIndex(new Point(100 + 50, 60)));
        Assert.Equal(1, flat.DropIndex(new Point(100 + 250, 60)));
    }
}
