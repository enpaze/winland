namespace Winland.Application.Tests;

[TestClass]
public class MonitorIdentityTests
{
    [TestMethod]
    public void WorkspacesReturnWhenWindowsRenamesTheMonitor()
    {
        var core = new WindowManager();
        var left = Panel(@"\\.\DISPLAY1", "DEL", "U2723QE", "LEFT", 0);
        var right = Panel(@"\\.\DISPLAY2", "SAM", "S27", "RIGHT", 1920);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new MonitorsChanged([left]));

        var renamed = Panel(@"\\.\DISPLAY4", "SAM", "S27", "RIGHT", 1920);
        var back = core.Handle(new MonitorsChanged([left, renamed]));

        var screen = Assert.ContainsSingle(candidate => candidate.Monitor.Name == @"\\.\DISPLAY4", back.State.Screens);
        Assert.AreEqual("SAM S27 RIGHT", screen.Monitor.Description);
        Assert.AreEqual(WorkspaceId.FromNumber(1, screen.Id), screen.ActiveWorkspace);
        Assert.AreEqual(screen.Id, Workspace(back, WorkspaceId.FromNumber(1, screen.Id)).Monitor);
        Assert.AreEqual(
            WorkspaceId.FromNumber(1, screen.Id),
            Assert.ContainsSingle(client => client.Window == new WindowId(2), back.State.Clients).Workspace);
        Assert.AreEqual(
            new WorkspaceId("1"),
            Assert.ContainsSingle(candidate => candidate.Monitor.Name == left.Name, back.State.Screens).ActiveWorkspace);
    }

    [TestMethod]
    public void IdenticalMonitorsAreNumberedInTheOrderTheyWereFirstSeen()
    {
        var core = new WindowManager();
        var first = Panel(@"\\.\DISPLAY1", "DEL", "U2723QE", "SAME", 0);
        core.Handle(new MonitorsChanged([first]));
        var second = Panel(@"\\.\DISPLAY2", "DEL", "U2723QE", "SAME", 1920);
        var outcome = core.Handle(new MonitorsChanged([second, first]));

        Assert.AreEqual("DEL U2723QE SAME", Description(outcome, @"\\.\DISPLAY1"));
        Assert.AreEqual("DEL U2723QE SAME #2", Description(outcome, @"\\.\DISPLAY2"));

        var third = Panel(@"\\.\DISPLAY3", "DEL", "U2723QE", "SAME", 3840);
        var three = core.Handle(new MonitorsChanged([third, second, first]));
        Assert.AreEqual("DEL U2723QE SAME", Description(three, @"\\.\DISPLAY1"));
        Assert.AreEqual("DEL U2723QE SAME #2", Description(three, @"\\.\DISPLAY2"));
        Assert.AreEqual("DEL U2723QE SAME #3", Description(three, @"\\.\DISPLAY3"));
    }

    [TestMethod]
    public void ARepluggedIdenticalMonitorKeepsItsNumberAndItsWorkspaces()
    {
        var core = new WindowManager();
        var first = Panel(@"\\.\DISPLAY1", "DEL", "U2723QE", "SAME", 0);
        var second = Panel(@"\\.\DISPLAY2", "DEL", "U2723QE", "SAME", 1920);
        core.Handle(new MonitorsChanged([first, second]));
        core.Handle(new WindowAppeared(new WindowId(1), first.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), second.WorkArea));
        core.Handle(new MonitorsChanged([first]));

        var renamed = Panel(@"\\.\DISPLAY9", "DEL", "U2723QE", "SAME", 1920);
        var back = core.Handle(new MonitorsChanged([renamed, first]));

        Assert.AreEqual("DEL U2723QE SAME #2", Description(back, @"\\.\DISPLAY9"));
        var screen = Assert.ContainsSingle(candidate => candidate.Monitor.Name == @"\\.\DISPLAY9", back.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(1, screen.Id), screen.ActiveWorkspace);
        Assert.AreEqual(screen.Id, Workspace(back, WorkspaceId.FromNumber(1, screen.Id)).Monitor);
        Assert.AreEqual(
            WorkspaceId.FromNumber(1, screen.Id),
            Assert.ContainsSingle(client => client.Window == new WindowId(2), back.State.Clients).Workspace);
        Assert.AreEqual(
            new WorkspaceId("1"),
            Assert.ContainsSingle(candidate => candidate.Monitor.Name == first.Name, back.State.Screens).ActiveWorkspace);
    }

    [TestMethod]
    public void DescriptionMatchingReadsTheIdentity()
    {
        var core = new WindowManager();
        var dell = Panel(@"\\.\DISPLAY1", "DEL", "U2723QE", "ABC", 0);
        var samsung = Panel(@"\\.\DISPLAY2", "SAM", "S27", "XYZ", 1920);
        core.Handle(new MonitorsChanged([dell, samsung]));
        core.Handle(new WindowAppeared(new WindowId(1), dell.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), samsung.WorkArea));

        var focused = core.Handle(new Dispatch("focus-monitor", "desc:SAM S27 XYZ"));
        Assert.AreEqual(new MonitorId(1), focused.State.ActiveMonitor);

        var numbered = core.Handle(new Dispatch("focus-monitor", "desc:friendly"));
        Assert.AreEqual("error: monitor not found", numbered.Error);

        var duplicate = Panel(@"\\.\DISPLAY3", "SAM", "S27", "XYZ", 3840);
        var both = core.Handle(new MonitorsChanged([dell, samsung, duplicate]));
        var byNumber = core.Handle(new Dispatch("focus-monitor", "desc:SAM S27 XYZ #2"));
        Assert.AreEqual(
            Assert.ContainsSingle(screen => screen.Monitor.Name == @"\\.\DISPLAY3", both.State.Screens).Id,
            byNumber.State.ActiveMonitor);
    }


    [TestMethod]
    public void MakeAloneDoesNotNumberMonitors()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        var first = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, area, Make: "DEL");
        var second = new Monitor(
            @"\\.\DISPLAY2",
            "Dell",
            1920,
            0,
            1920,
            1080,
            1,
            true,
            new Rectangle(1920, 0, 1920, 1080),
            Make: "DEL");

        var outcome = core.Handle(new MonitorsChanged([first, second]));

        Assert.AreEqual("Dell", Description(outcome, @"\\.\DISPLAY1"));
        Assert.AreEqual("Dell", Description(outcome, @"\\.\DISPLAY2"));
    }

    [TestMethod]
    public void AMonitorWithoutAModelOrSerialComesBackByItsWindowsName()
    {
        var core = new WindowManager();
        var left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1080));
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024));
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new MonitorsChanged([left]));

        var renamed = new Monitor(@"\\.\DISPLAY4", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024));
        var back = core.Handle(new MonitorsChanged([left, renamed]));

        Assert.AreEqual(new WorkspaceId("2"), Assert.ContainsSingle(client => client.Window == new WindowId(2), back.State.Clients).Workspace);
        Assert.AreEqual(new MonitorId(0), Workspace(back, new WorkspaceId("2")).Monitor);
        Assert.AreEqual("Samsung", Description(back, @"\\.\DISPLAY4"));
    }

    private static Monitor Panel(string name, string make, string model, string serial, int x)
    {
        var area = new Rectangle(x, 0, 1920, 1080);
        return new Monitor(name, "friendly", x, 0, 1920, 1080, 1, true, area, Make: make, Model: model, Serial: serial);
    }

    private static string Description(Outcome outcome, string name) =>
        Assert.ContainsSingle(monitor => monitor.Name == name, outcome.State.Monitors).Description;

    private static Rectangle WorkArea(Outcome outcome, string name) =>
        Assert.ContainsSingle(monitor => monitor.Name == name, outcome.State.Monitors).WorkArea;

    private static Workspace Workspace(Outcome outcome, WorkspaceId id) =>
        Assert.ContainsSingle(workspace => workspace.Id == id, outcome.State.Workspaces);
}
