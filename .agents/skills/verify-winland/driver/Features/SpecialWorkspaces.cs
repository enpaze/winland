
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
        run.Dispatch("move special stay");
        run.Until("move special stay hides the window on the special workspace", () =>
            !VerifyRun.Visible(right) && run.ClientOf(right).GetProperty("workspace").GetProperty("name").GetString() == "special:special");

        run.Dispatch("scratchpad");
        run.Until("scratchpad shows the special workspace over workspace 1", () => VerifyRun.Visible(right) && VerifyRun.Visible(left));
        run.UntilEvent("activespecial", "-99,special:special,");
        Assert.AreEqual(1, run.ActiveWorkspace());

        run.Dispatch("scratchpad");
        run.Until("scratchpad again hides it", () => !VerifyRun.Visible(right) && VerifyRun.Visible(left));
        run.UntilEvent("activespecial", ">>,,");
    });
}
