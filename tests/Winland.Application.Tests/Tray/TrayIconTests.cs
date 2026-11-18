namespace Winland.Application.Tests;

[TestClass]
public class TrayIconTests
{
    [TestMethod]
    public void TheIconStartsOnWorkspaceOne()
    {
        var outcome = new WindowManager().Execute("workspace 1").Effect;

        Assert.IsNotNull(outcome.Tray);
        Assert.AreEqual("1", outcome.Tray.Number);
        Assert.AreEqual("Winland, Workspace 1", outcome.Tray.Tip);
    }

    [TestMethod]
    public void SwitchingWorkspaceChangesTheNumber()
    {
        var core = new WindowManager();

        var outcome = core.Execute("workspace 2").Effect;

        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.AreEqual("2", outcome.Tray!.Number);
    }

    [TestMethod]
    public void ANamedWorkspaceIsNotCreated()
    {
        var reply = new WindowManager().Execute("workspace name:dev");

        Assert.AreEqual("error: no such workspace", reply.Text);
        Assert.AreEqual(WorkspaceId.Initial, reply.Effect.State.ActiveWorkspace);
        Assert.AreEqual("1", reply.Effect.Tray!.Number);
        Assert.DoesNotContain(workspace => workspace.Id.Name == "dev", reply.Effect.State.Workspaces);
    }

    [TestMethod]
    public void ASpecialWorkspaceKeepsTheNumberUnderneath()
    {
        var core = new WindowManager();
        core.Execute("workspace 4");

        var opened = core.Execute("togglespecialworkspace scratch").Effect;

        Assert.AreEqual(new WorkspaceId("4"), opened.State.ActiveWorkspace);
        Assert.IsNotNull(opened.State.SpecialWorkspace);
        Assert.AreEqual("4", opened.Tray!.Number);
    }

    [TestMethod]
    public void TheFocusedMonitorsWorkspaceIsTheNumber()
    {
        var core = TwoMonitors();
        Assert.AreEqual(new WorkspaceId("1"), core.Execute("workspace 1").Effect.State.ActiveWorkspace);
        Assert.AreEqual("1", core.Execute("focusmonitor LEFT").Effect.Tray!.Number);

        var right = core.Execute("focusmonitor RIGHT").Effect;

        Assert.AreEqual(WorkspaceId.FromNumber(1, new MonitorId(1)), right.State.ActiveWorkspace);
        Assert.AreEqual("1", right.Tray!.Number);
    }

    [TestMethod]
    public void TheMenuIsReloadEditConfigOpenLogAndQuitWhenTheLogExists()
    {
        Assert.AreSequenceEqual(
            [
                new TrayMenuItem(TrayCommand.Reload, "&Reload"),
                new TrayMenuItem(TrayCommand.EditConfig, "&Edit config"),
                new TrayMenuItem(TrayCommand.OpenLog, "Open &log"),
                new TrayMenuItem(TrayCommand.Quit, "&Quit"),
            ],
            TrayIcon.MenuFor(logExists: true));
    }

    [TestMethod]
    public void TheMenuLeavesOutOpenLogWhenThereIsNoLogFile()
    {
        Assert.AreSequenceEqual(
            [
                new TrayMenuItem(TrayCommand.Reload, "&Reload"),
                new TrayMenuItem(TrayCommand.EditConfig, "&Edit config"),
                new TrayMenuItem(TrayCommand.Quit, "&Quit"),
            ],
            TrayIcon.MenuFor(logExists: false));
    }

    [TestMethod]
    [DataRow("special:", "special")]
    [DataRow("special:scratch", "scratch")]
    [DataRow("12", "12")]
    public void TextUsesTheNumberOrTheName(string name, string text)
    {
        Assert.AreEqual(text, TrayIcon.Text(new WorkspaceId(name)));
    }

    private static WindowManager TwoMonitors()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged(
        [
            new Monitor("LEFT", "Left", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1080)),
            new Monitor("RIGHT", "Right", 1920, 0, 1920, 1080, 1, true, new Rectangle(1920, 0, 1920, 1080)),
        ]));
        core.Execute("focusmonitor LEFT");
        return core;
    }
}
