using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "groups")]
public class Groups(ITestOutputHelper output)
{
    [SmokeFact]
    public void GroupsWindowsIntoOneTileAndCyclesThem() => SmokeRun.Drive(output, run =>
    {
        run.FollowEvents();
        var (left, right) = run.OpenPair();

        run.Focus(left);
        run.Dispatch("togglegroup");
        run.Until("togglegroup makes the left window a Group of one", () => run.ClientOf(left).GetProperty("grouped").GetArrayLength() == 1);
        run.UntilEvent("togglegroup", "1," + SmokeRun.EventAddress(left));

        run.Focus(right);
        run.Dispatch("moveintogroup l");
        run.Until("moveintogroup l puts the right window into that Group, shown, with the other hidden", () =>
            run.ClientOf(left).GetProperty("grouped").GetArrayLength() == 2
            && SmokeRun.Visible(right) && !SmokeRun.Visible(left)
            && run.ClientOf(left).GetProperty("hidden").GetBoolean());
        run.Until("the Group fills the work area as one tile", () => SmokeRun.Frame(right).Width >= SmokeRun.WorkArea(right).Width * 9 / 10);

        run.Dispatch("changegroupactive");
        run.Until("changegroupactive shows the other member", () => SmokeRun.Visible(left) && !SmokeRun.Visible(right));

        run.Dispatch("moveoutofgroup");
        run.Until("moveoutofgroup gives the member its own tile again", () =>
            SmokeRun.Visible(left) && SmokeRun.Visible(right)
            && SmokeRun.SideBySide(SmokeRun.Frame(left), SmokeRun.Frame(right), SmokeRun.WorkArea(left)));
    });
}
