using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "monitors")]
public class Monitors(ITestOutputHelper output)
{
    [SmokeFact]
    public void ReportsAndFocusesMonitorsAndMovesWorkspacesBetweenThem() => SmokeRun.Drive(output, run =>
    {
        var count = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CMONITORS);
        var monitors = run.Json("monitors").EnumerateArray().ToList();
        Assert.Equal(count, monitors.Count);
        Assert.True(run.Json("monitors", "all").GetArrayLength() >= count);
        Assert.All(monitors, monitor => Assert.True(monitor.GetProperty("width").GetInt32() > 0 && monitor.GetProperty("height").GetInt32() > 0));
        run.Say($"saw: monitors lists the {count} monitor(s) Windows reports");

        var focused = monitors.Single(monitor => monitor.GetProperty("focused").GetBoolean());
        var name = focused.GetProperty("name").GetString()!;
        run.Dispatch($"focusmonitor {name}");
        run.Dispatch("focusmonitor current");
        Assert.Equal(name, run.Json("activeworkspace").GetProperty("monitor").GetString());
        var unknown = run.Cli("dispatch", "focusmonitor", "NoSuchMonitor");
        Assert.Equal(1, unknown.ExitCode);
        Assert.Equal("error: monitor not found", unknown.Error.Trim());

        if (count < 2)
        {
            run.Dispatch("movecurrentworkspacetomonitor current");
            run.Say("only one monitor: focusmonitor and movecurrentworkspacetomonitor answered on it, and moving between monitors was not driven");
            return;
        }

        run.FollowEvents();
        var window = run.OpenManaged("1");
        var other = monitors.First(monitor => !monitor.GetProperty("focused").GetBoolean()).GetProperty("name").GetString()!;
        run.Dispatch($"focusmonitor {other}");
        run.Until($"focusmonitor {other} focuses that monitor", () => run.Json("activeworkspace").GetProperty("monitor").GetString() == other);
        run.UntilEvent("focusedmon", other);

        run.Dispatch($"focusmonitor {name}");
        run.Focus(window);
        run.Dispatch($"movecurrentworkspacetomonitor {other}");
        run.Until($"movecurrentworkspacetomonitor moves workspace 1 and its window onto {other}", () =>
            run.ClientOf(window).GetProperty("workspace").GetProperty("monitor").GetString() == other);
        run.UntilEvent("moveworkspace");

        run.Dispatch($"movecurrentworkspacetomonitor {name}");
        run.Until("movecurrentworkspacetomonitor moves it back", () =>
            run.ClientOf(window).GetProperty("workspace").GetProperty("monitor").GetString() == name);
    });
}
