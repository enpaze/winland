using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "animations")]
[TestClass]
public class Animations
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public unsafe void SlidesAWorkspaceInThroughFrames()
    {
        int animate = 0;
        _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, 0, &animate, 0);
        var turnedOn = animate == 0;
        if (turnedOn)
        {
            TestContext.WriteLine("Windows Animation effects were off, so the driver turns them on for this run without saving the change");
            _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, 0, (void*)1, 0);
        }

        try
        {
            VerifyRun.Drive(TestContext, "animations = true\n", run =>
            {
                var window = run.OpenManaged("1");
                run.Until("the window fills the work area", () => VerifyRun.Frame(window).Near(VerifyRun.WorkArea(window), 12), 5_000);
                var rest = VerifyRun.Frame(window).Left;
                var monitor = VerifyRun.MonitorArea(window);

                run.Focus(window);
                run.Dispatch("move 2 stay");
                run.Until("the window leaves for hidden workspace 2", () => !VerifyRun.Visible(window));

                run.Dispatch("workspace 2");
                var lefts = new List<int>();
                var deadline = Environment.TickCount64 + 1_500;
                while (Environment.TickCount64 < deadline)
                {
                    if (VerifyRun.Visible(window))
                        lefts.Add(VerifyRun.Frame(window).Left);

                    Thread.Sleep(5);
                }

                var seen = lefts.Distinct().ToList();
                run.Say($"sampled {lefts.Count} left edges of the window as workspace 2 came in: {string.Join(",", seen)}");
                var between = seen.Count(left => left > rest + 20 && left < monitor.Right);
                Assert.IsTrue(between >= 3, $"Workspace 2 did not slide in from the right through frames: {string.Join(",", seen)}");
                run.Say($"saw: the window passed through {between} places between the right edge and its tile");
                run.Until("the window ends on its tile", () => VerifyRun.Frame(window).Near(VerifyRun.WorkArea(window), 12));
            });
        }
        finally
        {
            if (turnedOn)
                _ = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, 0, (void*)0, 0);
        }
    }

    private const int TitleBand = 48;

    private const int StrayPixels = 50;

    private const int BorderBlendSettlesMs = 1_200;

    [VerifyFact]
    public void ASlideIsDrawnOnlyOnItsMonitor()
    {
        VerifyRun.Drive(TestContext, "animations = true\nborder_size = 2\n", run =>
        {
            if (run.Json("get", "monitors").GetArrayLength() < 2)
            {
                run.Say("only one monitor: a slide beside another monitor was not driven");
                return;
            }

            var leaving = run.OpenManaged("1");
            run.Until("window 1 fills its monitor", () => VerifyRun.Frame(leaving).Near(VerifyRun.WorkArea(leaving), 12), 5_000);
            var own = VerifyRun.MonitorArea(leaving);
            var coming = run.OpenManaged("2");
            run.Focus(coming);
            run.Dispatch("move 2 stay");
            run.Until("window 2 leaves for hidden workspace 2", () => !VerifyRun.Visible(coming));

            var beside = run.OpenManaged("3");
            run.Windows.PaintDark(beside);
            run.Focus(beside);
            run.Dispatch($"move mon:{(own.Left == 0 ? "right" : "left")}");
            run.Until("window 3 fills the other monitor", () => VerifyRun.MonitorArea(beside) != own && VerifyRun.Frame(beside).Near(VerifyRun.WorkArea(beside), 12), 5_000);
            var work = VerifyRun.WorkArea(beside);
            var watched = work with { Top = work.Top + TitleBand };

            run.Focus(leaving);
            Thread.Sleep(BorderBlendSettlesMs);
            var baseline = ScreenPixels.Capture(watched);
            run.Say($"watching {watched} on the other monitor while monitor {own} slides");

            foreach (var workspace in (string[])["2", "1"])
            {
                run.Dispatch($"workspace {workspace}");
                var frames = 0;
                var worst = (Count: 0, Bounds: default(Box), Sample: 0u);
                var deadline = Environment.TickCount64 + 1_500;
                while (Environment.TickCount64 < deadline)
                {
                    var changed = ScreenPixels.Changed(baseline, ScreenPixels.Capture(watched), watched);
                    frames++;
                    if (changed.Count > worst.Count)
                        worst = changed;
                }

                run.Say($"workspace {workspace}: captured {frames} frames of the other monitor, at most {worst.Count} pixels changed in one");
                Assert.IsTrue(worst.Count <= StrayPixels, $"Switching to workspace {workspace} drew {worst.Count} pixels on the other monitor at {worst.Bounds}, colour 0x{worst.Sample:x6}.");
                run.Say($"saw: switching to workspace {workspace} drew no window on the other monitor");
            }
        });
    }
}
