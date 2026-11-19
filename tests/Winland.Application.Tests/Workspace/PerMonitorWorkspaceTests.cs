namespace Winland.Application.Tests;

[TestClass]
public class PerMonitorWorkspaceTests
{
    [TestMethod]
    public void EachMonitorHasItsOwnWorkspace3()
    {
        var core = new WindowManager();
        var left = Screen(@"\\.\DISPLAY1", "Dell", "P2415Q", "A", 0);
        var right = Screen(@"\\.\DISPLAY2", "Samsung", "S27", "B", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new Dispatch("workspace", "3"));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focus-monitor", right.Name));
        core.Handle(new Dispatch("workspace", "3"));
        var opened = core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var rightScreen = Assert.ContainsSingle(screen => screen.Monitor.Name == right.Name, opened.State.Screens);
        Assert.AreEqual(new WorkspaceId("3"), Client(opened, 1).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(3, rightScreen.Id), Client(opened, 2).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(3, rightScreen.Id), rightScreen.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), Assert.ContainsSingle(screen => screen.Monitor.Name == left.Name, opened.State.Screens).ActiveWorkspace);
    }

    [TestMethod]
    public void UnplugNumbersGuestsAfterTheHostsOwn()
    {
        var core = new WindowManager();
        var left = Screen(@"\\.\DISPLAY1", "Dell", "P2415Q", "A", 0);
        var right = Screen(@"\\.\DISPLAY2", "Samsung", "S27", "B", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focus-monitor", right.Name));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("workspace", "4"));
        core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea));

        var gone = core.Handle(new MonitorsChanged([left]));

        Assert.AreEqual(new WorkspaceId("1"), Client(gone, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("2"), Client(gone, 2).Workspace);
        Assert.AreEqual(new WorkspaceId("3"), Client(gone, 4).Workspace);
        Assert.AreEqual(new MonitorId(0), WorkspaceOf(gone, new WorkspaceId("2")).Monitor);
        Assert.AreEqual(new MonitorId(0), WorkspaceOf(gone, new WorkspaceId("3")).Monitor);
    }

    [TestMethod]
    public void UnplugNumbersGuestsAbove10When1To10AreFull()
    {
        var core = new WindowManager();
        var left = Screen(@"\\.\DISPLAY1", "Dell", "P2415Q", "A", 0);
        var right = Screen(@"\\.\DISPLAY2", "Samsung", "S27", "B", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        for (var number = 1; number <= 10; number++)
        {
            core.Handle(new Dispatch("focus-monitor", left.Name));
            core.Handle(new Dispatch("workspace", number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            core.Handle(new WindowAppeared(new WindowId(100 + number), left.WorkArea));
        }

        core.Handle(new Dispatch("focus-monitor", right.Name));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("workspace", "4"));
        core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea));

        var gone = core.Handle(new MonitorsChanged([left]));

        Assert.AreEqual(new WorkspaceId("11"), Client(gone, 2).Workspace);
        Assert.AreEqual(new WorkspaceId("12"), Client(gone, 4).Workspace);
        Assert.Contains(workspace => workspace.Id == new WorkspaceId("10"), gone.State.Workspaces);
        Assert.DoesNotContain(workspace => workspace.Id == new WorkspaceId("4", 1), gone.State.Workspaces);
    }

    [TestMethod]
    public void ReplugReturnsGuestsUnderTheirOldNumbersAndShowsThePreviousWorkspace()
    {
        var core = new WindowManager();
        var left = Screen(@"\\.\DISPLAY1", "Dell", "P2415Q", "A", 0);
        var right = Screen(@"\\.\DISPLAY2", "Samsung", "S27", "B", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new Dispatch("focus-monitor", right.Name));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("workspace", "4"));
        core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea));
        core.Handle(new MonitorsChanged([left]));

        var back = core.Handle(new MonitorsChanged([left, right]));

        var returned = Assert.ContainsSingle(screen => screen.Monitor.Name == right.Name, back.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(4, returned.Id), returned.ActiveWorkspace);
        Assert.AreEqual(WorkspaceId.FromNumber(2, returned.Id), Client(back, 2).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(4, returned.Id), Client(back, 4).Workspace);
        Assert.AreEqual(new WorkspaceId("1"), Assert.ContainsSingle(screen => screen.Monitor.Name == left.Name, back.State.Screens).ActiveWorkspace);
    }

    [TestMethod]
    public void MovingAWorkspaceKeepsItsNumberWhenTheTargetIsFree()
    {
        var core = new WindowManager();
        var left = Screen(@"\\.\DISPLAY1", "Dell", "P2415Q", "A", 0);
        var right = Screen(@"\\.\DISPLAY2", "Samsung", "S27", "B", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new Dispatch("workspace", "3"));
        core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea));

        var moved = core.Handle(new Dispatch("move-workspace", @"\\.\DISPLAY2"));

        var destination = Assert.ContainsSingle(screen => screen.Monitor.Name == right.Name, moved.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(3, destination.Id), Client(moved, 3).Workspace);
        Assert.AreEqual(destination.Id, WorkspaceOf(moved, WorkspaceId.FromNumber(3, destination.Id)).Monitor);
        Assert.DoesNotContain(workspace => workspace.Id == new WorkspaceId("3"), moved.State.Workspaces);
    }

    [TestMethod]
    public void MovingAWorkspaceTakesTheFirstFreeNumberWhenItsOwnIsTaken()
    {
        var core = new WindowManager();
        var left = Screen(@"\\.\DISPLAY1", "Dell", "P2415Q", "A", 0);
        var right = Screen(@"\\.\DISPLAY2", "Samsung", "S27", "B", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new Dispatch("workspace", "3"));
        core.Handle(new WindowAppeared(new WindowId(3), left.WorkArea));
        core.Handle(new Dispatch("focus-monitor", right.Name));
        core.Handle(new Dispatch("workspace", "3"));
        core.Handle(new WindowAppeared(new WindowId(8), right.WorkArea));
        core.Handle(new Dispatch("focus-monitor", left.Name));

        var moved = core.Handle(new Dispatch("move-workspace", @"\\.\DISPLAY2"));

        var destination = Assert.ContainsSingle(screen => screen.Monitor.Name == right.Name, moved.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(1, destination.Id), Client(moved, 3).Workspace);
        Assert.AreEqual(WorkspaceId.FromNumber(3, destination.Id), Client(moved, 8).Workspace);
    }

    private static Monitor Screen(string name, string make, string model, string serial, int x) =>
        new(name, "", x, 0, 1920, 1080, 1, true, new Rectangle(x, 0, 1920, 1080), make, model, serial);

    private static Workspace WorkspaceOf(Outcome outcome, WorkspaceId id) =>
        Assert.ContainsSingle(workspace => workspace.Id == id, outcome.State.Workspaces);
}
