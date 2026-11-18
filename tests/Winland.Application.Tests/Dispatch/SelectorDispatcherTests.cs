namespace Winland.Application.Tests;

[TestClass]
public class SelectorDispatcherTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    public void FocusWindowSwitchesToTheClientsWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2 silent, title:away\n"));
        Appear(core, 1, "app", "here");
        Appear(core, 2, "app", "away");

        var stayed = core.Handle(new Dispatch("focuswindow", "class:app"));

        Assert.AreEqual("ok", core.Execute("dispatch focuswindow class:app").Text);
        Assert.AreEqual(new WindowId(1), stayed.Focus);
        Assert.AreEqual(new WorkspaceId("1"), stayed.State.ActiveWorkspace);

        var followed = core.Handle(new Dispatch("focuswindow", "title:away"));

        Assert.AreEqual(new WindowId(2), followed.Focus);
        Assert.AreEqual(new WorkspaceId("2"), followed.State.ActiveWorkspace);
    }

    [TestMethod]
    public void FocusWindowMovesToTheClientsMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "left" },
        });
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "right" },
        });

        var outcome = core.Handle(new Dispatch("focuswindow", "class:right"));

        Assert.AreEqual(new WindowId(2), outcome.Focus);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), outcome.State.ActiveWorkspace);
    }

    [TestMethod]
    public void FocusWindowReportsAMiss()
    {
        var core = new WindowManager();
        Appear(core, 1, "app", "here");

        var reply = core.Execute("dispatch focuswindow class:missing");

        Assert.AreEqual("error: no such window", reply.Text);
        Assert.IsNull(reply.Effect.Focus);
    }

    [TestMethod]
    [DataRow("togglefloating class:b")]
    [DataRow("pin class:b")]
    [DataRow("pin enable class:b")]
    [DataRow("movetoworkspace 2,class:b")]
    [DataRow("movetoworkspacesilent 2,address:0x2")]
    [DataRow("swapwindow class:b")]
    [DataRow("moveintogroup address:0x2")]
    [DataRow("moveoutofgroup class:b")]
    [DataRow("moveoutofgroup active")]
    [DataRow("movewindow l,address:0x2")]
    [DataRow("fullscreen 1,address:0x2")]
    [DataRow("togglegroup ,class:b")]
    [DataRow("changegroupactive f,class:b")]
    public void ADispatcherGivenAWindowIsRefusedAndChangesNothing(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("focuswindow", "class:a");
        var before = desk.Send("j/clients");

        Assert.StartsWith("error:", desk.Dispatch(line), StringComparison.Ordinal);
        Assert.AreEqual(before, desk.Send("j/clients"));
    }

    [TestMethod]
    [DataRow("togglespecialworkspace work,notes")]
    [DataRow("workspace name:work,notes")]
    [DataRow("focusmonitor desc:Dell, Inc. U2720Q")]
    [DataRow("movecurrentworkspacetomonitor desc:Dell, Inc. U2720Q")]
    public void AWorkspaceOrMonitorNameMayHoldAComma(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");

        Assert.DoesNotContain("comma", desk.Dispatch(line), StringComparison.Ordinal);
    }

    [TestMethod]
    public void ExitStopsTheDaemon()
    {
        var core = new WindowManager();
        Appear(core, 1, "app", "term");

        var exit = core.Execute("dispatch exit");
        Assert.AreEqual("ok", exit.Text);
        Assert.IsTrue(exit.Effect.Exit);
        Assert.ContainsSingle(exit.Effect.State.Clients);
    }

    private static void Appear(WindowManager core, long window, string windowClass, string title, long processId = 0) =>
        core.Handle(new WindowAppeared(new WindowId(window), Wide)
        {
            Traits = WindowTraits.Normal with { Class = windowClass, Title = title },
            ProcessId = processId,
        });

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        CommandProtocol.FormatStream(reply.Effect.Events, reply.Effect.State);
}
