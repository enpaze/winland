
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "layouts")]
[TestClass]
public class Layouts
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void TilesSplitsResizesAndSwitchesLayouts() => VerifyRun.Drive(TestContext, "general:layout = dwindle\n", run =>
    {
        var (left, right) = run.OpenPair();
        Assert.AreEqual("dwindle", run.Json("activeworkspace").GetProperty("tiledLayout").GetString());

        run.Dispatch("togglesplit");
        run.Until("togglesplit stacks the two tiles", () => VerifyRun.Stacked(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));
        run.Dispatch("togglesplit");
        run.Until("togglesplit puts them side by side again", () => VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));

        run.Focus(left);
        var before = VerifyRun.Frame(left).Width;
        run.Dispatch("resizeactive 120 0");
        run.Until("resizeactive widens the active tile and narrows its neighbour", () =>
            VerifyRun.Frame(left).Width >= before + 80 && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));

        run.Dispatch("layout scrolling");
        run.Until("layout scrolling selects the scrolling Layout", () => run.Json("activeworkspace").GetProperty("tiledLayout").GetString() == "scrolling");
        run.Focus(left);
        var column = VerifyRun.Frame(left).Width;
        run.Dispatch("layoutmsg colresize +conf");
        run.Until("layoutmsg colresize +conf widens the focused column", () => VerifyRun.Frame(left).Width > column + 40);

        run.Dispatch("cyclelayout");
        run.Until("cyclelayout goes from scrolling to floating and floats the tiles", () =>
            run.Json("activeworkspace").GetProperty("tiledLayout").GetString() == "floating"
            && run.ClientOf(left).GetProperty("floating").GetBoolean()
            && run.ClientOf(right).GetProperty("floating").GetBoolean());

        run.Dispatch("cyclelayout");
        run.Until("cyclelayout goes back to dwindle and tiles the windows side by side", () =>
            run.Json("activeworkspace").GetProperty("tiledLayout").GetString() == "dwindle"
            && !run.ClientOf(left).GetProperty("floating").GetBoolean()
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));
    });
}
