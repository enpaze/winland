namespace Winland.Infrastructure.Tests;

[TestClass]
public class InputQueueTests
{
    private static InputQueue<string> Moves() =>
        new((waiting, next) => waiting.StartsWith("move", StringComparison.Ordinal) && next.StartsWith("move", StringComparison.Ordinal));

    [TestMethod]
    public void OnlyTheNewestWaitingMoveIsKept()
    {
        var queue = Moves();
        queue.Post("move 1");
        queue.Post("move 2");
        queue.Post("move 3");

        Assert.AreSequenceEqual(["move 3"], queue.Take());
    }

    [TestMethod]
    public void ItemsThatAddUpWaitAsOne()
    {
        var queue = new InputQueue<int>((_, _) => false, (waiting, next) => (waiting > 0 && next > 0, waiting + next));
        queue.Post(1);
        queue.Post(2);
        queue.Post(-5);
        queue.Post(3);

        TestSequence.Equal([3, -5, 3], queue.Take());
    }

    [TestMethod]
    public void MovesAreKeptInOrderAroundOtherInput()
    {
        var queue = Moves();
        queue.Post("move 1");
        queue.Post("move 2");
        queue.Post("bind a");
        queue.Post("bind b");
        queue.Post("move 3");
        queue.Post("move 4");

        Assert.AreSequenceEqual(["move 2", "bind a", "bind b", "move 4"], queue.Take());
    }

    [TestMethod]
    public void AMoveTakenByTheWorkerIsNotReplaced()
    {
        var queue = Moves();
        queue.Post("move 1");
        Assert.AreSequenceEqual(["move 1"], queue.Take());

        queue.Post("move 2");

        Assert.AreSequenceEqual(["move 2"], queue.Take());
    }

    [TestMethod]
    public void ACompletedQueueHandsOverWhatIsLeftAndThenNothing()
    {
        var queue = Moves();
        queue.Post("bind a");
        queue.Complete();

        Assert.IsFalse(queue.Post("bind b"));
        Assert.AreSequenceEqual(["bind a"], queue.Take());
        Assert.IsEmpty(queue.Take());
    }

    [TestMethod]
    public async Task TakeWaitsForThePoster()
    {
        var queue = Moves();
        var taken = Task.Run(() => queue.Take().ToArray());
        await Task.Delay(50);
        Assert.IsFalse(taken.IsCompleted);

        queue.Post("bind a");

        Assert.AreSequenceEqual(["bind a"], await taken.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
