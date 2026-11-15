namespace Winland.Domain.Tests;

public class BarShapesTests
{
    [Fact]
    public void PriorityWarnsAndTheBarLineBesideItStillApplies()
    {
        var document = ConfigParser.Parse("group:groupbar:priority = 5\ngroup:groupbar:font_size = 10\n");

        var warning = Assert.Single(document.Warnings);
        Assert.Equal(1, warning.Line);
        Assert.StartsWith("Unsupported keyword 'group:groupbar:priority'.", warning.Message, StringComparison.Ordinal);
        Assert.Equal(10, GroupOptions.From(document).Bar.FontSize);
        Assert.DoesNotContain("group:groupbar:priority", OptionTable.Names);
    }

    [Fact]
    public void OnlyTheOuterCornersOfTheOuterTabsAreRound()
    {
        Assert.Equal(Corners.All, BarShapes.Rounded(0, 1, 2));
        Assert.Equal(Corners.TopLeft | Corners.BottomLeft, BarShapes.Rounded(0, 3, 2));
        Assert.Equal(Corners.None, BarShapes.Rounded(1, 3, 2));
        Assert.Equal(Corners.TopRight | Corners.BottomRight, BarShapes.Rounded(2, 3, 2));
        Assert.Equal(Corners.None, BarShapes.Rounded(0, 1, 0));
    }

    [Fact]
    public void CoverageCutsOnlyTheRoundCorners()
    {
        var box = new Rectangle(10, 10, 20, 10);

        Assert.Equal(0, BarShapes.Coverage(9, 10, box, 4, Corners.All));
        Assert.Equal(1, BarShapes.Coverage(20, 15, box, 4, Corners.All));
        Assert.Equal(1, BarShapes.Coverage(10, 10, box, 0, Corners.All));
        Assert.Equal(1, BarShapes.Coverage(10, 10, box, 4, Corners.TopRight));

        var corner = BarShapes.Coverage(10, 10, box, 4, Corners.All);
        Assert.InRange(corner, 0, 0.25);
        Assert.Equal(corner, BarShapes.Coverage(29, 19, box, 4, Corners.All));
    }

    [Fact]
    public void RadiusIsAtMostHalfTheShorterSide()
    {
        var thin = new Rectangle(0, 0, 40, 4);

        Assert.Equal(1, BarShapes.Coverage(20, 0, thin, 40, Corners.All));
        Assert.True(BarShapes.Coverage(0, 0, thin, 40, Corners.All) < 1);
    }
}
