namespace Winland.Application.Tests;

[TestClass]
public class StateChangeTests
{
    private static readonly Client One = new(new ClientId(1), new WindowId(1));
    private static readonly Client Two = new(new ClientId(2), new WindowId(2), Workspace: WorkspaceId.Initial);
    private static readonly Client Three = new(new ClientId(3), new WindowId(3));

    [TestMethod]
    public void ClosedAndOpenedClientsKeepTheOrderTheirStatesListThem()
    {
        var change = StateChange.Between(new State([Three, One, Two]), new State([Two]));

        Assert.AreSequenceEqual([Three.Window, One.Window], change.Closed);
        Assert.IsEmpty(change.Opened);

        var back = StateChange.Between(new State([Two]), new State([One, Two, Three]));
        Assert.AreSequenceEqual([One.Window, Three.Window], back.Opened);
    }

    [TestMethod]
    public void EachClientIsFoundBeforeAndAfterByWindow()
    {
        var moved = Two with { Workspace = new WorkspaceId("2") };

        var change = StateChange.Between(new State([One, Two]), new State([One, moved]));

        Assert.AreEqual(WorkspaceId.Initial, change.Was[Two.Window].Workspace);
        Assert.AreEqual(new WorkspaceId("2"), change.Is[Two.Window].Workspace);
    }

    [TestMethod]
    public void AClientWasShownWhenItWasThereAndNotHidden()
    {
        var before = new State([One, Two]) { Hidden = [Two] };

        var change = StateChange.Between(before, new State([One, Two, Three]) { Hidden = [One] });

        Assert.IsTrue(change.WasShown(One.Window));
        Assert.IsFalse(change.WasShown(Two.Window));
        Assert.IsFalse(change.WasShown(Three.Window));
        Assert.Contains(One.Window, change.HiddenAfter);
    }
}
