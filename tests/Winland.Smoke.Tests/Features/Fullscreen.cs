using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "fullscreen")]
public class Fullscreen(ITestOutputHelper output)
{
    [SmokeFact]
    public void TogglesFullscreenAndMaximize() => SmokeRun.Drive(output, run =>
    {
        var (left, right) = run.OpenPair();
        run.Focus(left);

        run.Dispatch("fullscreen");
        run.Until("fullscreen covers the whole monitor", () =>
            SmokeRun.Frame(left).Near(SmokeRun.MonitorArea(left), 2) && run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 2);
        Assert.False(run.ClientOf(right).GetProperty("allowedOverFullscreen").GetBoolean());

        run.Dispatch("fullscreen");
        run.Until("fullscreen again puts the window back in its tile", () =>
            run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 0
            && SmokeRun.SideBySide(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));

        run.Dispatch("fullscreen 1");
        run.Until("fullscreen 1 maximizes the window to the work area", () =>
            SmokeRun.Frame(left).Near(SmokeRun.WorkArea(left), 4) && run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 1);

        run.Dispatch("fullscreen 1");
        run.Until("fullscreen 1 again ends maximize", () =>
            run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 0
            && SmokeRun.SideBySide(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));
    });
}
