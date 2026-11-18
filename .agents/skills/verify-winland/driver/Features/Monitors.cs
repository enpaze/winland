using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "monitors")]
[TestClass]
public class Monitors
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void ReportsAndFocusesMonitorsAndMovesWorkspacesBetweenThem() => VerifyRun.Drive(TestContext, run =>
    {
        var count = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CMONITORS);
        var monitors = run.Json("monitors").EnumerateArray().ToList();
        Assert.AreEqual(count, monitors.Count);
        Assert.IsTrue(run.Json("monitors", "all").GetArrayLength() >= count);
        foreach (var monitor in monitors)
        {
            Assert.IsTrue(monitor.GetProperty("width").GetInt32() > 0 && monitor.GetProperty("height").GetInt32() > 0);
            Assert.AreEqual(
                "id name description make model serial width height refreshRate x y activeWorkspace specialWorkspace reserved scale focused disabled currentFormat hardwareDetails",
                string.Join(' ', monitor.EnumerateObject().Select(property => property.Name)));
        }
        run.Say($"saw: monitors lists the {count} monitor(s) Windows reports");

        var focused = monitors.Single(monitor => monitor.GetProperty("focused").GetBoolean());
        var name = focused.GetProperty("name").GetString()!;
        run.Dispatch($"focusmonitor {name}");
        run.Dispatch("focusmonitor current");
        Assert.AreEqual(name, run.Json("activeworkspace").GetProperty("monitor").GetString());
        var unknown = run.Cli("dispatch", "focusmonitor", "NoSuchMonitor");
        Assert.AreEqual(1, unknown.ExitCode);
        Assert.AreEqual("error: monitor not found", unknown.Error.Trim());

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
        run.UntilEvent("focus", other + ",");

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
