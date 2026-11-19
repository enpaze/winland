namespace Winland.Domain.Tests;

[TestClass]
public class BarShapesTests
{
    [TestMethod]
    public void PriorityWarnsAndTheBarLineBesideItStillApplies()
    {
        var document = ConfigParser.Parse("group:groupbar:priority = 5\ntab_font_size = 10\n");

        var warning = Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(1, warning.Line);
        Assert.StartsWith("Unsupported keyword 'group:groupbar:priority'.", warning.Message, StringComparison.Ordinal);
        Assert.AreEqual(10, GroupOptions.From(document).Bar.FontSize);
        Assert.DoesNotContain("group:groupbar:priority", OptionTable.Names);
    }

    [TestMethod]
    public void OnlyTheOuterCornersOfTheOuterTabsAreRound()
    {
        Assert.AreEqual(Corners.All, BarShapes.Rounded(0, 1, 2));
        Assert.AreEqual(Corners.TopLeft | Corners.BottomLeft, BarShapes.Rounded(0, 3, 2));
        Assert.AreEqual(Corners.None, BarShapes.Rounded(1, 3, 2));
        Assert.AreEqual(Corners.TopRight | Corners.BottomRight, BarShapes.Rounded(2, 3, 2));
        Assert.AreEqual(Corners.None, BarShapes.Rounded(0, 1, 0));
    }

    [TestMethod]
    public void CoverageCutsOnlyTheRoundCorners()
    {
        var box = new Rectangle(10, 10, 20, 10);

        Assert.AreEqual(0, BarShapes.Coverage(9, 10, box, 4, Corners.All));
        Assert.AreEqual(1, BarShapes.Coverage(20, 15, box, 4, Corners.All));
        Assert.AreEqual(1, BarShapes.Coverage(10, 10, box, 0, Corners.All));
        Assert.AreEqual(1, BarShapes.Coverage(10, 10, box, 4, Corners.TopRight));

        var corner = BarShapes.Coverage(10, 10, box, 4, Corners.All);
        Assert.IsInRange(0, 0.25, corner);
        Assert.AreEqual(corner, BarShapes.Coverage(29, 19, box, 4, Corners.All));
    }

    [TestMethod]
    public void RadiusIsAtMostHalfTheShorterSide()
    {
        var thin = new Rectangle(0, 0, 40, 4);

        Assert.AreEqual(1, BarShapes.Coverage(20, 0, thin, 40, Corners.All));
        Assert.IsTrue(BarShapes.Coverage(0, 0, thin, 40, Corners.All) < 1);
    }
}
