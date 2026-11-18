using System.Globalization;
using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class CommandTests
{
    [TestMethod]
    public void PipeDispatchParsesTheNameAndArguments()
    {
        var parsed = Assert.IsExactInstanceOfType<CommandParsed>(CommandProtocol.Parse("exec notepad --title hi"));
        var dispatch = Assert.IsExactInstanceOfType<DispatchRequest>(parsed.Request);

        Assert.AreEqual("exec", dispatch.Name);
        Assert.AreEqual("notepad --title hi", dispatch.Arguments);
    }

    [TestMethod]
    public void PipeDispatchKeepsInternalSpacing()
    {
        var parsed = Assert.IsExactInstanceOfType<CommandParsed>(CommandProtocol.Parse("  exec   foo  bar"));
        var dispatch = Assert.IsExactInstanceOfType<DispatchRequest>(parsed.Request);

        Assert.AreEqual("exec", dispatch.Name);
        Assert.AreEqual("foo  bar", dispatch.Arguments);
    }

    [TestMethod]
    public void ExecOverThePipeRunsTheCommand()
    {
        var core = new WindowManager();

        var reply = core.Execute("exec elevated notepad");

        Assert.AreEqual("ok", reply.Text);
        Assert.IsNull(reply.Effect.Close);
        Assert.IsEmpty(reply.Effect.Placements);
    }

    [TestMethod]
    public void BoundExecRuns()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, E, exec, notepad")));
        core.Handle(new KeyPressed("Super_L"));

        var outcome = core.Handle(new KeyPressed("E"));

        Assert.IsNull(outcome.Close);
    }

    [TestMethod]
    public void DispatchKillactiveAsksToCloseTheActiveClientAndLeavesItInPlace()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        var reply = core.Execute("killactive");

        Assert.AreEqual("ok", reply.Text);
        Assert.AreEqual(new WindowId(2), reply.Effect.Close);
        Assert.AreEqual(2, reply.Effect.State.Clients.Count);
        Assert.Contains(client => client.Window == new WindowId(2), reply.Effect.State.Clients);
    }

    [TestMethod]
    [DataRow("killactiv", "error: unknown command 'killactiv'. Did you mean 'killactive'?")]
    [DataRow("gte windows", "error: unknown command 'gte'. Did you mean 'get'?")]
    [DataRow("notadispatcher", "error: unknown command 'notadispatcher'")]
    public void AnUnknownNameSuggestsTheOneNameItIsCloseTo(string line, string error)
    {
        var reply = new WindowManager().Execute(line);

        Assert.AreEqual(error, reply.Text);
    }

    [TestMethod]
    public void TheDispatchWordIsNotACommand()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 100, 100)));

        var reply = core.Execute("dispatch killactive");

        Assert.AreEqual("error: unknown command 'dispatch'", reply.Text);
        Assert.IsNull(reply.Effect.Close);
    }

    [TestMethod]
    [DataRow("get")]
    [DataRow("get clients")]
    [DataRow("get monitors some")]
    public void GetNeedsAThingItKnows(string line)
    {
        var reply = new WindowManager().Execute(line);

        Assert.AreEqual("error: unknown request", reply.Text);
    }

    [TestMethod]
    public void AnActionTakesTheJsonFlagAndAnyCase()
    {
        var parsed = Assert.IsExactInstanceOfType<CommandParsed>(CommandProtocol.Parse("j/Workspace 2"));
        var dispatch = Assert.IsExactInstanceOfType<DispatchRequest>(parsed.Request);

        Assert.AreEqual(("Workspace", "2", CommandFlags.Json), (dispatch.Name, dispatch.Arguments, dispatch.Flags));
        Assert.IsExactInstanceOfType<WindowsRequest>(Assert.IsExactInstanceOfType<CommandParsed>(CommandProtocol.Parse("GET Windows")).Request);
    }

    [TestMethod]
    public void UnknownRequestIsPlainText()
    {
        var reply = new WindowManager().Execute("not-a-command");

        Assert.AreEqual("error: unknown command 'not-a-command'", reply.Text);
    }

    [TestMethod]
    [DataRow("get windows")]
    [DataRow("get workspaces")]
    [DataRow("get monitors")]
    [DataRow("get window")]
    [DataRow("get workspace")]
    [DataRow("events")]
    public void ReadCommandsRejectTrailingArguments(string command)
    {
        var reply = new WindowManager().Execute(command + " extra");

        Assert.AreEqual("error: unknown request", reply.Text);
    }

    [TestMethod]
    public void ClientsReplyIsEmptyUntilAWindowIsAdopted()
    {
        var core = new WindowManager();
        var clients = Json(core.Execute("j/get windows").Text);

        Assert.AreEqual(JsonValueKind.Array, clients.ValueKind);
        Assert.AreEqual(0, clients.GetArrayLength());
        Assert.AreEqual("no open windows", core.Execute("get windows").Text);
    }

    [TestMethod]
    public void ClientsReplyUsesTheAddressFloatingAndWorkspaceShape()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 800, 600);
        core.Handle(new WindowAppeared(new WindowId(42), area));
        core.Handle(new WindowAppeared(new WindowId(255), area)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = new Rectangle(4, 5, 6, 7),
        });

        var clients = Json(core.Execute("j/GET Windows").Text);

        Assert.AreEqual(2, clients.GetArrayLength());
        AssertClient(clients[0], "0x2a", floating: false, hidden: false, workspaceId: 1, workspaceName: "1");
        AssertClient(clients[1], "0xff", floating: true, hidden: false, workspaceId: 1, workspaceName: "1");
    }

    [TestMethod]
    public void ClientWorkspaceFollowsTheStoredWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2 silent, class:kitty\n"));
        core.Handle(new WindowAppeared(new WindowId(9), new Rectangle(0, 0, 100, 100))
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        var client = Assert.ContainsSingle(Json(core.Execute("j/get windows").Text).EnumerateArray());
        var workspace = client.GetProperty("workspace");

        Assert.AreEqual(2, workspace.GetProperty("id").GetInt32());
        Assert.AreEqual("2", workspace.GetProperty("name").GetString());
    }

    [TestMethod]
    public void WorkspacesReplyStartsWithTheDefaultWorkspace()
    {
        var workspace = Assert.ContainsSingle(Json(new WindowManager().Execute("j/get workspaces").Text).EnumerateArray());

        Assert.AreEqual(1, workspace.GetProperty("id").GetInt32());
        Assert.AreEqual("1", workspace.GetProperty("name").GetString());
        Assert.AreEqual(0, workspace.GetProperty("windows").GetInt32());
    }

    [TestMethod]
    public void WorkspacesReplyCountsClientsAndKeepsTheDefault()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2, class:kitty\n"));
        var area = new Rectangle(0, 0, 100, 100);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        var workspaces = Json(core.Execute("j/get workspaces").Text);
        var byName = workspaces.EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!);

        Assert.AreEqual(1, byName["1"].GetProperty("id").GetInt32());
        Assert.AreEqual(1, byName["1"].GetProperty("windows").GetInt32());
        Assert.AreEqual(2, byName["2"].GetProperty("id").GetInt32());
        Assert.AreEqual(1, byName["2"].GetProperty("windows").GetInt32());
    }

    [TestMethod]
    public void MonitorsReplyIsTheDefaultMonitorOnTheActiveWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 4, class:kitty\n"));
        core.Handle(new WindowAppeared(new WindowId(3), new Rectangle(0, 0, 100, 100))
        {
            Traits = WindowTraits.Normal with { Class = "kitty" },
        });

        var monitor = Assert.ContainsSingle(Json(core.Execute("j/get monitors").Text).EnumerateArray());
        var active = monitor.GetProperty("activeWorkspace");

        Assert.AreEqual(0, monitor.GetProperty("id").GetInt32());
        Assert.AreEqual("", monitor.GetProperty("name").GetString());
        Assert.IsTrue(monitor.GetProperty("focused").GetBoolean());
        Assert.AreEqual(4, active.GetProperty("id").GetInt32());
        Assert.AreEqual("4", active.GetProperty("name").GetString());
    }

    [TestMethod]
    public void ActiveWindowIsEmptyUntilAClientExists()
    {
        var core = new WindowManager();
        var active = Json(core.Execute("j/get window").Text);

        Assert.AreEqual(JsonValueKind.Null, active.ValueKind);
        Assert.AreEqual("", core.Execute("get window").Text);
    }

    [TestMethod]
    public void ActiveWindowFollowsTheLatestAdoptedClient()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 640, 480);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        var active = Json(core.Execute("j/get window").Text);

        Assert.AreEqual("0x2", active.GetProperty("address").GetString());

        core.Handle(new WindowClosed(new WindowId(2)));

        active = Json(core.Execute("j/get window").Text);
        Assert.AreEqual("0x1", active.GetProperty("address").GetString());
    }

    [TestMethod]
    public void DispatchWorkspaceOverThePipeSwitches()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 800, 600);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        CommandReply reply = default;
        var thread = new Thread(() => reply = core.Execute("workspace 2"));
        thread.Start();
        thread.Join();

        Assert.AreEqual("ok", reply.Text);
        Assert.AreEqual(new WorkspaceId("2"), reply.Effect.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1), new WindowId(2)], reply.Effect.Hide);
        Assert.IsEmpty(reply.Effect.Show);
    }

    [TestMethod]
    public void WorkspacesReplyAfterASwitchReportsEachDesk()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 800, 600);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        core.Execute("workspace 2");
        core.Handle(new WindowAppeared(new WindowId(3), area));

        var byName = Json(core.Execute("j/get workspaces").Text)
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("name").GetString()!);

        Assert.AreEqual(1, byName["1"].GetProperty("id").GetInt32());
        Assert.AreEqual(2, byName["1"].GetProperty("windows").GetInt32());
        Assert.AreEqual(2, byName["2"].GetProperty("id").GetInt32());
        Assert.AreEqual(1, byName["2"].GetProperty("windows").GetInt32());

        var clients = Json(core.Execute("j/get windows").Text).EnumerateArray().ToArray();
        AssertClient(clients[0], "0x1", floating: false, hidden: true, workspaceId: 1, workspaceName: "1");
        AssertClient(clients[1], "0x2", floating: false, hidden: true, workspaceId: 1, workspaceName: "1");
        AssertClient(clients[2], "0x3", floating: false, hidden: false, workspaceId: 2, workspaceName: "2");

        var monitor = Assert.ContainsSingle(Json(core.Execute("j/get monitors").Text).EnumerateArray());
        var activeWorkspace = monitor.GetProperty("activeWorkspace");
        Assert.AreEqual(2, activeWorkspace.GetProperty("id").GetInt32());
        Assert.AreEqual("2", activeWorkspace.GetProperty("name").GetString());

        var active = Json(core.Execute("j/get window").Text);
        Assert.AreEqual("0x3", active.GetProperty("address").GetString());
        Assert.IsFalse(active.GetProperty("hidden").GetBoolean());
        Assert.AreEqual(2, active.GetProperty("workspace").GetProperty("id").GetInt32());
    }

    [TestMethod]
    public void PipeRunsFocusFloatAndMoveOnTheActiveWorkspace()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 1920, 1080);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));

        var focus = core.Execute("movefocus l");

        Assert.AreEqual("ok", focus.Text);
        Assert.AreEqual(new WindowId(1), focus.Effect.Focus);

        var floating = core.Execute("togglefloating");

        Assert.IsTrue(Client(floating.Effect, 1).Floating);
        Assert.IsFalse(Client(floating.Effect, 2).Floating);

        var moved = core.Execute("movetoworkspace 3");

        Assert.AreEqual(new WorkspaceId("3"), moved.Effect.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("3"), Client(moved.Effect, 1).Workspace);
        Assert.AreEqual(new WorkspaceId("1"), Client(moved.Effect, 2).Workspace);
        Assert.Contains(client => client.Window == new WindowId(2), moved.Effect.State.Hidden);
    }

    [TestMethod]
    public void BoundWorkspaceSwitches()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = SUPER, 2, workspace, 2")));
        core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 100, 100)));
        core.Handle(new KeyPressed("Super_L"));

        var outcome = core.Handle(new KeyPressed("2"));

        Assert.IsTrue(outcome.Consumed);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreSequenceEqual([new WindowId(1)], outcome.Hide);
    }

    [TestMethod]
    public void ConcurrentPipeRequestsLeaveTheDesksConsistent()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600)));
        var failures = 0;
        var threads = new Thread[4];
        for (var index = 0; index < threads.Length; index++)
        {
            var step = index;
            threads[index] = new Thread(() =>
            {
                for (var n = 0; n < 30; n++)
                {
                    try
                    {
                        var workspace = ((step + n) % 3) + 1;
                        if (n % 2 == 0)
                            core.Execute("workspace " + workspace.ToString(CultureInfo.InvariantCulture));
                        else
                            core.Handle(new Dispatch("workspace", workspace.ToString(CultureInfo.InvariantCulture)));

                        _ = core.Execute("j/get workspaces");
                        _ = core.Execute("j/get windows");
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        Interlocked.Increment(ref failures);
                    }
                }
            });
        }

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        Assert.AreEqual(0, failures);
        var active = Assert.ContainsSingle(Json(core.Execute("j/get monitors").Text).EnumerateArray())
            .GetProperty("activeWorkspace")
            .GetProperty("id")
            .GetInt32();
        Assert.IsInRange(1, 3, active);
        Assert.Contains(
            workspace => workspace.GetProperty("id").GetInt32() == active,
            Json(core.Execute("j/get workspaces").Text).EnumerateArray());
    }

    private static void AssertClient(
        JsonElement client,
        string address,
        bool floating,
        bool hidden,
        int workspaceId,
        string workspaceName)
    {
        Assert.AreEqual(address, client.GetProperty("address").GetString());
        Assert.AreEqual(floating, client.GetProperty("floating").GetBoolean());
        Assert.AreEqual(hidden, client.GetProperty("hidden").GetBoolean());
        Assert.AreEqual(workspaceId, client.GetProperty("workspace").GetProperty("id").GetInt32());
        Assert.AreEqual(workspaceName, client.GetProperty("workspace").GetProperty("name").GetString());
    }
}
