using Windows.Win32;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "window-commands")]
[TestClass]
public class WindowCommands
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void SwapsMovesFloatsPinsAndCloses() => VerifyRun.Drive(TestContext, run =>
    {
        var (left, right) = run.OpenPair();

        run.Focus(left);
        run.Dispatch("swapwindow r");
        run.Until("swapwindow r puts the focused window on the right", () => VerifyRun.Frame(left).Left > VerifyRun.Frame(right).Left);

        run.Dispatch("movewindow l");
        run.Until("movewindow l puts it back on the left", () => VerifyRun.Frame(left).Left < VerifyRun.Frame(right).Left);

        run.Dispatch("togglefloating");
        run.Until("togglefloating floats the window and the other fills the work area", () =>
            run.ClientOf(left).GetProperty("floating").GetBoolean() && VerifyRun.Frame(right).Near(VerifyRun.WorkArea(right), 12));

        run.Dispatch("pin");
        run.Until("pin pins the floating window", () => run.ClientOf(left).GetProperty("pinned").GetBoolean());
        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides the tile and keeps the pinned window shown", () => !VerifyRun.Visible(right) && VerifyRun.Visible(left));
        Assert.AreEqual(2, run.ClientOf(left).GetProperty("workspace").GetProperty("id").GetInt32());

        run.Dispatch("pin");
        run.Until("pin again unpins it", () => !run.ClientOf(left).GetProperty("pinned").GetBoolean());
        run.Dispatch("workspace 1");
        run.Until("workspace 1 shows the tile and hides the unpinned window on workspace 2", () => VerifyRun.Visible(right) && !VerifyRun.Visible(left));

        run.Focus(right);
        run.Dispatch("killactive");
        run.Until("killactive closes the focused window", () => !PInvoke.IsWindow(right) && run.Client(right) is null);
    });
}
