namespace Winland.Application.Tests;

[TestClass]
public class SpecialWorkspaceTests
{
    private static readonly Rectangle Area = new(0, 0, 800, 600);

    [TestMethod]
    public void AClientCanBeSentToASpecialWorkspaceAndToggled()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));

        var moved = core.Handle(new Dispatch("move", "special:scratch"));

        Assert.AreEqual(WorkspaceId.Initial, moved.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("special:scratch"), Client(moved, 2).Workspace);
        Assert.AreEqual(WorkspaceId.Initial, Client(moved, 1).Workspace);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), moved.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), moved.State.Hidden);
        Assert.AreEqual(new WorkspaceId("special:scratch"), moved.State.SpecialWorkspace);
        Assert.AreEqual(new WindowId(2), moved.Focus);

        var closed = core.Handle(new Dispatch("scratchpad", "scratch"));

        Assert.AreEqual(WorkspaceId.Initial, closed.State.ActiveWorkspace);
        Assert.IsNull(closed.State.SpecialWorkspace);
        Assert.Contains(window => window == new WindowId(2), closed.Hide);
        Assert.Contains(client => client.Window == new WindowId(2), closed.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), closed.State.Hidden);
        Assert.AreEqual(new WindowId(1), closed.Focus);

        var opened = core.Handle(new Dispatch("scratchpad", "scratch"));

        Assert.AreEqual(WorkspaceId.Initial, opened.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("special:scratch"), opened.State.SpecialWorkspace);
        Assert.Contains(window => window == new WindowId(2), opened.Show);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), opened.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), opened.State.Hidden);
        Assert.AreEqual(new WindowId(2), opened.Focus);
    }

    [TestMethod]
    public void AnOpenedSpecialWorkspaceIsRaisedAboveTheNormalWorkspace()
    {
        var core = new WindowManager();
        foreach (var window in new long[] { 1, 2, 3, 4 })
            core.Handle(Appeared(window, Area));
        core.Handle(new WindowFocused(new WindowId(4), FocusCause.User));
        core.Handle(new Dispatch("float"));
        foreach (var window in new long[] { 2, 3, 4 })
        {
            core.Handle(new WindowFocused(new WindowId(window), FocusCause.User));
            core.Handle(new Dispatch("move", "special:scratch stay"));
        }

        var opened = core.Handle(new Dispatch("scratchpad", "scratch"));

        Assert.AreSequenceEqual(
            [new ZOrderRequest(new WindowId(2), true), new ZOrderRequest(new WindowId(3), true), new ZOrderRequest(new WindowId(4), true)],
            opened.ZOrder.Where(request => request.Window != new WindowId(1)));
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2), new WindowId(3), new WindowId(4)], opened.State.Stacking);
    }

    [TestMethod]
    [DataRow("special:scratch")]
    [DataRow("2")]
    public void AMoveThatStaysGivesTheKeyboardToTheWindowLeftFocused(string workspace)
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));

        var moved = core.Handle(new Dispatch("move", workspace + " stay"));

        Assert.AreEqual(new WindowId(1), moved.Focus);
    }

    [TestMethod]
    public void OneSpecialWorkspaceIsShownPerMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 800, 600);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 800, 0, 800, 600);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        core.Handle(new Dispatch("move", "special:notes"));
        var second = core.Handle(new Dispatch("focus-monitor", right.Name));
        Assert.AreEqual(new MonitorId(1), second.State.ActiveMonitor);
        var other = core.Handle(new Dispatch("move", "special:term"));

        Assert.AreEqual(new WorkspaceId("special:notes"), Screen(other, 0).SpecialWorkspace);
        Assert.AreEqual(new WorkspaceId("special:term"), Screen(other, 1).SpecialWorkspace);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), other.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), other.State.Hidden);

        var replaced = core.Handle(new Dispatch("scratchpad", "notes"));

        Assert.IsNull(Screen(replaced, 0).SpecialWorkspace);
        Assert.AreEqual(new WorkspaceId("special:notes"), Screen(replaced, 1).SpecialWorkspace);
        Assert.Contains(client => client.Window == new WindowId(2), replaced.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), replaced.State.Hidden);
    }

    [TestMethod]
    public void AddressesRulesAndEventsCoverSpecial()
    {
        var core = new WindowManager(ConfigParser.Parse("""
            windowrule = workspace special:scratch stay, class:quiet
            windowrule = workspace special, class:loud
            """));
        var display = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 800, 600);
        core.Handle(new MonitorsChanged([display]));
        core.Handle(Appeared(1, Area));

        var stayed = core.Handle(Appeared(2, Area, windowClass: "quiet"));
        Assert.AreEqual(new WorkspaceId("special:scratch"), Client(stayed, 2).Workspace);
        Assert.IsNull(stayed.State.SpecialWorkspace);
        Assert.Contains(window => window == new WindowId(2), stayed.Hide);
        Assert.DoesNotContain(line => line.StartsWith("activespecial", StringComparison.Ordinal), Lines(stayed));

        var named = core.Handle(new Dispatch("workspace", "special:scratch"));
        Assert.AreSequenceEqual(
            [
                @"activespecial>>-98,special:scratch,\\.\DISPLAY1",
                @"focus>>\\.\DISPLAY1,1,1,2,",
            ],
            Lines(named));
        Assert.AreEqual(WorkspaceId.Initial, named.State.ActiveWorkspace);

        var same = core.Handle(new Dispatch("workspace", "special"));
        Assert.AreEqual(WorkspaceId.DefaultSpecial, same.State.SpecialWorkspace);
        Assert.Contains(window => window == new WindowId(2), same.Hide);
        Assert.AreSequenceEqual(
            [
                @"createworkspace>>-99,special:special,\\.\DISPLAY1",
                @"activespecial>>-99,special:special,\\.\DISPLAY1",
                @"focus>>\\.\DISPLAY1,1,1,,",
            ],
            Lines(same));

        var loud = core.Handle(Appeared(3, Area, windowClass: "loud"));
        Assert.AreEqual(WorkspaceId.DefaultSpecial, Client(loud, 3).Workspace);
        Assert.AreEqual(WorkspaceId.DefaultSpecial, loud.State.SpecialWorkspace);

        var closed = core.Handle(new Dispatch("scratchpad", ""));
        Assert.AreSequenceEqual(
            [
                @"activespecial>>,,\\.\DISPLAY1",
                @"focus>>\\.\DISPLAY1,1,1,1,",
            ],
            Lines(closed));
        Assert.IsNull(closed.State.SpecialWorkspace);

        var monitors = Json(core.Execute("j/get monitors").Text);
        Assert.IsFalse(monitors[0].GetProperty("specialWorkspace").TryGetProperty("id", out _));
        Assert.AreEqual("", monitors[0].GetProperty("specialWorkspace").GetProperty("name").GetString());

        core.Handle(new Dispatch("scratchpad", ""));
        var open = Json(core.Execute("j/get monitors").Text);
        Assert.IsFalse(open[0].GetProperty("specialWorkspace").TryGetProperty("id", out _));
        Assert.AreEqual("special", open[0].GetProperty("specialWorkspace").GetProperty("type").GetString());
        Assert.AreEqual("special:special", open[0].GetProperty("specialWorkspace").GetProperty("name").GetString());
        Assert.AreEqual(1, open[0].GetProperty("activeWorkspace").GetProperty("id").GetInt32());
    }

    [TestMethod]
    public void TheSpecialWorkspaceHidesWhenItsLastClientLeaves()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(new Dispatch("move", "special"));
        var closed = core.Handle(new WindowClosed(new WindowId(1)));

        Assert.IsNull(closed.State.SpecialWorkspace);
        Assert.IsEmpty(closed.State.Clients);
    }

    [TestMethod]
    public void ChangingWorkspaceHidesTheSpecialWorkspace()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(new Dispatch("move", "special:scratch"));
        var changed = core.Handle(new Dispatch("workspace", "2"));

        Assert.AreEqual(new WorkspaceId("2"), changed.State.ActiveWorkspace);
        Assert.IsNull(changed.State.SpecialWorkspace);
        Assert.Contains(client => client.Window == new WindowId(1), changed.State.Hidden);
    }

    [TestMethod]
    public void FallthroughKeepsTheSpecialWorkspaceOpenWhenFocusLeavesIt()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        core.Handle(new Dispatch("move", "special"));

        var closed = core.Handle(new Dispatch("focus", "address:0x1"));

        Assert.IsNull(closed.State.SpecialWorkspace);
        Assert.AreEqual(new WindowId(1), closed.Focus);
        Assert.Contains(client => client.Window == new WindowId(2), closed.State.Hidden);
    }

    [TestMethod]
    public void AMoveThatStaysDoesNotShowTheSpecialWorkspace()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        var moved = core.Handle(new Dispatch("move", "special:scratch stay"));

        Assert.IsNull(moved.State.SpecialWorkspace);
        Assert.AreEqual(new WorkspaceId("special:scratch"), Client(moved, 1).Workspace);
        Assert.Contains(client => client.Window == new WindowId(1), moved.State.Hidden);
        Assert.AreEqual(WorkspaceId.Initial, moved.State.ActiveWorkspace);
    }

    private static MonitorSnapshot Screen(Outcome outcome, int id) =>
        Assert.ContainsSingle(screen => screen.Id == new MonitorId(id), outcome.State.Screens);

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);
}
