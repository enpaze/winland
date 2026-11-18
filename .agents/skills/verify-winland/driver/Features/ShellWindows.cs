using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "shell-windows")]
public class ShellWindows(ITestOutputHelper output)
{
    [VerifyFact]
    public void HidesTheTaskbarWhileRunningAndShowsItOnExit() => VerifyRun.Drive(output, "general:hide_taskbar = true\n", run =>
    {
        run.Until("hide_taskbar hides every taskbar", () => !Taskbars.AllVisible(out _));
        var window = run.OpenManaged("1");
        run.Until("the window takes in the strip the taskbar reserved and fills the monitor", () =>
            VerifyRun.Frame(window).Near(VerifyRun.MonitorArea(window), 12));
        Assert.DoesNotContain(run.Json("clients").EnumerateArray(), client => client.GetProperty("class").GetString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
        run.Say("saw: the taskbar is not a Client");
    });
}
