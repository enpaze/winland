namespace Winland.Infrastructure.Tests;

public class QuickMoveTests
{
    private static readonly WindowId Window = new(0x7101);

    [Fact]
    public void AMoveThatKeepsTheSizeOnlyMovesTheWindow()
    {
        var decision = QuickMove.Decide(new Rectangle(110, 0, 400, 300), new Rectangle(100, 0, 400, 300), null, 0);

        Assert.True(decision.Send);
        Assert.True(decision.KeepSize);
    }

    [Fact]
    public void AMoveThatChangesTheSizeSendsTheSize()
    {
        var decision = QuickMove.Decide(new Rectangle(100, 0, 420, 300), new Rectangle(100, 0, 400, 300), null, 0);

        Assert.True(decision.Send);
        Assert.False(decision.KeepSize);
    }

    [Fact]
    public void AWindowAlreadyThereIsNotSentAnything()
    {
        var at = new Rectangle(100, 0, 400, 300);

        Assert.False(QuickMove.Decide(at, at, null, 0).Send);
    }

    [Fact]
    public void AWindowStillBusyWithTheLastFrameIsSkipped()
    {
        var decision = QuickMove.Decide(new Rectangle(140, 0, 400, 300), new Rectangle(100, 0, 400, 300), new Rectangle(120, 0, 400, 300), 0);

        Assert.False(decision.Send);
    }

    [Fact]
    public void AWindowThatTookTheLastFrameGetsTheNext()
    {
        var decision = QuickMove.Decide(new Rectangle(140, 0, 400, 300), new Rectangle(120, 0, 400, 300), new Rectangle(120, 0, 400, 300), 0);

        Assert.True(decision.Send);
    }

    [Fact]
    public void AWindowThatAnswersNothingForAWhileIsSentTheNewestFrameAnyway()
    {
        var target = new Rectangle(140, 0, 400, 300);
        var current = new Rectangle(100, 0, 400, 300);
        var requested = new Rectangle(120, 0, 400, 300);

        Assert.False(QuickMove.Decide(target, current, requested, QuickMove.MaxSkips - 1).Send);
        Assert.True(QuickMove.Decide(target, current, requested, QuickMove.MaxSkips).Send);
    }

    [Fact]
    public void RequestsDoNotPileUpBehindABusyWindow()
    {
        var moves = new QuickMove();
        var sent = new List<Rectangle>();
        var current = new Rectangle(0, 0, 400, 300);

        for (var step = 1; step <= QuickMove.MaxSkips + 2; step++)
            moves.Request(Window, current with { X = step * 10 }, current, (target, _) => sent.Add(target));

        Assert.Equal([current with { X = 10 }, current with { X = (QuickMove.MaxSkips + 2) * 10 }], sent);
    }

    [Fact]
    public void ARequestTrailsUntilTheWindowStandsOnItsTarget()
    {
        var moves = new QuickMove();
        var target = new Rectangle(10, 0, 400, 300);

        moves.Request(Window, target, new Rectangle(0, 0, 400, 300), (_, _) => { });

        Assert.True(moves.Trails(Window, new Rectangle(0, 0, 400, 300)));
        Assert.False(moves.Trails(Window, target));
    }

    [Fact]
    public void AMoveToWhereWinlandPutTheWindowIsWinlands()
    {
        var moves = new QuickMove();
        var target = new Rectangle(10, 0, 400, 300);
        moves.Note(Window, target, waiting: false);

        Assert.True(moves.Ours(Window, target));
        Assert.True(moves.Ours(Window, new Rectangle(10, 0, 380, 300)));
        Assert.False(moves.Ours(new WindowId(0x7103), target));
    }

    [Fact]
    public void AnEarlierFrameOfAMotionIsStillWinlands()
    {
        var moves = new QuickMove();
        moves.Note(Window, new Rectangle(10, 0, 400, 300), waiting: true);
        moves.Note(Window, new Rectangle(20, 0, 400, 300), waiting: true);

        Assert.True(moves.Ours(Window, new Rectangle(10, 0, 400, 300)));
    }

    [Fact]
    public void AMoveTheApplicationMadeItselfIsNotWinlands()
    {
        var moves = new QuickMove();
        moves.Note(Window, new Rectangle(145, 1223, 2263, 197), waiting: true);

        Assert.False(moves.Ours(Window, new Rectangle(449, 1249, 1655, 197)));
    }

    [Fact]
    public void AResizeInPlaceIsTakenOnlyOnceTheSizeLands()
    {
        var moves = new QuickMove();
        var current = new Rectangle(0, 0, 400, 300);
        moves.Request(Window, new Rectangle(0, 0, 800, 600), current, (_, _) => { });

        Assert.True(moves.Trails(Window, current));
        Assert.False(QuickMove.Decide(new Rectangle(0, 0, 900, 700), current, new Rectangle(0, 0, 800, 600), 0).Send);
    }

    [Fact]
    public void ARequestIsNotWaitedForPastThePatience()
    {
        var now = 0L;
        var moves = new QuickMove(() => now);
        var stale = new Rectangle(10, 0, 400, 300);
        var current = new Rectangle(0, 0, 400, 300);
        moves.Note(Window, stale, waiting: true);

        now = QuickMove.Patience + 1;

        Assert.False(moves.Trails(Window, current));
        Assert.False(moves.Ours(Window, current));
        Assert.True(moves.Request(Window, current with { X = 20 }, current, (_, _) => { }));
    }
}
