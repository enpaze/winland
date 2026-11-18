using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "shell-windows")]
public class ShellWindows(ITestOutputHelper output)
{
    [SmokeFact]
    public void HidesTheTaskbarWhileRunningAndShowsItOnExit() => SmokeRun.Drive(output, "general:hide_taskbar = true\n", run =>
    {
        run.Until("hide_taskbar hides every taskbar", () => !Taskbars.AllVisible(out _));
        var window = run.OpenManaged("1");
        run.Until("the window takes in the strip the taskbar reserved and fills the monitor", () =>
            SmokeRun.Frame(window).Near(SmokeRun.MonitorArea(window), 12));
        Assert.DoesNotContain(run.Json("clients").EnumerateArray(), client => client.GetProperty("class").GetString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
        run.Say("saw: the taskbar is not a Client");
    });
}
