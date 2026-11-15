using System.Text.Json;

namespace Winland.Application.Tests;

/// <summary>The empty Workspaces Winland lets go.</summary>
public class PersistentWorkspaceTests
{
    [Fact]
    public void WorkspaceLifecycleReleasesEmptyWorkspaces()
    {
        var desk = new TestDesktop();

        desk.AddMonitor("HEADLESS-LIFECYCLE-OWNERSHIP", TestDesktop.Second);
        Assert.Contains("HEADLESS-LIFECYCLE-OWNERSHIP", MonitorNames(desk));
        desk.Ok("focusmonitor", "HEADLESS-LIFECYCLE-OWNERSHIP");
        desk.Ok("workspace", "410");
        Assert.Equal("410 (410)", Label(desk.ActiveWorkspace()));

        desk.RemoveMonitor("HEADLESS-LIFECYCLE-OWNERSHIP");
        Assert.DoesNotContain("HEADLESS-LIFECYCLE-OWNERSHIP", MonitorNames(desk));
        Assert.DoesNotContain("410 (410)", Labels(desk));

        desk.AddMonitor("HEADLESS-LIFECYCLE-OWNERSHIP", TestDesktop.Second);
        Assert.Contains("HEADLESS-LIFECYCLE-OWNERSHIP", MonitorNames(desk));
        // The empty Workspace it was showing comes back under that number.
        Assert.Contains("410 (410)", Labels(desk));
        desk.Ok("focusmonitor", "HEADLESS-LIFECYCLE-OWNERSHIP");
        Assert.Equal("410 (410)", Label(desk.ActiveWorkspace()));

        desk.RemoveMonitor("HEADLESS-LIFECYCLE-OWNERSHIP");
        Assert.DoesNotContain("HEADLESS-LIFECYCLE-OWNERSHIP", MonitorNames(desk));
    }

    private static string[] Labels(TestDesktop desk) => [.. desk.Workspaces().Select(Label)];

    // A Workspace as the reference reply writes it, `ID (NAME)`: a numbered Workspace's id, or else its name, then its name.
    private static string Label(JsonElement workspace)
    {
        var id = workspace.TryGetProperty("id", out var number) ? number.GetInt32() : 0;
        return id > 0 ? $"{id} ({Name(workspace)})" : $"{Name(workspace)} ({Name(workspace)})";
    }

    private static string Name(JsonElement workspace) => workspace.GetProperty("name").GetString() ?? "";

    private static string[] MonitorNames(TestDesktop desk)
    {
        using var document = JsonDocument.Parse(desk.Send("j/monitors"));
        return [.. document.RootElement.EnumerateArray().Select(monitor => monitor.GetProperty("name").GetString() ?? "")];
    }
}
