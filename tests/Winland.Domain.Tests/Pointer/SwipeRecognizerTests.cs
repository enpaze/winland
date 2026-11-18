namespace Winland.Domain.Tests;

[TestClass]
public class SwipeRecognizerTests
{
    private static readonly TrackpadGesture Horizontal = new(3, GestureAxis.Horizontal);

    [TestMethod]
    public void ASwipeShorterThanThePickDistancePicksNothing()
    {
        var swipe = new SwipeRecognizer();
        swipe.Begin();

        var step = swipe.Update(new SwipeUpdated(3, 4, 0), [Horizontal]);

        Assert.AreEqual(default, step);
        Assert.IsNull(swipe.Active);
    }

    [TestMethod]
    public void TheUpdateThatPicksTheGestureIsItsFirstStep()
    {
        var swipe = new SwipeRecognizer();
        swipe.Begin();

        swipe.Update(new SwipeUpdated(3, 3, 0), [Horizontal]);
        var picking = swipe.Update(new SwipeUpdated(3, 3, 1), [Horizontal]);
        var next = swipe.Update(new SwipeUpdated(3, -1, 0), [Horizontal]);

        Assert.AreEqual(new SwipeStep(Picked: true, Along: 3), picking);
        Assert.AreEqual(new SwipeStep(Picked: false, Along: -1), next);
        Assert.AreSame(Horizontal, swipe.Active);
    }

    [TestMethod]
    public void ASwipeThatMatchesNoGestureIsIgnoredUntilTheNextBegins()
    {
        var swipe = new SwipeRecognizer();
        swipe.Begin();

        swipe.Update(new SwipeUpdated(4, 10, 0), [Horizontal]);
        var later = swipe.Update(new SwipeUpdated(3, 10, 0), [Horizontal]);
        swipe.Begin();
        var again = swipe.Update(new SwipeUpdated(3, 10, 0), [Horizontal]);

        Assert.AreEqual(default, later);
        Assert.IsTrue(again.Picked);
    }

    [TestMethod]
    public void AVerticalSwipeDoesNotPickAHorizontalGesture()
    {
        var swipe = new SwipeRecognizer();
        swipe.Begin();

        var step = swipe.Update(new SwipeUpdated(3, 2, 10), [Horizontal]);

        Assert.AreEqual(default, step);
    }

    [TestMethod]
    public void AVerticalGestureReadsVerticalMovement()
    {
        var vertical = new TrackpadGesture(3, GestureAxis.Vertical);
        var swipe = new SwipeRecognizer();
        swipe.Begin();

        var step = swipe.Update(new SwipeUpdated(3, 2, 10), [vertical]);

        Assert.AreEqual(new SwipeStep(Picked: true, Along: 10), step);
    }

    [TestMethod]
    public void EndReturnsTheGestureAndIgnoresLaterUpdates()
    {
        var swipe = new SwipeRecognizer();
        swipe.Begin();
        swipe.Update(new SwipeUpdated(3, 10, 0), [Horizontal]);

        var ended = swipe.End();
        var after = swipe.Update(new SwipeUpdated(3, 10, 0), [Horizontal]);

        Assert.AreSame(Horizontal, ended);
        Assert.IsNull(swipe.Active);
        Assert.AreEqual(default, after);
    }
}
