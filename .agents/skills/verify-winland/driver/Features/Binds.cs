using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "binds")]
[TestClass]
public class Binds
{

    public TestContext TestContext { get; set; } = null!;
        private const string Config =
        """
        $hyper = Ctrl+Alt+Shift
        bind = $hyper+F9, workspace 4
        bind = $hyper+F10, "Back to workspace 1", workspace 1
        bind = $hyper+MOUSE:X2, workspace 1
        input:follow_mouse = true

        """;

    [VerifyFact]
    public void RunsKeyBindsAndFollowsTheMouse() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var binds = run.Ask("get", "binds");
        Assert.Contains("bind = Ctrl+Alt+Shift+F9, workspace 4", binds, StringComparison.Ordinal);
        Assert.Contains("bind = Ctrl+Alt+Shift+F10, \"Back to workspace 1\", workspace 1", binds, StringComparison.Ordinal);
        Assert.Contains("bind = Ctrl+Alt+Shift+mouse:x2, workspace 1", binds, StringComparison.Ordinal);
        var json = run.Json("get", "binds").EnumerateArray().ToList();
        Assert.Contains(bind => bind.GetProperty("key").GetString() == "F9" && bind.GetProperty("modMask").GetInt32() == 13 && bind.GetProperty("dispatcher").GetString() == "workspace", json);
        Assert.Contains(bind => bind.GetProperty("key").GetString() == "mouse:x2" && bind.GetProperty("mouse").GetBoolean(), json);
        run.Say("saw: binds lists the config's binds as chords with the description quoted, Ctrl+Alt+Shift as modMask 13 and the mouse button by name");

        var (left, right) = run.OpenPair();

        Desktop.Chord(VIRTUAL_KEY.VK_F9, VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_SHIFT);
        run.Say("pressed Ctrl+Alt+Shift+F9");
        run.Until("the bind switched to workspace 4", () => run.ActiveWorkspace() == 4 && !VerifyRun.Visible(left));

        Desktop.Chord(VIRTUAL_KEY.VK_F10, VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_SHIFT);
        run.Say("pressed Ctrl+Alt+Shift+F10");
        run.Until("the other bind switched back to workspace 1", () => run.ActiveWorkspace() == 1 && VerifyRun.Visible(left));

        run.Focus(left);
        var target = VerifyRun.Frame(right);
        var (x, y) = ((target.Left + target.Right) / 2, (target.Top + target.Bottom) / 2);
        for (var step = 0; step < 15; step++)
            Desktop.MoveMouse(x + (step % 3) - 1, y + (step % 2));

        run.Say($"moved the pointer onto the right window at {x},{y}");
        run.Until("follow_mouse focused the window under the pointer", () => run.Active() == VerifyRun.Address(right));
    });
}
