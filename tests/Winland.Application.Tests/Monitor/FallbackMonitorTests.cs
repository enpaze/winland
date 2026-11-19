namespace Winland.Application.Tests;

[TestClass]
public class FallbackMonitorTests
{
    [TestMethod]
    public void DisabledMonitorsHandTheirWorkspacesToFallback()
    {
        var desk = new TestDesktop();

        desk.Ok("focus-monitor", "HEADLESS-2");
        desk.Ok("workspace", "9");
        desk.Spawn("fallback-workspace-a");

        Assert.DoesNotContain("FALLBACK", desk.Send("j/get monitors"), StringComparison.Ordinal);
        Assert.AreEqual("HEADLESS-2", MonitorOf(desk, 9));

        desk.ReportEnabled(false);
        Assert.Contains("FALLBACK", desk.Send("j/get monitors"), StringComparison.Ordinal);
        Assert.AreEqual("FALLBACK", desk.ActiveWorkspace().GetProperty("monitor").GetString());
        Assert.AreEqual(1, desk.ActiveWorkspace().GetProperty("id").GetInt32());

        desk.Ok("workspace", "9");
        desk.Spawn("fallback-workspace-b");

        Assert.AreEqual("ok", desk.Send("reload"));
        Assert.AreEqual("FALLBACK", MonitorOf(desk, 9));

        desk.ReportEnabled(true);
        Assert.AreEqual("HEADLESS-2", MonitorOf(desk, 9));
    }

    private static string? MonitorOf(TestDesktop desk, int workspace) =>
        desk.Workspaces().Single(item => item.GetProperty("id").GetInt32() == workspace).GetProperty("monitor").GetString();
}
