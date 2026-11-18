namespace Winland.Application.Tests;

[TestClass]
public class OutcomeThenTests
{
    private static readonly State Empty = new([]);

    [TestMethod]
    public void RequestsJoinInOrderAndTheStateIsTheSecondsOwn()
    {
        var first = new Outcome([], Empty) { Closes = [new WindowId(1)], Events = [new ClientClosed(new WindowId(1))] };
        var after = new State([]) { ActiveWorkspace = new WorkspaceId("2") };
        var second = new Outcome([], after) { Closes = [new WindowId(2)], Events = [new ClientClosed(new WindowId(2))] };

        var both = first.Then(second);

        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], both.Closes);
        Assert.AreEqual(2, both.Events.Count);
        Assert.AreSame(after, both.State);
    }

    [TestMethod]
    public void TheTraitsOfBothAreKept()
    {
        var first = new Outcome([], Empty) { Traits = DispatcherTraits.WarpsToFocused };
        var second = new Outcome([], Empty) { Traits = DispatcherTraits.WarpsToEmptyMonitor };

        Assert.AreEqual(DispatcherTraits.WarpsToFocused | DispatcherTraits.WarpsToEmptyMonitor, first.Then(second).Traits);
        Assert.AreEqual(DispatcherTraits.WarpsToFocused, first.Then(new Outcome([], Empty)).Traits);
        Assert.AreEqual(DispatcherTraits.WarpsToEmptyMonitor, new Outcome([], Empty).Then(second).Traits);
        Assert.IsNull(new Outcome([], Empty).Then(new Outcome([], Empty)).Traits);
    }

    [TestMethod]
    public void TheFirstErrorWins()
    {
        var failed = new Outcome([], Empty) { Error = "error: no such workspace" };
        var later = new Outcome([], Empty) { Error = "error: window not found" };

        Assert.AreEqual("error: no such workspace", failed.Then(later).Error);
        Assert.AreEqual("error: window not found", new Outcome([], Empty).Then(later).Error);
    }

    [TestMethod]
    public void AValueTheSecondLeavesUnsetKeepsTheFirsts()
    {
        var warp = new WarpTo(new Point(5, 5));
        var first = new Outcome([], Empty, Focus: new WindowId(1)) { Warp = warp };

        var both = first.Then(new Outcome([], Empty));

        Assert.AreEqual(new WindowId(1), both.Focus);
        Assert.AreEqual(warp, both.Warp);
    }
}
