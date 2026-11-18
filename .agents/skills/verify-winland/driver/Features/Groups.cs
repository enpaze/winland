using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "groups")]
public class Groups(ITestOutputHelper output)
{
    [VerifyFact]
    public void GroupsWindowsIntoOneTileAndCyclesThem() => VerifyRun.Drive(output, run =>
    {
        run.FollowEvents();
        var (left, right) = run.OpenPair();

        run.Focus(left);
        run.Dispatch("togglegroup");
        run.Until("togglegroup makes the left window a Group of one", () => run.ClientOf(left).GetProperty("grouped").GetArrayLength() == 1);
        run.UntilEvent("togglegroup", "1," + VerifyRun.EventAddress(left));

        run.Focus(right);
        run.Dispatch("moveintogroup l");
        run.Until("moveintogroup l puts the right window into that Group, shown, with the other hidden", () =>
            run.ClientOf(left).GetProperty("grouped").GetArrayLength() == 2
            && VerifyRun.Visible(right) && !VerifyRun.Visible(left)
            && run.ClientOf(left).GetProperty("hidden").GetBoolean());
        run.Until("the Group fills the work area as one tile", () => VerifyRun.Frame(right).Width >= VerifyRun.WorkArea(right).Width * 9 / 10);

        run.Dispatch("changegroupactive");
        run.Until("changegroupactive shows the other member", () => VerifyRun.Visible(left) && !VerifyRun.Visible(right));

        run.Dispatch("moveoutofgroup");
        run.Until("moveoutofgroup gives the member its own tile again", () =>
            VerifyRun.Visible(left) && VerifyRun.Visible(right)
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));
    });
}
