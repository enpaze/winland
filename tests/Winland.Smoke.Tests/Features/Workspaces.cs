using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "workspaces")]
public class Workspaces(ITestOutputHelper output)
{
    [SmokeFact]
    public void SwitchesAndMovesWindowsBetweenWorkspaces() => SmokeRun.Drive(output, run =>
    {
        run.Holds("winland adopts no window but the test windows", () => run.Ask("-j", "clients") == "[]");
        run.FollowEvents();
        var first = run.OpenManaged("1");
        var second = run.OpenManaged("2");
        run.Until("the two windows tile side by side", () => SmokeRun.SideBySide(SmokeRun.Frame(first), SmokeRun.Frame(second), SmokeRun.WorkArea(first)));

        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides both windows", () => !SmokeRun.Visible(first) && !SmokeRun.Visible(second));
        Assert.Equal(2, run.Json("activeworkspace").GetProperty("id").GetInt32());
        Assert.True(run.ClientOf(first).GetProperty("hidden").GetBoolean());

        run.Dispatch("workspace 1");
        run.Until("workspace 1 shows both windows tiled again", () => SmokeRun.SideBySide(SmokeRun.Frame(first), SmokeRun.Frame(second), SmokeRun.WorkArea(first)));

        run.Dispatch($"focuswindow address:{SmokeRun.Address(second)}");
        run.Dispatch("movetoworkspacesilent 3");
        run.Until("movetoworkspacesilent puts the second window on workspace 3, hidden", () =>
            !SmokeRun.Visible(second) && run.ClientOf(second).GetProperty("workspace").GetProperty("id").GetInt32() == 3);
        Assert.Equal(1, run.Json("activeworkspace").GetProperty("id").GetInt32());
        run.Until("the first window fills the work area alone", () => SmokeRun.Frame(first).Near(SmokeRun.WorkArea(first), 12));

        run.Dispatch($"focuswindow address:{SmokeRun.Address(first)}");
        run.Dispatch("movetoworkspace 3");
        run.Until("movetoworkspace follows the window to workspace 3", () =>
            run.Json("activeworkspace").GetProperty("id").GetInt32() == 3 && SmokeRun.Visible(first) && SmokeRun.Visible(second));

        run.UntilEvent("openwindow", "WinlandSmokeWindow,WinlandSmoke 1");
        run.UntilEvent("workspacev2");
        run.UntilEvent("movewindowv2", SmokeRun.Address(second)[2..] + ",3,3,");
    });
}
