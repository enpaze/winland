namespace Winland.Infrastructure.Tests;

[TestClass]
public partial class CursorIdleTests
{
    [TestMethod]
    public void KeyPressHidesUntilThePointerMoves()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));

        idle.KeyPressed();
        Assert.IsTrue(cursor.Hidden);

        idle.Moved();
        Assert.IsFalse(cursor.Hidden);
    }

    [TestMethod]
    public void KeyPressDoesNothingWhenHidingIsOff()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(false));

        idle.KeyPressed();

        Assert.AreEqual(0, cursor.Hides);
        Assert.IsFalse(cursor.Hidden);
    }

    [TestMethod]
    public void WarpMovesTheCursorAndKeepsItHiddenUntilTheMouseMoves()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));
        idle.KeyPressed();

        idle.Warp(new Point(12, 34));
        var hiddenAfterWarp = cursor.Hidden;
        idle.Moved();

        Assert.AreEqual(new Point(12, 34), cursor.Warped);
        Assert.IsTrue(hiddenAfterWarp);
        Assert.IsFalse(cursor.Hidden);
    }

    [TestMethod]
    public void TheCursorNeverHidesForBeingStill()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));

        idle.Moved();

        Assert.AreEqual(0, cursor.Hides);
        Assert.IsFalse(cursor.Hidden);
    }

    [TestMethod]
    public void TurningHideOnKeyOffShowsTheCursor()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));
        idle.KeyPressed();

        idle.Configure(new CursorBehavior(false));

        Assert.IsFalse(cursor.Hidden);
    }

    [TestMethod]
    public void RevealShowsTheCursorHoweverItWasHidden()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));
        idle.KeyPressed();
        idle.Touched();

        idle.Reveal();

        Assert.IsFalse(cursor.Hidden);
        Assert.AreEqual(1, cursor.Hides);
        Assert.AreEqual(1, cursor.Shows);
    }

    [TestMethod]
    public void AnotherSurfaceTakesOverTheCursor()
    {
        var first = new FakeCursor();
        var second = new FakeCursor();
        var idle = new CursorIdle(first);
        idle.Configure(new CursorBehavior(true));

        idle.Use(second);
        idle.KeyPressed();

        Assert.IsFalse(first.Hidden);
        Assert.IsTrue(second.Hidden);
        Assert.ThrowsExactly<ArgumentNullException>(() => idle.Use(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => idle.Configure(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CursorIdle(null!));
    }

    private sealed class FakeCursor : ICursorSurface
    {
        public int Hides { get; private set; }

        public int Shows { get; private set; }

        public bool Hidden { get; private set; }

        public Point? Warped { get; private set; }

        public void Warp(Point position) => Warped = position;

        public void Hide()
        {
            Hidden = true;
            Hides++;
        }

        public void Show()
        {
            Hidden = false;
            Shows++;
        }
    }
}
