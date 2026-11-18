namespace Winland.Application.Tests;

[TestClass]
public class MonitorWorkspaceTests
{
    [TestMethod]
    public void EachEnabledMonitorShowsItsOwnWorkspace()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1.5);
        core.Handle(new MonitorsChanged([left, right]));

        var first = core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        var second = core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        Assert.AreEqual(left.WorkArea, BoundsOf(first, 1));
        Assert.AreEqual(right.WorkArea, BoundsOf(second, 2));
        Assert.AreEqual(new WorkspaceId("1"), Client(second, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("1", 1), Client(second, 2).Workspace);
        Assert.AreEqual(new MonitorId(0), Workspace(second, "1").Monitor);
        Assert.AreEqual(new MonitorId(1), On(second, 1, "1").Monitor);
        Assert.AreEqual(new MonitorId(0), second.State.ActiveMonitor);
        Assert.AreEqual(new WorkspaceId("1"), second.State.ActiveWorkspace);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), second.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), second.State.Hidden);

        var monitors = Json(core.Execute("j/monitors").Text).EnumerateArray().ToArray();
        Assert.AreEqual(2, monitors.Length);
        Assert.AreEqual(0, monitors[0].GetProperty("id").GetInt32());
        Assert.AreEqual(@"\\.\DISPLAY1", monitors[0].GetProperty("name").GetString());
        Assert.AreEqual("Dell", monitors[0].GetProperty("description").GetString());
        Assert.AreEqual(0, monitors[0].GetProperty("x").GetInt32());
        Assert.AreEqual(0, monitors[0].GetProperty("y").GetInt32());
        Assert.AreEqual(1920, monitors[0].GetProperty("width").GetInt32());
        Assert.AreEqual(1080, monitors[0].GetProperty("height").GetInt32());
        Assert.AreEqual(1, monitors[0].GetProperty("scale").GetDouble());
        Assert.IsTrue(monitors[0].GetProperty("focused").GetBoolean());
        Assert.AreEqual(1, monitors[0].GetProperty("activeWorkspace").GetProperty("id").GetInt32());
        Assert.AreEqual("1", monitors[0].GetProperty("activeWorkspace").GetProperty("name").GetString());

        Assert.AreEqual(1, monitors[1].GetProperty("id").GetInt32());
        Assert.AreEqual(@"\\.\DISPLAY2", monitors[1].GetProperty("name").GetString());
        Assert.AreEqual("Samsung", monitors[1].GetProperty("description").GetString());
        Assert.AreEqual(1920, monitors[1].GetProperty("x").GetInt32());
        Assert.AreEqual(0, monitors[1].GetProperty("y").GetInt32());
        Assert.AreEqual(1280, monitors[1].GetProperty("width").GetInt32());
        Assert.AreEqual(1024, monitors[1].GetProperty("height").GetInt32());
        Assert.AreEqual(1.5, monitors[1].GetProperty("scale").GetDouble());
        Assert.IsFalse(monitors[1].GetProperty("focused").GetBoolean());
        Assert.AreEqual(1, monitors[1].GetProperty("activeWorkspace").GetProperty("id").GetInt32());
        Assert.AreEqual("1", monitors[1].GetProperty("activeWorkspace").GetProperty("name").GetString());
        Assert.AreEqual(@"\\.\DISPLAY2", monitors[1].GetProperty("activeWorkspace").GetProperty("monitor").GetString());

        var workspaces = Json(core.Execute("j/workspaces").Text).EnumerateArray().ToArray();
        var onLeft = Assert.ContainsSingle(item => item.GetProperty("monitor").GetString() == @"\\.\DISPLAY1", workspaces);
        var onRight = Assert.ContainsSingle(item => item.GetProperty("monitor").GetString() == @"\\.\DISPLAY2", workspaces);
        Assert.AreEqual(0, onLeft.GetProperty("monitorID").GetInt32());
        Assert.AreEqual(1, onLeft.GetProperty("windows").GetInt32());
        Assert.AreEqual("1", onLeft.GetProperty("name").GetString());
        Assert.AreEqual(1, onRight.GetProperty("monitorID").GetInt32());
        Assert.AreEqual(1, onRight.GetProperty("windows").GetInt32());
        Assert.AreEqual("1", onRight.GetProperty("name").GetString());

        var clients = Json(core.Execute("j/clients").Text).EnumerateArray().ToArray();
        Assert.IsFalse(clients[0].GetProperty("hidden").GetBoolean());
        Assert.IsFalse(clients[1].GetProperty("hidden").GetBoolean());
        Assert.AreEqual(1, clients[0].GetProperty("workspace").GetProperty("id").GetInt32());
        Assert.AreEqual(@"\\.\DISPLAY1", clients[0].GetProperty("workspace").GetProperty("monitor").GetString());
        Assert.AreEqual(1, clients[1].GetProperty("workspace").GetProperty("id").GetInt32());
        Assert.AreEqual(@"\\.\DISPLAY2", clients[1].GetProperty("workspace").GetProperty("monitor").GetString());
    }

    [TestMethod]
    public void UnpluggingMovesEachWorkspaceIntactOntoARemainingMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new MonitorsChanged([left]));

        Assert.ContainsSingle(outcome.State.Monitors);
        Assert.AreEqual(new MonitorId(0), outcome.State.ActiveMonitor);
        Assert.AreEqual(new WorkspaceId("1"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), Workspace(outcome, "1").Monitor);
        Assert.AreEqual(new MonitorId(0), Workspace(outcome, "2").Monitor);
        Assert.AreEqual(left.WorkArea, BoundsOf(outcome, 1));
        Assert.AreEqual(new WorkspaceId("1"), Client(outcome, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("2"), Client(outcome, 2).Workspace);
        Assert.Contains(new WindowId(2), outcome.Hide);
        Assert.Contains(client => client.Window == new WindowId(2), outcome.State.Hidden);

        var workspaces = Json(core.Execute("j/workspaces").Text)
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("name").GetString()!);
        Assert.AreEqual(@"\\.\DISPLAY1", workspaces["1"].GetProperty("monitor").GetString());
        Assert.AreEqual(@"\\.\DISPLAY1", workspaces["2"].GetProperty("monitor").GetString());
        Assert.AreEqual(0, workspaces["2"].GetProperty("monitorID").GetInt32());
        Assert.AreEqual(1, workspaces["1"].GetProperty("windows").GetInt32());
        Assert.AreEqual(1, workspaces["2"].GetProperty("windows").GetInt32());

        var monitor = Assert.ContainsSingle(Json(core.Execute("j/monitors").Text).EnumerateArray());
        Assert.AreEqual(@"\\.\DISPLAY1", monitor.GetProperty("name").GetString());
        Assert.IsTrue(monitor.GetProperty("focused").GetBoolean());
        Assert.AreEqual("1", monitor.GetProperty("activeWorkspace").GetProperty("name").GetString());
    }

    [TestMethod]
    public void ReplugMovesTheWorkspacesBackAndShowsTheOneTheMonitorShowed()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new MonitorsChanged([left]));

        var back = core.Handle(new MonitorsChanged([left, right]));

        var screen = Assert.ContainsSingle(candidate => candidate.Monitor.Name == right.Name, back.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(1, screen.Id), screen.ActiveWorkspace);
        Assert.AreEqual(screen.Id, On(back, screen.Id.Value, "1").Monitor);
        Assert.AreEqual(new WorkspaceId("1"), Assert.ContainsSingle(candidate => candidate.Monitor.Name == left.Name, back.State.Screens).ActiveWorkspace);
        Assert.Contains(new WindowId(2), back.Show);
        Assert.IsEmpty(back.State.Hidden);
        Assert.AreEqual(right.WorkArea, BoundsOf(back, 2));
        Assert.AreEqual(left.WorkArea, BoundsOf(back, 1));
    }

    [TestMethod]
    public void UnpluggingKeepsAGroupAndAPinnedClientWithTheirWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:box\n"));
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("togglegroup"));
        core.Handle(new WindowAppeared(new WindowId(3), right.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(4), right.WorkArea) { Traits = WindowTraits.Normal with { Class = "box" } });
        core.Handle(new Dispatch("pin"));

        var gone = core.Handle(new MonitorsChanged([left]));

        Assert.AreSequenceEqual([new WindowId(2), new WindowId(3)], Assert.ContainsSingle(gone.State.Groups));
        Assert.AreEqual(new WorkspaceId("2"), Client(gone, 2).Workspace);
        Assert.AreEqual(new WorkspaceId("2"), Client(gone, 3).Workspace);
        Assert.Contains(client => client.Window == new WindowId(2), gone.State.Hidden);
        Assert.Contains(client => client.Window == new WindowId(3), gone.State.Hidden);
        Assert.IsTrue(Client(gone, 4).Pinned);
        Assert.IsInRange(left.X, left.X + left.Width - Client(gone, 4).Bounds.Width, Client(gone, 4).Bounds.X);
        Assert.IsInRange(left.Y, left.Y + left.Height - Client(gone, 4).Bounds.Height, Client(gone, 4).Bounds.Y);
        Assert.DoesNotContain(client => client.Window == new WindowId(4), gone.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(1), gone.State.Hidden);

        var back = core.Handle(new MonitorsChanged([left, right]));

        Assert.AreSequenceEqual([new WindowId(2), new WindowId(3)], Assert.ContainsSingle(back.State.Groups));
        Assert.Contains(new WindowId(3), back.Show);
        Assert.ContainsSingle(client => client.Window == new WindowId(2), back.State.Hidden);
        var returned = Assert.ContainsSingle(candidate => candidate.Monitor.Name == right.Name, back.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(1, returned.Id), Client(back, 2).Workspace);
        Assert.IsInRange(right.X, right.X + right.Width - Client(back, 4).Bounds.Width, Client(back, 4).Bounds.X);
    }

    [TestMethod]
    public void UnpluggingTheFocusedMonitorFocusesTheWindowShownOnTheMonitorThatRemains()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var gone = core.Handle(new MonitorsChanged([left]));

        Assert.Contains(client => client.Window == new WindowId(2), gone.State.Hidden);
        Assert.AreEqual(new WindowId(1), gone.Focus);
        Assert.AreEqual("0x1", Json(core.Execute("j/activewindow").Text).GetProperty("address").GetString());
        Assert.DoesNotContain(
            line => line.StartsWith("workspace>>", StringComparison.Ordinal),
            Lines(gone));
    }

    [TestMethod]
    public void ReplugFocusesTheShownWindowWhenTheFocusedOneIsHidden()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new Dispatch("workspace", "5"));
        core.Handle(new WindowAppeared(new WindowId(5), right.WorkArea));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var gone = core.Handle(new MonitorsChanged([left]));
        core.Handle(new Dispatch("workspace", Client(gone, 5).Workspace.Name));

        var back = core.Handle(new MonitorsChanged([left, right]));

        var returned = Assert.ContainsSingle(candidate => candidate.Monitor.Name == right.Name, back.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(5, returned.Id), Client(back, 5).Workspace);
        Assert.Contains(client => client.Window == new WindowId(5), back.State.Hidden);
        Assert.AreEqual(new WindowId(1), back.Focus);
        Assert.AreEqual("0x1", Json(core.Execute("j/activewindow").Text).GetProperty("address").GetString());
    }

    [TestMethod]
    public void AShownSpecialWorkspaceFromAnUnpluggedMonitorReplacesTheOneTheRemainingMonitorShows()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new Dispatch("togglespecialworkspace", "notes"));
        core.Handle(new WindowAppeared(new WindowId(4), left.WorkArea));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new Dispatch("togglespecialworkspace", "scratch"));
        core.Handle(new WindowAppeared(new WindowId(5), right.WorkArea));

        var gone = core.Handle(new MonitorsChanged([left]));

        var only = Assert.ContainsSingle(gone.State.Screens);
        Assert.AreEqual(new WorkspaceId("special:scratch"), only.SpecialWorkspace);
        Assert.DoesNotContain(client => client.Window == new WindowId(5), gone.State.Hidden);
        Assert.Contains(client => client.Window == new WindowId(4), gone.State.Hidden);
    }

    [TestMethod]
    public void FocusingAMonitorWithASpecialWorkspaceOpenFocusesItsWindow()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new WindowAppeared(new WindowId(1), right.WorkArea));
        core.Handle(new Dispatch("togglespecialworkspace", "scratch"));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("focusmonitor", left.Name));

        var back = core.Handle(new Dispatch("focusmonitor", right.Name));

        Assert.AreEqual(new WindowId(2), back.Focus);
    }

    [TestMethod]
    public void UnpluggingCarriesAShownSpecialWorkspaceAndAFullscreenClient()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new Dispatch("focusmonitor", right.Name));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new Dispatch("fullscreen"));
        core.Handle(new Dispatch("togglespecialworkspace", "scratch"));
        core.Handle(new WindowAppeared(new WindowId(5), right.WorkArea));

        var gone = core.Handle(new MonitorsChanged([left]));

        var only = Assert.ContainsSingle(gone.State.Screens);
        Assert.AreEqual(new WorkspaceId("special:scratch"), only.SpecialWorkspace);
        Assert.DoesNotContain(client => client.Window == new WindowId(5), gone.State.Hidden);
        Assert.AreEqual(FullscreenMode.Fullscreen, Client(gone, 2).Fullscreen);
        Assert.Contains(client => client.Window == new WindowId(2), gone.State.Hidden);

        var back = core.Handle(new MonitorsChanged([left, right]));

        var stayed = Assert.ContainsSingle(candidate => candidate.Monitor.Name == left.Name, back.State.Screens);
        var returned = Assert.ContainsSingle(candidate => candidate.Monitor.Name == right.Name, back.State.Screens);
        Assert.AreEqual(new WorkspaceId("special:scratch"), stayed.SpecialWorkspace);
        Assert.IsNull(returned.SpecialWorkspace);
        Assert.AreEqual(WorkspaceId.FromNumber(1, returned.Id), Client(back, 2).Workspace);
        Assert.AreEqual(FullscreenMode.Fullscreen, Client(back, 2).Fullscreen);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), back.State.Hidden);
        Assert.AreEqual(new Rectangle(1920, 0, 1280, 1024), Client(back, 2).Bounds);
    }

    [TestMethod]
    public void SwitchingWorkspaceGivesTheKeyboardToTheWindowItFocuses()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(3), right.WorkArea));
        core.Handle(new WindowFocused(new WindowId(3), FocusCause.User));
        core.Handle(new Dispatch("movetoworkspacesilent", "2"));
        core.Handle(new WindowFocused(new WindowId(2), FocusCause.User));

        var outcome = core.Handle(new Dispatch("workspace", "2"));

        Assert.AreEqual(outcome.State.ActiveWorkspace, Client(outcome, 3).Workspace);
        Assert.AreEqual(new WindowId(3), outcome.Focus);
    }

    [TestMethod]
    public void WorkspaceNumberNamesTheFocusedMonitorsWorkspace()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new Dispatch("workspace", "2"));

        Assert.IsNull(outcome.Focus);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), outcome.State.ActiveMonitor);
        Assert.AreEqual(new MonitorId(0), Workspace(outcome, "2").Monitor);
        Assert.AreEqual(new WorkspaceId("1"), Client(outcome, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("1", 1), Client(outcome, 2).Workspace);
        var rightScreen = Assert.ContainsSingle(screen => screen.Monitor.Name == right.Name, outcome.State.Screens);
        Assert.AreEqual(WorkspaceId.FromNumber(1, rightScreen.Id), rightScreen.ActiveWorkspace);
        Assert.Contains(window => window == new WindowId(1), outcome.Hide);
        Assert.DoesNotContain(window => window == new WindowId(2), outcome.Hide);
        Assert.AreSequenceEqual(
            [
                @"createworkspace>>2,2,\\.\DISPLAY1",
                @"workspace>>2,2,\\.\DISPLAY1",
                @"focus>>\\.\DISPLAY1,2,2,,",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void MoveToWorkspaceUsesTheFocusedMonitorsNumber()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new Dispatch("movetoworkspace", "2"));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), outcome.State.ActiveMonitor);
        Assert.AreEqual(new WorkspaceId("2"), Client(outcome, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("1", 1), Client(outcome, 2).Workspace);
        Assert.AreEqual(left.WorkArea, BoundsOf(outcome, 1));
        Assert.AreSequenceEqual(
            [
                @"createworkspace>>2,2,\\.\DISPLAY1",
                @"destroyworkspace>>1,1,\\.\DISPLAY1",
                @"movewindow>>1,2,2,\\.\DISPLAY1",
                @"workspace>>2,2,\\.\DISPLAY1",
                @"focus>>\\.\DISPLAY1,2,2,1,",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void MoveToWorkspaceSilentStaysOnTheFocusedMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new Dispatch("movetoworkspacesilent", "2"));

        Assert.IsNull(outcome.Focus);
        Assert.AreEqual(new WorkspaceId("1"), outcome.State.ActiveWorkspace);
        Assert.AreEqual(new MonitorId(0), outcome.State.ActiveMonitor);
        Assert.AreEqual(new WorkspaceId("1"), Workspace(outcome, "1").Id);
        Assert.AreEqual(new WorkspaceId("2"), Client(outcome, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("1", 1), Client(outcome, 2).Workspace);
        Assert.Contains(client => client.Window == new WindowId(1), outcome.State.Hidden);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), outcome.State.Hidden);
        Assert.AreEqual(new WorkspaceId("1", 1), Assert.ContainsSingle(screen => screen.Monitor.Name == right.Name, outcome.State.Screens).ActiveWorkspace);
        Assert.AreSequenceEqual(
            [
                @"createworkspace>>2,2,\\.\DISPLAY1",
                @"movewindow>>1,2,2,\\.\DISPLAY1",
                @"focus>>\\.\DISPLAY1,1,1,,",
            ],
            Lines(outcome));
    }

    private static Workspace Workspace(Outcome outcome, string name) =>
        Assert.ContainsSingle(workspace => workspace.Id == new WorkspaceId(name), outcome.State.Workspaces);

    private static Workspace On(Outcome outcome, int monitor, string name) =>
        Assert.ContainsSingle(workspace => workspace.Id == new WorkspaceId(name, monitor), outcome.State.Workspaces);

    private static Decoration Border(Outcome outcome, long window) =>
        Assert.ContainsSingle(decoration => decoration.Client == new ClientId(window), outcome.Decorations);

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);
}
