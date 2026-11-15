using System.Text.Json;

namespace Winland.Application.Tests;

/// <summary>The color fields of the Monitors reply, and the Monitor keyword that cannot change them.</summary>
public class MonitorColorTests
{
    [Fact]
    public void MonitorsColorManagement()
    {
        var desk = new TestDesktop();
        ExpectFields(desk, "colorManagementPreset");

        ExpectFields(desk, "colorManagementPreset", "sdrBrightness", "sdrSaturation");
    }

    // The reference checks that its monitors reply names these fields.
    private static void ExpectFields(TestDesktop desk, params string[] fields)
    {
        using var monitors = JsonDocument.Parse(desk.Send("j/monitors"));
        foreach (var field in fields)
            Assert.Contains(monitors.RootElement.EnumerateArray(), monitor => monitor.TryGetProperty(field, out _));
    }
}
