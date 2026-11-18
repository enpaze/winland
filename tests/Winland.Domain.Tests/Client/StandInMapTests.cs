namespace Winland.Domain.Tests;

[TestClass]
public class StandInMapTests
{
    private static readonly FrameInsets Shadow = new(7, 0, 7, 8);

    [TestMethod]
    public void APaintedWindowIsScaledOntoAFrameOfItsShape()
    {
        var map = StandInMap.For(new Rectangle(50, 40, 200, 100), new Size(400, 200), Shadow, new Size(414, 208));

        Assert.AreEqual(new Rectangle(7, 0, 400, 200), map.Source);
        Assert.AreEqual(new Rectangle(0, 0, 200, 100), map.Destination);
    }

    [TestMethod]
    public void AWindowNotPaintedAtItsSizeYetFillsWhatItGainedFromItsEdges()
    {
        var map = StandInMap.For(new Rectangle(0, 0, 300, 150), new Size(400, 200), Shadow, new Size(214, 108));

        Assert.AreEqual(new Rectangle(7, 0, 196, 96), map.Source);
        Assert.AreEqual(new Rectangle(0, 0, 147, 72), map.Destination);
        Assert.AreEqual(new StandInPiece(new Rectangle(202, 0, 1, 96), new Rectangle(147, 0, 153, 150)), map.Right);
        Assert.AreEqual(new StandInPiece(new Rectangle(7, 95, 196, 1), new Rectangle(0, 72, 147, 78)), map.Bottom);
    }

    [TestMethod]
    public void APaintedWindowNeedsNoEdges()
    {
        var map = StandInMap.For(new Rectangle(0, 0, 300, 150), new Size(400, 200), Shadow, new Size(414, 208));

        Assert.IsNull(map.Right);
        Assert.IsNull(map.Bottom);
    }

    [TestMethod]
    public void AnOversizedWindowIsSqueezedIntoTheFrame()
    {
        var map = StandInMap.For(new Rectangle(0, 0, 100, 50), new Size(400, 200), FrameInsets.None, new Size(800, 400));

        Assert.AreEqual(new Rectangle(0, 0, 800, 400), map.Source);
        Assert.AreEqual(new Rectangle(0, 0, 100, 50), map.Destination);
    }

    [TestMethod]
    public void AFrameOfAnotherShapeCutsThePictureOff()
    {
        var map = StandInMap.For(new Rectangle(0, 0, 200, 200), new Size(400, 200), Shadow, new Size(414, 208));

        Assert.AreEqual(new Rectangle(7, 0, 200, 200), map.Source);
        Assert.AreEqual(new Rectangle(0, 0, 200, 200), map.Destination);
    }

    [TestMethod]
    public void AFrameOfAnotherShapeScalesThePictureEvenlyAsLittleAsCoversIt()
    {
        var map = StandInMap.For(new Rectangle(0, 0, 100, 80), new Size(400, 200), FrameInsets.None, null);

        Assert.AreEqual(new Rectangle(0, 0, 250, 200), map.Source);
        Assert.AreEqual(new Rectangle(0, 0, 100, 80), map.Destination);
    }

    [TestMethod]
    public void AnUnknownPaintedSizeIsTheSizeTheWindowWasGiven()
    {
        var map = StandInMap.For(new Rectangle(0, 0, 40, 20), new Size(400, 200), Shadow, null);

        Assert.AreEqual(new Rectangle(7, 0, 400, 200), map.Source);
        Assert.AreEqual(new Rectangle(0, 0, 40, 20), map.Destination);
    }

    [TestMethod]
    public void AFlatFrameKeepsAPixelSoItsDestinationIsValid()
    {
        var map = StandInMap.For(new Rectangle(0, 100, 400, 0), new Size(400, 200), FrameInsets.None, null);

        Assert.AreEqual(new Rectangle(0, 0, 400, 1), map.Destination);
    }
}
