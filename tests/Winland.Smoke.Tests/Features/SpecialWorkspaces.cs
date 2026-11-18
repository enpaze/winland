using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "special-workspaces")]
public class SpecialWorkspaces(ITestOutputHelper output)
{
    [SmokeFact]
    public void ShowsAndHidesASpecialWorkspaceOverTheNormalOne() => SmokeRun.Drive(output, run =>
    {
        run.FollowEvents();
        var (left, right) = run.OpenPair();

        run.Focus(right);
        run.Dispatch("movetoworkspacesilent special");
        run.Until("movetoworkspacesilent special hides the window on the special workspace", () =>
            !SmokeRun.Visible(right) && run.ClientOf(right).GetProperty("workspace").GetProperty("name").GetString() == "special:special");

        run.Dispatch("togglespecialworkspace");
        run.Until("togglespecialworkspace shows the special workspace over workspace 1", () => SmokeRun.Visible(right) && SmokeRun.Visible(left));
        run.UntilEvent("activespecial", "special:special,");
        Assert.Equal(1, run.ActiveWorkspace());

        run.Dispatch("togglespecialworkspace");
        run.Until("togglespecialworkspace again hides it", () => !SmokeRun.Visible(right) && SmokeRun.Visible(left));
        run.UntilEvent("activespecial", ">>,");
    });
}
