namespace Winland.Infrastructure.Tests;

[TestClass]
public class SizingGrabTests
{
    [TestMethod]
    [DataRow(4)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(17)]
    public void AWindowThatNamesASizingBorderIsSized(int hit)
    {
        Assert.AreEqual(DragKind.Size, SizingGrab.Classify(hit));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(9)]
    [DataRow(18)]
    public void AWindowThatNamesItsTitleOrAButtonIsMoved(int hit)
    {
        Assert.AreEqual(DragKind.Move, SizingGrab.Classify(hit));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void AWindowWhoseEdgeLeftThePointerLeavesItToTheWindow(int hit)
    {
        Assert.AreEqual(DragKind.Unclear, SizingGrab.Classify(hit));
    }

    [TestMethod]
    public void AWindowThatDoesNotAnswerInTimeLeavesItToTheWindow()
    {
        Assert.AreEqual(DragKind.Unclear, SizingGrab.Classify(null));
    }

    [TestMethod]
    public void AChangeInSizeIsASizingEvenWhenThePositionChangesToo()
    {
        var start = new Rectangle(100, 100, 800, 600);

        Assert.AreEqual(DragKind.Size, SizingGrab.Resolve(start, start with { Width = 780 }));
        Assert.AreEqual(DragKind.Size, SizingGrab.Resolve(start, start with { Height = 620 }));
        Assert.AreEqual(DragKind.Size, SizingGrab.Resolve(start, new Rectangle(110, 100, 790, 600)));
    }

    [TestMethod]
    public void AChangeInPositionAloneIsAMove()
    {
        var start = new Rectangle(100, 100, 800, 600);

        Assert.AreEqual(DragKind.Move, SizingGrab.Resolve(start, start with { X = 130 }));
        Assert.AreEqual(DragKind.Move, SizingGrab.Resolve(start, start with { Y = 90 }));
    }

    [TestMethod]
    public void NoChangeLeavesTheDragUnclear()
    {
        var start = new Rectangle(100, 100, 800, 600);

        Assert.AreEqual(DragKind.Unclear, SizingGrab.Resolve(start, start));
    }
}
