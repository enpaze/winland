using System.Text.Json;

namespace Winland.Application.Tests;

public class MonitorColorTests
{
    [Fact]
    public void MonitorsColorManagement()
    {
        var desk = new TestDesktop();
        ExpectFields(desk, "colorManagementPreset");

        ExpectFields(desk, "colorManagementPreset", "sdrBrightness", "sdrSaturation");
    }

    private static void ExpectFields(TestDesktop desk, params string[] fields)
    {
        using var monitors = JsonDocument.Parse(desk.Send("j/monitors"));
        foreach (var field in fields)
            Assert.Contains(monitors.RootElement.EnumerateArray(), monitor => monitor.TryGetProperty(field, out _));
    }
}
