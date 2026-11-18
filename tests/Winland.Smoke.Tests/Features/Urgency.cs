using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "urgency")]
public class Urgency(ITestOutputHelper output)
{
    [SmokeFact]
    public void MarksAWindowThatOpensOnAHiddenWorkspaceUrgent() => SmokeRun.Drive(output, "windowrule = float, class:WinlandSmokeWindow, title:WinlandDialog\n", run =>
    {
        run.FollowEvents();
        var (left, _) = run.OpenPair();
        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides workspace 1", () => !SmokeRun.Visible(left));

        var dialog = run.OpenTitled("WinlandDialog", 220, 140, owner: left);
        run.Until(
            "the owned window opens silently on its owner's hidden workspace",
            () => run.Client(dialog)?.GetProperty("workspace").GetProperty("id").GetInt32() == 1 && !SmokeRun.Visible(dialog),
            detail: () => "active workspace " + run.ActiveWorkspace() + " events: " + string.Join(" | ", run.Events));
        run.UntilEvent("urgent", SmokeRun.EventAddress(dialog));
        Assert.Equal(2, run.ActiveWorkspace());
        run.Say("saw: the workspace did not change for the urgent window");
    });
}
