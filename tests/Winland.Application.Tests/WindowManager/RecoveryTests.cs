using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class RecoveryTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CloakedClientsReturnSilentlyToTheirWorkspaceAndRectangles(bool floating)
    {
        var core = new WindowManager();
        WindowAppeared Window(long id) => new(new WindowId(id), Area)
        {
            Opened = true,
            Bounds = new Rectangle(120, 130, 400, 300),
            Traits = WindowTraits.Normal with { Dialog = floating },
        };
        core.Handle(Window(1));
        core.Handle(Window(2));
        var before = core.Recovery().Clients.ToDictionary(client => client.Window, client => client.Bounds);
        core.Handle(new WindowClosed(new WindowId(1)) { Cloaked = true });
        core.Handle(new WindowClosed(new WindowId(2)) { Cloaked = true });
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(3), Area));

        core.Handle(Window(2));
        var returned = core.Handle(Window(1));

        Assert.AreEqual(new WorkspaceId("2"), returned.State.ActiveWorkspace);
        foreach (var client in core.Recovery().Clients.Where(client => client.Window.Value != 3))
        {
            Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
            Assert.AreEqual(before[client.Window], client.Bounds);
        }
        Assert.AreEqual(new WindowId(3), core.Handle(new Dispatch("killactive")).Close);
    }

    [TestMethod]
    public void RecoveryRecordsEachClientsWorkspaceMonitorAndWhetherItIsHidden()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "Notepad", Title = "Untitled" },
        });
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), Area)
        {
            Traits = WindowTraits.Normal with { Class = "CabinetWClass", Title = "Home" },
        });

        var snapshot = core.Recovery();

        var hidden = Assert.ContainsSingle(client => client.Window == new WindowId(1), snapshot.Clients);
        Assert.AreEqual(new WorkspaceId("1"), hidden.Workspace);
        Assert.AreEqual(MonitorId.Primary, hidden.Monitor);
        Assert.IsTrue(hidden.Hidden);
        Assert.AreEqual("Notepad", hidden.Class);
        Assert.AreEqual("Untitled", hidden.Title);
        Assert.AreEqual(0, hidden.ProcessId);

        var shown = Assert.ContainsSingle(client => client.Window == new WindowId(2), snapshot.Clients);
        Assert.AreEqual(new WorkspaceId("2"), shown.Workspace);
        Assert.AreEqual(MonitorId.Primary, shown.Monitor);
        Assert.IsFalse(shown.Hidden);
        Assert.AreEqual("CabinetWClass", shown.Class);
        Assert.AreEqual("Home", shown.Title);
    }

    [TestMethod]
    public void RecoveryKeepsTheMonitorThatOwnsTheWorkspace()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", 0, 1920, 1080);
        var right = Display(@"\\.\DISPLAY2", 1920, 1280, 1024);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var snapshot = core.Recovery();

        var first = Assert.ContainsSingle(client => client.Window == new WindowId(1), snapshot.Clients);
        var second = Assert.ContainsSingle(client => client.Window == new WindowId(2), snapshot.Clients);
        Assert.AreEqual(new WorkspaceId("1"), first.Workspace);
        Assert.AreEqual(new MonitorId(0), first.Monitor);
        Assert.IsFalse(first.Hidden);
        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), second.Workspace);
        Assert.AreEqual(new MonitorId(1), second.Monitor);
        Assert.IsFalse(second.Hidden);
    }

    [TestMethod]
    public void MinimizedClientOnAnInactiveWorkspaceIsRecordedAsHidden()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowMinimized(new WindowId(1)));
        core.Handle(new Dispatch("workspace", "2"));

        var client = Assert.ContainsSingle(core.Recovery().Clients);
        Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
        Assert.IsTrue(client.Hidden);
    }

    [TestMethod]
    public void SnapshotRoundTripsThroughJson()
    {
        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(42), new WorkspaceId("notes"), MonitorId.Primary, true, "Notepad", "Untitled", 4242)
            {
                MonitorIdentity = "Dell P2415Q A",
            },
            new RecoveredClient(new WindowId(7), WorkspaceId.Initial, MonitorId.Primary, false, "CabinetWClass", "Home", 9)
            {
                MonitorIdentity = @"\\.\DISPLAY1",
            },
        ]);

        var json = snapshot.ToJson();
        using var document = JsonDocument.Parse(json);
        var clients = document.RootElement.GetProperty("clients").EnumerateArray().ToArray();
        Assert.AreEqual(2, clients.Length);
        Assert.AreEqual(42, clients[0].GetProperty("window").GetInt64());
        Assert.AreEqual("notes", clients[0].GetProperty("workspace").GetString());
        Assert.AreEqual("Dell P2415Q A", clients[0].GetProperty("monitor").GetString());
        Assert.IsTrue(clients[0].GetProperty("hidden").GetBoolean());
        Assert.AreEqual("Notepad", clients[0].GetProperty("class").GetString());
        Assert.AreEqual("Untitled", clients[0].GetProperty("title").GetString());
        Assert.AreEqual(4242, clients[0].GetProperty("process").GetInt64());

        Assert.IsTrue(RecoverySnapshot.TryParse(json, out var parsed));
        Assert.AreEqual(snapshot.Clients.Count, parsed.Clients.Count);
        Assert.AreEqual(snapshot.Clients[0], parsed.Clients[0]);
        Assert.AreEqual(snapshot.Clients[1], parsed.Clients[1]);
    }

    [TestMethod]
    public void ASnapshotACleanExitLeftSaysSo()
    {
        var client = new RecoveredClient(new WindowId(7), WorkspaceId.Initial, MonitorId.Primary, true, "App", "App", 1);

        Assert.IsTrue(RecoverySnapshot.TryParse(new RecoverySnapshot([client]) { Clean = true }.ToJson(), out var clean));
        Assert.IsTrue(RecoverySnapshot.TryParse(new RecoverySnapshot([client]).ToJson(), out var crashed));

        Assert.IsTrue(clean.Clean);
        Assert.IsFalse(crashed.Clean);
    }

    [TestMethod]
    public void TheFrameAndRegionAWindowWasLeftWithRoundTrip()
    {
        var changed = new RecoveredClient(new WindowId(42), WorkspaceId.Initial, MonitorId.Primary, false, "App", "App", 1)
        {
            FrameStyle = 0x14CF0000,
            Region = true,
        };
        var plain = new RecoveredClient(new WindowId(7), WorkspaceId.Initial, MonitorId.Primary, false, "App", "App", 1);

        var json = new RecoverySnapshot([changed, plain]).ToJson();

        Assert.IsTrue(RecoverySnapshot.TryParse(json, out var parsed));
        Assert.AreEqual(changed, parsed.Clients[0]);
        Assert.AreEqual(plain, parsed.Clients[1]);
        Assert.DoesNotContain("frameStyle\":null", json, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("{\"clients\":null}")]
    public void UnreadableSnapshotIsRejected(string json)
    {
        Assert.IsFalse(RecoverySnapshot.TryParse(json, out _));
    }

    [TestMethod]
    public void RestoreRejoinsTheSameWindowAndIgnoresOnesThatNeverAppear()
    {
        var prior = new WindowManager();
        prior.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "Notepad", Title = "Untitled" },
        });
        prior.Handle(new WindowAppeared(new WindowId(2), Area));
        prior.Handle(new Dispatch("workspace", "2"));
        var snapshot = prior.Recovery();

        var core = new WindowManager();
        core.Restore(snapshot);
        var rejoined = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "Notepad", Title = "Untitled" },
        });

        var client = Assert.ContainsSingle(rejoined.State.Clients);
        Assert.AreEqual(new WindowId(1), client.Window);
        Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
        Assert.AreEqual(MonitorId.Primary, Assert.ContainsSingle(workspace => workspace.Id == new WorkspaceId("1"), rejoined.State.Workspaces).Monitor);
        Assert.DoesNotContain(hidden => hidden.Window == new WindowId(1), rejoined.State.Hidden);
        Assert.DoesNotContain(candidate => candidate.Window == new WindowId(2), rejoined.State.Clients);
        Assert.IsEmpty(rejoined.Hide);
    }

    [TestMethod]
    public void RestorePutsAClientBackOnAnInactiveWorkspaceWithoutSwitching()
    {
        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(5), new WorkspaceId("4"), MonitorId.Primary, true, "Notepad", "Notes", 10),
        ]);
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 9, class:Notepad\n"));
        core.Restore(snapshot);

        var outcome = core.Handle(new WindowAppeared(new WindowId(5), Area)
        {
            Traits = WindowTraits.Normal with { Class = "Notepad", Title = "Notes" },
        });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual(new WorkspaceId("4"), client.Workspace);
        Assert.AreEqual(WorkspaceId.Initial, outcome.State.ActiveWorkspace);
        Assert.Contains(hidden => hidden.Window == new WindowId(5), outcome.State.Hidden);
        Assert.AreSequenceEqual([new WindowId(5)], outcome.Hide);
    }

    [TestMethod]
    public void ARestartShowsTheWorkspaceThatWasShownWithItsTilesWhereTheyWere()
    {
        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(1), WorkspaceId.Initial, MonitorId.Primary, true, "App", "One", 10, Floating: false, Bounds: new Rectangle(0, 0, 1920, 1080)),
            new RecoveredClient(new WindowId(2), new WorkspaceId("4"), MonitorId.Primary, false, "App", "Two", 10, Floating: false, Bounds: new Rectangle(0, 0, 600, 1080)),
            new RecoveredClient(new WindowId(3), new WorkspaceId("4"), MonitorId.Primary, false, "App", "Three", 10, Floating: false, Bounds: new Rectangle(600, 0, 1320, 1080)),
        ]);
        var core = new WindowManager(ConfigParser.Parse("general:gaps_in = 0\ngeneral:gaps_out = 0\ngeneral:border_size = 0\n"));
        core.Restore(snapshot);
        foreach (var client in snapshot.Clients)
        {
            core.Handle(new WindowAppeared(client.Window, Area)
            {
                Traits = WindowTraits.Normal with { Class = client.Class, Title = client.Title },
                ProcessId = client.ProcessId,
            });
        }

        var arranged = core.Handle(new RestoredAdopted());

        Assert.AreEqual(new WorkspaceId("4"), arranged.State.ActiveWorkspace);
        Assert.AreEqual(new Rectangle(0, 0, 600, 1080), BoundsOf(arranged, 2));
        Assert.AreEqual(new Rectangle(600, 0, 1320, 1080), BoundsOf(arranged, 3));
        Assert.Contains(new WindowId(1), arranged.Hide);
    }

    [TestMethod]
    public void RestoreKeepsTheClientUntilALaterAppearanceCanAdoptIt()
    {
        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(5), new WorkspaceId("3"), new MonitorId(1), true, "Notepad", "Notes", 10),
        ]);
        var core = new WindowManager();
        var right = Display(@"\\.\DISPLAY2", 1920, 1280, 1024);
        core.Handle(new MonitorsChanged(
        [
            Display(@"\\.\DISPLAY1", 0, 1920, 1080),
            right,
        ]));
        core.Restore(snapshot);
        var blocked = core.Handle(new WindowAppeared(new WindowId(5), right.WorkArea)
        {
            Traits = WindowTraits.Normal with { Visible = false, Class = "Notepad", Title = "Notes" },
        });
        Assert.IsEmpty(blocked.State.Clients);

        var outcome = core.Handle(new WindowAppeared(new WindowId(5), right.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "Notepad", Title = "Notes" },
        });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        var workspace = WorkspaceId.FromNumber(3, new MonitorId(1));
        Assert.AreEqual(workspace, client.Workspace);
        Assert.AreEqual(new MonitorId(1), Assert.ContainsSingle(candidate => candidate.Id == workspace, outcome.State.Workspaces).Monitor);
    }

    [TestMethod]
    public void RestoreDropsARememberedWindowWhenItClosesBeforeAdoption()
    {
        var core = new WindowManager();
        core.Restore(new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(5), new WorkspaceId("4"), MonitorId.Primary, true, "Notepad", "Notes", 10),
        ]));
        core.Handle(new WindowClosed(new WindowId(5)));

        var outcome = core.Handle(new WindowAppeared(new WindowId(5), Area));

        Assert.AreEqual(WorkspaceId.Initial, Assert.ContainsSingle(outcome.State.Clients).Workspace);
    }

    [TestMethod]
    public void MissingMonitorFallsBackWhenTheWindowIsSeenAgain()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", 0, 1920, 1080);
        core.Handle(new MonitorsChanged([left]));
        core.Restore(new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(5), new WorkspaceId("4"), new MonitorId(99), true, "Notepad", "Notes", 10),
        ]));

        var outcome = core.Handle(new WindowAppeared(new WindowId(5), left.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "Notepad", Title = "Notes" },
        });

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual(new WorkspaceId("4"), client.Workspace);
        Assert.AreEqual(new MonitorId(0), Assert.ContainsSingle(workspace => workspace.Id == client.Workspace, outcome.State.Workspaces).Monitor);
    }

    [TestMethod]
    public void AFloatingPinnedClientComesBackFloatingPinnedAndWhereItWas()
    {
        var prior = new WindowManager(ConfigParser.Parse("windowrule = float, class:stick\n"));
        prior.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Bounds = new Rectangle(0, 0, 400, 300),
            Traits = WindowTraits.Normal with { Class = "stick" },
        });
        prior.Handle(new Dispatch("pin"));
        var placed = Assert.ContainsSingle(prior.Handle(new Dispatch("workspace", "2")).State.Clients).Bounds;
        Assert.IsTrue(RecoverySnapshot.TryParse(prior.Recovery().ToJson(), out var snapshot));

        var recorded = Assert.ContainsSingle(snapshot.Clients);
        Assert.IsTrue(recorded.Floating);
        Assert.IsTrue(recorded.Pinned);
        Assert.AreEqual(placed, recorded.Bounds);

        var core = new WindowManager();
        core.Restore(snapshot);
        var back = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Bounds = new Rectangle(5, 5, 50, 50),
            Traits = WindowTraits.Normal with { Class = "stick" },
        });

        var client = Assert.ContainsSingle(back.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.IsTrue(client.Pinned);
        Assert.AreEqual(placed, client.Bounds);
        Assert.AreEqual(new WorkspaceId("2"), client.Workspace);
    }

    [TestMethod]
    public void AFullscreenClientComesBackFullscreen()
    {
        var prior = new WindowManager();
        prior.Handle(new WindowAppeared(new WindowId(1), Area));
        prior.Handle(new WindowAppeared(new WindowId(2), Area));
        prior.Handle(new Dispatch("fullscreen", "1"));
        Assert.IsTrue(RecoverySnapshot.TryParse(prior.Recovery().ToJson(), out var snapshot));

        var core = new WindowManager();
        core.Restore(snapshot);
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        var back = core.Handle(new WindowAppeared(new WindowId(2), Area));

        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(2), back.State.Clients);
        Assert.AreEqual(FullscreenMode.Maximized, client.Fullscreen);
        Assert.IsFalse(client.Floating);
        Assert.AreEqual(FullscreenMode.None, Assert.ContainsSingle(candidate => candidate.Window == new WindowId(1), back.State.Clients).Fullscreen);
    }

    [TestMethod]
    public void ASnapshotWithoutTheNewerFieldsStillParsesAndLeavesThemToTheRules()
    {
        const string json = """{"clients":[{"window":5,"workspace":"1","monitor":"\\\\.\\DISPLAY1","hidden":false,"class":"stick","title":"","process":10}]}""";

        Assert.IsTrue(RecoverySnapshot.TryParse(json, out var snapshot));
        var recorded = Assert.ContainsSingle(snapshot.Clients);
        Assert.IsNull(recorded.Floating);
        Assert.IsNull(recorded.Bounds);
        Assert.IsNull(recorded.Pinned);
        Assert.IsNull(recorded.Fullscreen);

        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:stick\nwindowrule = pin, class:stick\n"));
        core.Restore(snapshot);
        var back = core.Handle(new WindowAppeared(new WindowId(5), Area)
        {
            Bounds = new Rectangle(10, 20, 300, 200),
            Traits = WindowTraits.Normal with { Class = "stick" },
        });

        var client = Assert.ContainsSingle(back.State.Clients);
        Assert.IsTrue(client.Floating);
        Assert.IsTrue(client.Pinned);
        Assert.AreEqual(new Rectangle(10, 20, 300, 200), client.Bounds);
    }

    private static Rectangle BoundsOf(Outcome outcome, long window) =>
        Assert.ContainsSingle(placement => placement.Client == new ClientId(window), outcome.Placements).Bounds;
}
