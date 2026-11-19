using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class SelectorDispatcherTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [TestMethod]
    public void FocusSelectorSwitchesToTheClientsWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2 stay, title:away\n"));
        Appear(core, 1, "app", "here");
        Appear(core, 2, "app", "away");

        var stayed = core.Handle(new Dispatch("focus", "class:app"));

        Assert.AreEqual("ok", core.Execute("focus class:app").Text);
        Assert.AreEqual(new WindowId(1), stayed.Focus);
        Assert.AreEqual(new WorkspaceId("1"), stayed.State.ActiveWorkspace);

        var followed = core.Handle(new Dispatch("focus", "title:away"));

        Assert.AreEqual(new WindowId(2), followed.Focus);
        Assert.AreEqual(new WorkspaceId("2"), followed.State.ActiveWorkspace);
    }

    [TestMethod]
    public void FocusSelectorMovesToTheClientsMonitor()
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

        var outcome = core.Handle(new Dispatch("focus", "class:right"));

        Assert.AreEqual(new WindowId(2), outcome.Focus);
        Assert.AreEqual(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), outcome.State.ActiveWorkspace);
    }

    [TestMethod]
    public void FocusSelectorMatchesFieldsAsAWindowRuleDoes()
    {
        var core = new WindowManager();
        Appear(core, 1, "Chrome_WidgetWin_1", "Release Notes - Code", exe: "Code.exe");
        Appear(core, 2, "CASCADIA_HOSTING_WINDOW_CLASS", "PowerShell", exe: "WindowsTerminal.exe");

        Assert.AreEqual(new WindowId(1), core.Handle(new Dispatch("focus", "exe:code.EXE")).Focus);
        Assert.AreEqual(new WindowId(2), core.Handle(new Dispatch("focus", "cascadia*")).Focus);
        Assert.AreEqual(new WindowId(1), core.Handle(new Dispatch("focus", "title:*notes*")).Focus);
        Assert.AreEqual(new WindowId(2), core.Handle(new Dispatch("focus", "exe:windows*")).Focus);
        Assert.AreEqual("error: no such window", core.Execute("focus title:Notes").Text);
        Assert.AreEqual("error: no such window", core.Execute("focus exe:Code").Text);
    }

    [TestMethod]
    public void FocusSelectorFindsAHiddenGroupMemberByItsExe()
    {
        var desk = new TestDesktop();
        desk.Spawn("a", traits: WindowTraits.Normal with { Exe = "first.exe" });
        desk.Ok("group");
        desk.Spawn("b", traits: WindowTraits.Normal with { Exe = "second.exe" });
        Assert.AreEqual("b", desk.ActiveClass());

        desk.Ok("focus", "exe:first.exe");

        Assert.AreEqual("a", desk.ActiveClass());
        Assert.AreEqual("found", JsonElement.Parse(desk.Send("j/inspect exe:second.exe")).GetProperty("status").GetString());
    }

    [TestMethod]
    public void FocusSelectorReportsAMiss()
    {
        var core = new WindowManager();
        Appear(core, 1, "app", "here");

        var reply = core.Execute("focus class:missing");

        Assert.AreEqual("error: no such window", reply.Text);
        Assert.IsNull(reply.Effect.Focus);
    }

    [TestMethod]
    [DataRow("float class:b")]
    [DataRow("pin class:b")]
    [DataRow("pin enable class:b")]
    [DataRow("move 2,class:b")]
    [DataRow("move 2,address:0x2 stay")]
    [DataRow("swap class:b")]
    [DataRow("join address:0x2")]
    [DataRow("leave-group class:b")]
    [DataRow("leave-group active")]
    [DataRow("move l,address:0x2")]
    [DataRow("fullscreen 1,address:0x2")]
    [DataRow("group ,class:b")]
    [DataRow("tab f,class:b")]
    public void ADispatcherGivenAWindowIsRefusedAndChangesNothing(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("focus", "class:a");
        var before = desk.Send("j/get windows");

        Assert.StartsWith("error:", desk.Dispatch(line), StringComparison.Ordinal);
        Assert.AreEqual(before, desk.Send("j/get windows"));
    }

    [TestMethod]
    [DataRow("scratchpad work,notes")]
    [DataRow("workspace name:work,notes")]
    [DataRow("focus-monitor desc:Dell, Inc. U2720Q")]
    [DataRow("move-workspace desc:Dell, Inc. U2720Q")]
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

        var exit = core.Execute("exit");
        Assert.AreEqual("ok", exit.Text);
        Assert.IsTrue(exit.Effect.Exit);
        Assert.ContainsSingle(exit.Effect.State.Clients);
    }

    private static void Appear(WindowManager core, long window, string windowClass, string title, long processId = 0, string exe = "") =>
        core.Handle(new WindowAppeared(new WindowId(window), Wide)
        {
            Traits = WindowTraits.Normal with { Class = windowClass, Title = title, Exe = exe },
            ProcessId = processId,
        });

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        CommandProtocol.FormatStream(reply.Effect.Events, reply.Effect.State);
}
