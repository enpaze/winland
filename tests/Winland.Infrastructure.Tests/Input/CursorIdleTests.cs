namespace Winland.Infrastructure.Tests;

public partial class CursorIdleTests
{
    [Fact]
    public void KeyPressHidesUntilThePointerMoves()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));

        idle.KeyPressed();
        Assert.True(cursor.Hidden);

        idle.Moved();
        Assert.False(cursor.Hidden);
    }

    [Fact]
    public void KeyPressDoesNothingWhenHidingIsOff()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(false));

        idle.KeyPressed();

        Assert.Equal(0, cursor.Hides);
        Assert.False(cursor.Hidden);
    }

    [Fact]
    public void WarpMovesTheCursorAndKeepsItHiddenUntilTheMouseMoves()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));
        idle.KeyPressed();

        idle.Warp(new Point(12, 34));
        var hiddenAfterWarp = cursor.Hidden;
        idle.Moved();

        Assert.Equal(new Point(12, 34), cursor.Warped);
        Assert.True(hiddenAfterWarp);
        Assert.False(cursor.Hidden);
    }

    [Fact]
    public void TheCursorNeverHidesForBeingStill()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));

        idle.Moved();

        Assert.Equal(0, cursor.Hides);
        Assert.False(cursor.Hidden);
    }

    [Fact]
    public void TurningHideOnKeyOffShowsTheCursor()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));
        idle.KeyPressed();

        idle.Configure(new CursorBehavior(false));

        Assert.False(cursor.Hidden);
    }

    // Revealing on exit shows a cursor hidden both by a key and by touch.
    [Fact]
    public void RevealShowsTheCursorHoweverItWasHidden()
    {
        var cursor = new FakeCursor();
        var idle = new CursorIdle(cursor);
        idle.Configure(new CursorBehavior(true));
        idle.KeyPressed();
        idle.Touched();

        idle.Reveal();

        Assert.False(cursor.Hidden);
        Assert.Equal(1, cursor.Hides);
        Assert.Equal(1, cursor.Shows);
    }

    // The daemon starts with a stand-in and swaps in the real cursor when it installs the system.
    [Fact]
    public void AnotherSurfaceTakesOverTheCursor()
    {
        var first = new FakeCursor();
        var second = new FakeCursor();
        var idle = new CursorIdle(first);
        idle.Configure(new CursorBehavior(true));

        idle.Use(second);
        idle.KeyPressed();

        Assert.False(first.Hidden);
        Assert.True(second.Hidden);
        Assert.Throws<ArgumentNullException>(() => idle.Use(null!));
        Assert.Throws<ArgumentNullException>(() => idle.Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new CursorIdle(null!));
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
