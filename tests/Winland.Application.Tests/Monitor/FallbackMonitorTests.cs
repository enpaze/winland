namespace Winland.Application.Tests;

public class FallbackMonitorTests
{
    [Fact]
    public void DisabledMonitorsHandTheirWorkspacesToFallback()
    {
        var desk = new TestDesktop();

        desk.Ok("focusmonitor", "HEADLESS-2");
        desk.Ok("workspace", "9");
        desk.Spawn("fallback-workspace-a");

        Assert.DoesNotContain("FALLBACK", desk.Send("j/monitors"), StringComparison.Ordinal);
        Assert.Equal("HEADLESS-2", MonitorOf(desk, 9));

        desk.ReportEnabled(false);
        Assert.Contains("FALLBACK", desk.Send("j/monitors"), StringComparison.Ordinal);
        Assert.Equal("FALLBACK", desk.ActiveWorkspace().GetProperty("monitor").GetString());
        Assert.Equal(1, desk.ActiveWorkspace().GetProperty("id").GetInt32());

        desk.Ok("workspace", "9");
        desk.Spawn("fallback-workspace-b");

        Assert.Equal("ok", desk.Send("reload"));
        Assert.Equal("FALLBACK", MonitorOf(desk, 9));

        desk.ReportEnabled(true);
        Assert.Equal("HEADLESS-2", MonitorOf(desk, 9));
    }

    private static string? MonitorOf(TestDesktop desk, int workspace) =>
        desk.Workspaces().Single(item => item.GetProperty("id").GetInt32() == workspace).GetProperty("monitor").GetString();
}
