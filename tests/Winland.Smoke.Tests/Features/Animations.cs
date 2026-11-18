using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "animations")]
public class Animations(ITestOutputHelper output)
{
    [SmokeFact]
    public unsafe void SlidesAWorkspaceInThroughFrames()
    {
        int animate = 0;
        _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, 0, &animate, 0);
        var turnedOn = animate == 0;
        if (turnedOn)
        {
            output.WriteLine("Windows Animation effects were off, so the driver turns them on for this run without saving the change");
            _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, 0, (void*)1, 0);
        }

        try
        {
            SmokeRun.Drive(output, "animations:enabled = true\n", run =>
            {
                var window = run.OpenManaged("1");
                run.Until("the window fills the work area", () => SmokeRun.Frame(window).Near(SmokeRun.WorkArea(window), 12), 5_000);
                var rest = SmokeRun.Frame(window).Left;
                var monitor = SmokeRun.MonitorArea(window);

                run.Focus(window);
                run.Dispatch("movetoworkspacesilent 2");
                run.Until("the window leaves for hidden workspace 2", () => !SmokeRun.Visible(window));

                run.Dispatch("workspace 2");
                var lefts = new List<int>();
                var deadline = Environment.TickCount64 + 1_500;
                while (Environment.TickCount64 < deadline)
                {
                    if (SmokeRun.Visible(window))
                        lefts.Add(SmokeRun.Frame(window).Left);

                    Thread.Sleep(5);
                }

                var seen = lefts.Distinct().ToList();
                run.Say($"sampled {lefts.Count} left edges of the window as workspace 2 came in: {string.Join(",", seen)}");
                var between = seen.Count(left => left > rest + 20 && left < monitor.Right);
                Assert.True(between >= 3, $"Workspace 2 did not slide in from the right through frames: {string.Join(",", seen)}");
                run.Say($"saw: the window passed through {between} places between the right edge and its tile");
                run.Until("the window ends on its tile", () => SmokeRun.Frame(window).Near(SmokeRun.WorkArea(window), 12));
            });
        }
        finally
        {
            if (turnedOn)
                _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, 0, (void*)0, 0);
        }
    }
}
