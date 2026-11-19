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
        var monitors = run.Json("get", "monitors").EnumerateArray().ToList();
        Assert.AreEqual(count, monitors.Count);
        Assert.IsTrue(run.Json("get", "monitors", "all").GetArrayLength() >= count);
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
        run.Dispatch($"focus-monitor {name}");
        run.Dispatch($"focus-monitor {focused.GetProperty("id").GetInt32()}");
        Assert.AreEqual(name, run.Json("get", "workspace").GetProperty("monitor").GetString());
        var unknown = run.Cli("focus-monitor", "NoSuchMonitor");
        Assert.AreEqual(1, unknown.ExitCode);
        Assert.AreEqual("error: monitor not found", unknown.Error.Trim());

        if (count < 2)
        {
            run.Dispatch($"move-workspace {name}");
            run.Say("only one monitor: focus-monitor and move-workspace answered on it, and moving between monitors was not driven");
            return;
        }

        run.FollowEvents();
        var window = run.OpenManaged("1");
        var other = monitors.First(monitor => !monitor.GetProperty("focused").GetBoolean()).GetProperty("name").GetString()!;
        run.Dispatch($"focus-monitor {other}");
        run.Until($"focus-monitor {other} focuses that monitor", () => run.Json("get", "workspace").GetProperty("monitor").GetString() == other);
        run.UntilEvent("focus", other + ",");

        run.Dispatch($"focus-monitor {name}");
        run.Focus(window);
        run.Dispatch($"move-workspace {other}");
        run.Until($"move-workspace moves workspace 1 and its window onto {other}", () =>
            run.ClientOf(window).GetProperty("workspace").GetProperty("monitor").GetString() == other);
        run.UntilEvent("moveworkspace");

        run.Dispatch($"move-workspace {name}");
        run.Until("move-workspace moves it back", () =>
            run.ClientOf(window).GetProperty("workspace").GetProperty("monitor").GetString() == name);
    });
}
