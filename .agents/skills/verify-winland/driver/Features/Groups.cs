
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "groups")]
[TestClass]
public class Groups
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void GroupsWindowsIntoOneTileAndCyclesThem() => VerifyRun.Drive(TestContext, run =>
    {
        run.FollowEvents();
        var (left, right) = run.OpenPair();

        run.Focus(left);
        run.Dispatch("group");
        run.Until("group makes the left window a Group of one", () => run.ClientOf(left).GetProperty("grouped").GetArrayLength() == 1);
        run.UntilEvent("togglegroup", "1," + VerifyRun.EventAddress(left));

        run.Focus(right);
        run.Dispatch("join left");
        run.Until("join left puts the right window into that Group, shown, with the other hidden", () =>
            run.ClientOf(left).GetProperty("grouped").GetArrayLength() == 2
            && VerifyRun.Visible(right) && !VerifyRun.Visible(left)
            && run.ClientOf(left).GetProperty("hidden").GetBoolean());
        run.Until("the Group fills the work area as one tile", () => VerifyRun.Frame(right).Width >= VerifyRun.WorkArea(right).Width * 9 / 10);

        run.Dispatch("tab next");
        run.Until("tab next shows the other member", () => VerifyRun.Visible(left) && !VerifyRun.Visible(right));

        run.Dispatch("leave-group");
        run.Until("leave-group gives the member its own tile again", () =>
            VerifyRun.Visible(left) && VerifyRun.Visible(right)
            && VerifyRun.SideBySide(VerifyRun.Frame(left), VerifyRun.Frame(right), VerifyRun.WorkArea(left)));
    });
}
