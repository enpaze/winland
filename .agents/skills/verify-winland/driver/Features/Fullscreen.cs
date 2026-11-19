
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "fullscreen")]
[TestClass]
public class Fullscreen
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void TogglesFullscreenAndMaximize() => VerifyRun.Drive(TestContext, run =>
    {
        var (left, right) = run.OpenPair();
        run.Focus(left);

        run.Dispatch("fullscreen");
        run.Until("fullscreen covers the whole monitor", () =>
            VerifyRun.Frame(left).Near(VerifyRun.MonitorArea(left), 2) && run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 2);
        Assert.IsFalse(run.ClientOf(right).GetProperty("allowedOverFullscreen").GetBoolean());

        run.Dispatch("fullscreen");
        run.Until("fullscreen again puts the window back in its tile", () =>
            run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 0
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));

        run.Dispatch("maximize");
        run.Until("maximize fills the work area", () =>
            VerifyRun.Frame(left).Near(VerifyRun.WorkArea(left), 4) && run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 1);

        run.Dispatch("maximize");
        run.Until("maximize again ends maximize", () =>
            run.ClientOf(left).GetProperty("fullscreen").GetInt32() == 0
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));
    });
}
