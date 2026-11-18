
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "workspaces")]
[TestClass]
public class Workspaces
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void SwitchesAndMovesWindowsBetweenWorkspaces() => VerifyRun.Drive(TestContext, run =>
    {
        run.Holds("winland adopts no window but the test windows", () => run.Ask("-j", "get", "windows") == "[]");
        run.FollowEvents();
        var first = run.OpenManaged("1");
        var second = run.OpenManaged("2");
        run.Until("the two windows tile side by side", () => VerifyRun.SideBySide(VerifyRun.Frame(first), VerifyRun.Frame(second), VerifyRun.WorkArea(first)));

        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides both windows", () => !VerifyRun.Visible(first) && !VerifyRun.Visible(second));
        Assert.AreEqual(2, run.Json("get", "workspace").GetProperty("id").GetInt32());
        Assert.IsTrue(run.ClientOf(first).GetProperty("hidden").GetBoolean());

        run.Dispatch("workspace 1");
        run.Until("workspace 1 shows both windows tiled again", () => VerifyRun.SideBySide(VerifyRun.Frame(first), VerifyRun.Frame(second), VerifyRun.WorkArea(first)));

        run.Dispatch($"focuswindow address:{VerifyRun.Address(second)}");
        run.Dispatch("movetoworkspacesilent 3");
        run.Until("movetoworkspacesilent puts the second window on workspace 3, hidden", () =>
            !VerifyRun.Visible(second) && run.ClientOf(second).GetProperty("workspace").GetProperty("id").GetInt32() == 3);
        Assert.AreEqual(1, run.Json("get", "workspace").GetProperty("id").GetInt32());
        run.Until("the first window fills the work area alone", () => VerifyRun.Frame(first).Near(VerifyRun.WorkArea(first), 12));

        run.Dispatch($"focuswindow address:{VerifyRun.Address(first)}");
        run.Dispatch("movetoworkspace 3");
        run.Until("movetoworkspace follows the window to workspace 3", () =>
            run.Json("get", "workspace").GetProperty("id").GetInt32() == 3 && VerifyRun.Visible(first) && VerifyRun.Visible(second));

        run.UntilEvent("openwindow", "WinlandVerifyWindow,WinlandVerify 1");
        run.UntilEvent("workspace", "3,3,");
        run.UntilEvent("movewindow", VerifyRun.EventAddress(second) + ",3,3,");
        run.UntilEvent("focus", ",3,3," + VerifyRun.EventAddress(first) + ",WinlandVerify 1");
    });
}
