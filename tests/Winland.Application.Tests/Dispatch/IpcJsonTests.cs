using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class IpcJsonTests
{
    [TestMethod]
    public void ClientsAndActiveWindowMatchTheStatusBarShape()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(42), new Rectangle(0, 0, 800, 600))
        {
            Traits = WindowTraits.Normal with { Class = "kitty", Title = "term" },
            ProcessId = 4242,
        });

        const string Client =
            """
            {"address":"0x2a","hidden":false,"visible":true,"at":[0,0],"size":[800,600],"workspace":{"id":1,"type":"normal","name":"1","monitor":""},"floating":false,"monitor":"","class":"kitty","title":"term","initialClass":"kitty","initialTitle":"term","pid":4242,"pinned":false,"fullscreen":0,"fullscreenHandler":"default","allowedOverFullscreen":true,"grouped":[]}
            """;

        Assert.AreEqual("[" + Client + "]", core.Execute("j/get windows").Text);
        Assert.AreEqual(Client, core.Execute("j/get window").Text);
        Assert.AreEqual(4242, core.Recovery().Clients[0].ProcessId);
    }

    [TestMethod]
    public void FloatingClientReportsItsOwnRectangleAndAHiddenClientIsNotVisible()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600)));
        core.Handle(new WindowAppeared(new WindowId(7), new Rectangle(0, 0, 800, 600))
        {
            Traits = WindowTraits.Normal with { Dialog = true, Class = "dialog", Title = "Save" },
            Bounds = new Rectangle(10, 20, 300, 200),
            ProcessId = 99,
        });
        core.Execute("workspace 2");

        var clients = Json(core.Execute("j/get windows").Text).EnumerateArray().ToArray();

        Assert.AreEqual("0x1", clients[0].GetProperty("address").GetString());
        Assert.IsTrue(clients[0].GetProperty("hidden").GetBoolean());
        Assert.IsFalse(clients[0].GetProperty("visible").GetBoolean());
        Assert.AreEqual("", clients[0].GetProperty("monitor").GetString());

        Assert.AreEqual("0x7", clients[1].GetProperty("address").GetString());
        Assert.IsTrue(clients[1].GetProperty("floating").GetBoolean());
        Assert.IsTrue(clients[1].GetProperty("hidden").GetBoolean());
        Assert.AreEqual(250, clients[1].GetProperty("at")[0].GetInt32());
        Assert.AreEqual(200, clients[1].GetProperty("at")[1].GetInt32());
        Assert.AreEqual(300, clients[1].GetProperty("size")[0].GetInt32());
        Assert.AreEqual(200, clients[1].GetProperty("size")[1].GetInt32());
        Assert.AreEqual(99, clients[1].GetProperty("pid").GetInt64());
        Assert.AreEqual("dialog", clients[1].GetProperty("initialClass").GetString());
        Assert.AreEqual("Save", clients[1].GetProperty("initialTitle").GetString());
        Assert.AreEqual(JsonValueKind.Array, clients[1].GetProperty("grouped").ValueKind);
        Assert.AreEqual(0, clients[1].GetProperty("grouped").GetArrayLength());
        Assert.AreEqual(JsonValueKind.Number, clients[1].GetProperty("fullscreen").ValueKind);
        Assert.AreEqual(0, clients[1].GetProperty("fullscreen").GetInt32());
        Assert.IsFalse(clients[1].TryGetProperty("fullscreenClient", out _));
    }

    [TestMethod]
    public void WorkspacesAndActiveWorkspaceMatchTheStatusBarShape()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(42), new Rectangle(0, 0, 800, 600))
        {
            Traits = WindowTraits.Normal with { Class = "kitty", Title = "term" },
        });

        const string Workspace =
            """
            {"id":1,"type":"normal","name":"1","monitor":"","monitorId":0,"windows":1,"hasFullscreen":false,"lastWindow":"0x2a","lastWindowTitle":"term","tiledLayout":"dwindle"}
            """;

        Assert.AreEqual("[" + Workspace + "]", core.Execute("j/get workspaces").Text);
        Assert.AreEqual(Workspace, core.Execute("j/get workspace").Text);
    }

    [TestMethod]
    [DataRow("scrolling")]
    [DataRow("dwindle")]
    public void WorkspacesReportTheirTiledLayout(string layout)
    {
        var core = new WindowManager(ConfigParser.Parse("general:layout = " + layout));
        core.Handle(new WindowAppeared(new WindowId(42), new Rectangle(0, 0, 800, 600)));

        var workspace = Assert.ContainsSingle(Json(core.Execute("j/get workspaces").Text).EnumerateArray());

        Assert.AreEqual(layout, workspace.GetProperty("tiledLayout").GetString());
        Assert.AreEqual(layout, Json(core.Execute("j/get workspace").Text).GetProperty("tiledLayout").GetString());
    }

    [TestMethod]
    public void ActiveWorkspaceRejectsTrailingArguments()
    {
        Assert.AreEqual("error: unknown request", new WindowManager().Execute("j/get workspace extra").Text);
    }

    [TestMethod]
    public void MonitorsMatchTheStatusBarShape()
    {
        var core = new WindowManager();
        var display = new Monitor(
            "Dell",
            "Dell U2720Q",
            0,
            0,
            1920,
            1080,
            1,
            true,
            new Rectangle(0, 0, 1920, 1040),
            Make: "DEL",
            Model: "U2720Q",
            Serial: "ABC123",
            RefreshRate: 60);
        core.Handle(new MonitorsChanged([display]));

        const string Monitor =
            """
            {"id":0,"name":"Dell","description":"DEL U2720Q ABC123","make":"DEL","model":"U2720Q","serial":"ABC123","width":1920,"height":1080,"refreshRate":60,"x":0,"y":0,"activeWorkspace":{"id":1,"type":"normal","name":"1","monitor":"Dell"},"specialWorkspace":{"type":"","name":"","monitor":""},"reserved":[0,0,0,40],"scale":1,"focused":true,"disabled":false,"currentFormat":"","hardwareDetails":{"hdr":false,"chroma":false,"bt2020":false}}
            """;

        Assert.AreEqual("[" + Monitor + "]", core.Execute("j/get monitors").Text);
        Assert.AreEqual("[" + Monitor + "]", core.Execute("j/get monitors all").Text);
    }

    [TestMethod]
    public void ClientMonitorIsTheNameOfTheMonitorItIsOn()
    {
        var core = new WindowManager();
        var left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1080), RefreshRate: 60);
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1.5, true, new Rectangle(1920, 0, 1280, 1024), RefreshRate: 144);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var monitors = Json(core.Execute("j/get monitors").Text).EnumerateArray().ToArray();
        var clients = Json(core.Execute("j/get windows").Text).EnumerateArray().ToArray();

        Assert.AreEqual(monitors[0].GetProperty("name").GetString(), clients[0].GetProperty("monitor").GetString());
        Assert.AreEqual(monitors[1].GetProperty("name").GetString(), clients[1].GetProperty("monitor").GetString());
        Assert.AreEqual(@"\\.\DISPLAY2", clients[1].GetProperty("monitor").GetString());
        Assert.AreEqual(60, monitors[0].GetProperty("refreshRate").GetDouble());
        Assert.AreEqual(144, monitors[1].GetProperty("refreshRate").GetDouble());
        Assert.AreEqual(1.5, monitors[1].GetProperty("scale").GetDouble());
    }

    [TestMethod]
    public void MonitorsAllIncludesDisabledMonitors()
    {
        var core = new WindowManager();
        var left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1080));
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, false, new Rectangle(1920, 0, 1280, 1024));
        core.Handle(new MonitorsChanged([left, right]));

        var shown = Json(core.Execute("j/get monitors").Text).EnumerateArray().ToArray();
        var all = Json(core.Execute("j/get monitors all").Text).EnumerateArray().ToArray();

        var enabled = Assert.ContainsSingle(shown);
        Assert.AreEqual(@"\\.\DISPLAY1", enabled.GetProperty("name").GetString());
        Assert.IsFalse(enabled.GetProperty("disabled").GetBoolean());

        Assert.AreEqual(2, all.Length);
        var disabled = Assert.ContainsSingle(monitor => monitor.GetProperty("name").GetString() == @"\\.\DISPLAY2", all);
        Assert.IsTrue(disabled.GetProperty("disabled").GetBoolean());
        Assert.IsFalse(disabled.GetProperty("focused").GetBoolean());
        Assert.AreEqual(JsonValueKind.Array, disabled.GetProperty("reserved").ValueKind);
        Assert.AreEqual(4, disabled.GetProperty("reserved").GetArrayLength());
        Assert.AreEqual(JsonValueKind.Object, disabled.GetProperty("specialWorkspace").ValueKind);
        Assert.IsFalse(disabled.GetProperty("specialWorkspace").TryGetProperty("id", out _));
        Assert.AreEqual("", disabled.GetProperty("specialWorkspace").GetProperty("name").GetString());
    }

    [TestMethod]
    public void MonitorsWithoutADisplayStillReportsTheActiveWorkspace()
    {
        const string Monitor =
            """
            {"id":0,"name":"","description":"","make":"","model":"","serial":"","width":0,"height":0,"refreshRate":0,"x":0,"y":0,"activeWorkspace":{"id":1,"type":"normal","name":"1","monitor":""},"specialWorkspace":{"type":"","name":"","monitor":""},"reserved":[0,0,0,0],"scale":1,"focused":true,"disabled":false,"currentFormat":"","hardwareDetails":{"hdr":false,"chroma":false,"bt2020":false}}
            """;

        Assert.AreEqual("[" + Monitor + "]", new WindowManager().Execute("j/get monitors").Text);
    }
}
