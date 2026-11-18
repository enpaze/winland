using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "layouts")]
public class Layouts(ITestOutputHelper output)
{
    [SmokeFact]
    public void TilesSplitsResizesAndSwitchesLayouts() => SmokeRun.Drive(output, "general:layout = dwindle\n", run =>
    {
        var (left, right) = run.OpenPair();
        Assert.Equal("dwindle", run.Json("activeworkspace").GetProperty("tiledLayout").GetString());

        run.Dispatch("togglesplit");
        run.Until("togglesplit stacks the two tiles", () => SmokeRun.Stacked(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));
        run.Dispatch("togglesplit");
        run.Until("togglesplit puts them side by side again", () => SmokeRun.SideBySide(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));

        run.Focus(left);
        var before = SmokeRun.Frame(left).Width;
        run.Dispatch("resizeactive 120 0");
        run.Until("resizeactive widens the active tile and narrows its neighbour", () =>
            SmokeRun.Frame(left).Width >= before + 80 && SmokeRun.SideBySide(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));

        run.Dispatch("layout scrolling");
        run.Until("layout scrolling selects the scrolling Layout", () => run.Json("activeworkspace").GetProperty("tiledLayout").GetString() == "scrolling");
        run.Focus(left);
        var column = SmokeRun.Frame(left).Width;
        run.Dispatch("layoutmsg colresize +conf");
        run.Until("layoutmsg colresize +conf widens the focused column", () => SmokeRun.Frame(left).Width > column + 40);

        run.Dispatch("cyclelayout");
        run.Until("cyclelayout goes from scrolling to floating and floats the tiles", () =>
            run.Json("activeworkspace").GetProperty("tiledLayout").GetString() == "floating"
            && run.ClientOf(left).GetProperty("floating").GetBoolean()
            && run.ClientOf(right).GetProperty("floating").GetBoolean());

        run.Dispatch("cyclelayout");
        run.Until("cyclelayout goes back to dwindle and tiles the windows side by side", () =>
            run.Json("activeworkspace").GetProperty("tiledLayout").GetString() == "dwindle"
            && !run.ClientOf(left).GetProperty("floating").GetBoolean()
            && SmokeRun.SideBySide(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));
    });
}
