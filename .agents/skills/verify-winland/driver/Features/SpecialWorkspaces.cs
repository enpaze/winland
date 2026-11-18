
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "special-workspaces")]
[TestClass]
public class SpecialWorkspaces
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void ShowsAndHidesASpecialWorkspaceOverTheNormalOne() => VerifyRun.Drive(TestContext, run =>
    {
        run.FollowEvents();
        var (left, right) = run.OpenPair();

        run.Focus(right);
        run.Dispatch("movetoworkspacesilent special");
        run.Until("movetoworkspacesilent special hides the window on the special workspace", () =>
            !VerifyRun.Visible(right) && run.ClientOf(right).GetProperty("workspace").GetProperty("name").GetString() == "special:special");

        run.Dispatch("togglespecialworkspace");
        run.Until("togglespecialworkspace shows the special workspace over workspace 1", () => VerifyRun.Visible(right) && VerifyRun.Visible(left));
        run.UntilEvent("activespecial", "special:special,");
        Assert.AreEqual(1, run.ActiveWorkspace());

        run.Dispatch("togglespecialworkspace");
        run.Until("togglespecialworkspace again hides it", () => !VerifyRun.Visible(right) && VerifyRun.Visible(left));
        run.UntilEvent("activespecial", ">>,");
    });
}
