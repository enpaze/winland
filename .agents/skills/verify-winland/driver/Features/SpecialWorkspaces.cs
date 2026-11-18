using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "special-workspaces")]
public class SpecialWorkspaces(ITestOutputHelper output)
{
    [VerifyFact]
    public void ShowsAndHidesASpecialWorkspaceOverTheNormalOne() => VerifyRun.Drive(output, run =>
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
        Assert.Equal(1, run.ActiveWorkspace());

        run.Dispatch("togglespecialworkspace");
        run.Until("togglespecialworkspace again hides it", () => !VerifyRun.Visible(right) && VerifyRun.Visible(left));
        run.UntilEvent("activespecial", ">>,");
    });
}
