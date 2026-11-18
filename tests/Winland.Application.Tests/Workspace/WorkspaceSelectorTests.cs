namespace Winland.Application.Tests;

[TestClass]
public class WorkspaceSelectorTests
{
    [TestMethod]
    public void MonitorStepCyclesTheFocusedMonitorsWorkspaces()
    {
        var core = TwoMonitors(out var left, out var right);
        Appear(core, 1, left);
        Appear(core, 2, right);
        core.Handle(new Dispatch("workspace", "3"));
        Appear(core, 3, left);
        core.Handle(new Dispatch("workspace", "1"));

        var monitorNext = core.Handle(new Dispatch("workspace", "m+1"));
        Assert.AreEqual(new WorkspaceId("3"), monitorNext.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), monitorNext.State.ActiveMonitor);

        var monitorBack = core.Handle(new Dispatch("workspace", "m-1"));
        Assert.AreEqual(new WorkspaceId("1"), monitorBack.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), monitorBack.State.ActiveMonitor);

        Assert.AreEqual(new WorkspaceId("1"), core.Handle(new Dispatch("workspace", "m~1")).State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), core.Handle(new Dispatch("workspace", "m~9")).State.ActiveWorkspace);
        var secondOnMonitor = core.Handle(new Dispatch("workspace", "m~2"));
        Assert.AreEqual(new WorkspaceId("3"), secondOnMonitor.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), secondOnMonitor.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(secondOnMonitor, 2).Workspace);
    }

    [TestMethod]
    public void RelativeStepWalksThisMonitorsNumbers()
    {
        var core = TwoMonitors(out var left, out var right);
        Appear(core, 1, left);
        Appear(core, 2, right);

        var stepped = core.Handle(new Dispatch("workspace", "r+1"));
        Assert.AreEqual(new WorkspaceId("2"), stepped.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), stepped.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), Client(stepped, 2).Workspace);

        core.Handle(new Dispatch("workspace", "1"));
        Assert.AreEqual(new WorkspaceId("1"), core.Handle(new Dispatch("workspace", "r~1")).State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("2"), core.Handle(new Dispatch("workspace", "r~2")).State.ActiveWorkspace);

        var back = core.Handle(new Dispatch("workspace", "r-1"));
        Assert.AreEqual(new WorkspaceId("1"), back.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), back.State.ActiveMonitor);

        var named = core.Handle(new Dispatch("workspace", "name:notes"));
        Assert.AreEqual("error: no such workspace", named.Error);
        Assert.AreEqual(new WorkspaceId("1"), named.State.ActiveWorkspace);
    }

    [TestMethod]
    public void PreviousIsGlobalAndPreviousPerMonitorStaysOnThisMonitor()
    {
        var core = TwoMonitors(out var left, out var right);
        Appear(core, 1, left);
        Appear(core, 2, right);

        var none = core.Handle(new Dispatch("workspace", "previous"));
        Assert.AreEqual(new WorkspaceId("1"), none.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), none.State.ActiveMonitor);

        core.Handle(new Dispatch("workspace", "2"));
        var stayed = core.Handle(new Dispatch("workspace", "previous_per_monitor"));
        Assert.AreEqual(new WorkspaceId("1"), stayed.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), stayed.State.ActiveMonitor);

        var global = core.Handle(new Dispatch("workspace", "previous"));
        Assert.AreEqual(new WorkspaceId("2"), global.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), global.State.ActiveMonitor);

        var ready = VisitedBoth(out left, out right);
        var across = ready.Handle(new Dispatch("workspace", "previous"));
        Assert.AreEqual(WorkspaceId.FromNumber(2, new MonitorId(1)), across.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(1), across.State.ActiveMonitor);

        var local = VisitedBoth(out _, out _);
        var same = local.Handle(new Dispatch("workspace", "previous_per_monitor"));
        Assert.AreEqual(new WorkspaceId("4"), same.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), same.State.ActiveMonitor);
    }

    [TestMethod]
    [DataRow("preview")]
    [DataRow("prev_per_monitor")]
    [DataRow("previously")]
    [DataRow("r+notes")]
    [DataRow("m-x")]
    [DataRow("e~one")]
    [DataRow("e+1")]
    [DataRow("e-1")]
    [DataRow("e~2")]
    [DataRow("r+1x")]
    public void TextThatIsNotASelectorDoesNotNameAWorkspace(string name)
    {
        var core = TwoMonitors(out var left, out _);
        Appear(core, 1, left);
        core.Handle(new Dispatch("workspace", "2"));

        var named = core.Handle(new Dispatch("workspace", name));

        Assert.AreEqual("error: no such workspace", named.Error);
        Assert.AreEqual(new WorkspaceId("2"), named.State.ActiveWorkspace);
    }

    [TestMethod]
    public void EmptySelectorsHonorNextAndThisMonitor()
    {
        var core = TwoMonitors(out var left, out _);
        Appear(core, 1, left);

        Assert.AreEqual(new WorkspaceId("2"), core.Handle(new Dispatch("workspace", "empty")).State.ActiveWorkspace);
        core.Handle(new Dispatch("workspace", "1"));
        Assert.AreEqual(new WorkspaceId("2"), core.Handle(new Dispatch("workspace", "emptym")).State.ActiveWorkspace);
        core.Handle(new Dispatch("workspace", "1"));
        Assert.AreEqual(new WorkspaceId("2"), core.Handle(new Dispatch("workspace", "emptyn")).State.ActiveWorkspace);
        core.Handle(new Dispatch("workspace", "1"));
        Assert.AreEqual(new WorkspaceId("2"), core.Handle(new Dispatch("workspace", "emptynm")).State.ActiveWorkspace);

        var later = TwoMonitors(out left, out _);
        Appear(later, 1, left);
        later.Handle(new Dispatch("workspace", "5"));
        Appear(later, 5, left);
        Assert.AreEqual(new WorkspaceId("6"), later.Handle(new Dispatch("workspace", "emptyn")).State.ActiveWorkspace);
        later.Handle(new Dispatch("workspace", "5"));
        Assert.AreEqual(new WorkspaceId("2"), later.Handle(new Dispatch("workspace", "empty")).State.ActiveWorkspace);
    }

    [TestMethod]
    public void BracketSelectorDoesNotSwitch()
    {
        var core = TwoMonitors(out var left, out var right);
        Appear(core, 1, left);
        Appear(core, 2, right);

        var filtered = core.Handle(new Dispatch("workspace", "r[1-3]"));
        Assert.AreEqual(new WorkspaceId("1"), filtered.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), filtered.State.ActiveMonitor);
        Assert.AreEqual(new WorkspaceId("1"), Client(filtered, 1).Workspace);

        var moved = core.Handle(new Dispatch("movetoworkspace", "r[1-3]"));
        Assert.AreEqual(new WorkspaceId("1"), Client(moved, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("1"), moved.State.ActiveWorkspace);
    }

    [TestMethod]
    public void UnknownDispatcherIsAnErrorOnThePipeAndAWarningInABind()
    {
        var core = TwoMonitors(out var left, out _);
        Appear(core, 1, left);

        var reply = core.Execute("nosuch");
        Assert.AreEqual("error: unknown command 'nosuch'", reply.Text);
        Assert.AreEqual(new WorkspaceId("1"), reply.Effect.State.ActiveWorkspace);
        Assert.IsNull(reply.Effect.Close);

        var cased = core.Execute("NoSuch");
        Assert.AreEqual("error: unknown command 'NoSuch'", cased.Text);

        var loaded = core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, Q, madeup\n", dispatchers: WindowManager.DispatcherNames)));
        var warned = Assert.ContainsSingle(loaded.Events.OfType<ConfigWarned>());
        Assert.AreEqual(1, warned.Line);
        Assert.AreEqual("Unknown dispatcher 'madeup'.", warned.Message);

        core.Handle(new KeyPressed("Super_L"));
        var pressed = core.Handle(new KeyPressed("Q"));
        Assert.IsFalse(pressed.Consumed);
        Assert.IsNull(pressed.Close);
        Assert.AreEqual(new WorkspaceId("1"), pressed.State.ActiveWorkspace);
    }

    [TestMethod]
    public void BadLayoutMessageReturnsAnErrorAndAKnownOneStillRuns()
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(
            "general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n")));
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        var unknown = core.Execute("layoutmsg nosuch");
        Assert.AreEqual("error: bad layoutmsg 'nosuch'", unknown.Text);
        Assert.IsEmpty(unknown.Effect.Placements);

        var missing = core.Execute("layoutmsg");
        Assert.AreEqual("error: bad layoutmsg", missing.Text);

        var ran = core.Execute("layoutmsg togglesplit");
        Assert.AreEqual("ok", ran.Text);
        Assert.AreEqual(new Rectangle(0, 0, 1920, 540), BoundsOf(ran.Effect, 1));
        Assert.AreEqual(new Rectangle(0, 540, 1920, 540), BoundsOf(ran.Effect, 2));
    }

    private static WindowManager VisitedBoth(out Monitor left, out Monitor right)
    {
        var core = TwoMonitors(out left, out right);
        Appear(core, 1, left);
        Appear(core, 2, right);
        core.Handle(new Dispatch("workspace", "4"));
        Appear(core, 4, left);
        core.Handle(new Dispatch("workspace", "1"));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new Dispatch("focusmonitor", left.Name));
        return core;
    }

    private static WindowManager TwoMonitors(out Monitor left, out Monitor right)
    {
        left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1080));
        right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024));
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([left, right]));
        return core;
    }

    private static void Appear(WindowManager core, long window, Monitor monitor) =>
        core.Handle(new WindowAppeared(new WindowId(window), monitor.WorkArea));

    private static Workspace Workspace(Outcome outcome, string name) =>
        Assert.ContainsSingle(workspace => workspace.Id == new WorkspaceId(name), outcome.State.Workspaces);
}
