using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "fullscreen")]
public class Fullscreen(ITestOutputHelper output)
{
    [VerifyFact]
    public void TogglesFullscreenAndMaximize() => VerifyRun.Drive(output, run =>
    {
        var (left, right) = run.OpenPair();
        run.Focus(left);

        run.Dispatch("fullscreen");
        run.Until("fullscreen covers the whole monitor", () =>
            VerifyRun.Frame(left).Near(VerifyRun.MonitorArea(left), 2) && run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 2);
        Assert.False(run.ClientOf(right).GetProperty("allowedOverFullscreen").GetBoolean());

        run.Dispatch("fullscreen");
        run.Until("fullscreen again puts the window back in its tile", () =>
            run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 0
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));

        run.Dispatch("fullscreen 1");
        run.Until("fullscreen 1 maximizes the window to the work area", () =>
            VerifyRun.Frame(left).Near(VerifyRun.WorkArea(left), 4) && run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 1);

        run.Dispatch("fullscreen 1");
        run.Until("fullscreen 1 again ends maximize", () =>
            run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 0
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));
    });
}
