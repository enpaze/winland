
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "shell-windows")]
[TestClass]
public class ShellWindows
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void HidesTheTaskbarWhileRunningAndShowsItOnExit() => VerifyRun.Drive(TestContext, "hide_taskbar = true\n", run =>
    {
        run.Until("hide_taskbar hides every taskbar", () => !Taskbars.AllVisible(out _));
        var window = run.OpenManaged("1");
        run.Until("the window takes in the strip the taskbar reserved and fills the monitor", () =>
            VerifyRun.Frame(window).Near(VerifyRun.MonitorArea(window), 12));
        Assert.DoesNotContain(client => client.GetProperty("class").GetString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd", run.Json("get", "windows").EnumerateArray());
        run.Say("saw: the taskbar is not a Client");
    });
}
