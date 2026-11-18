using Windows.Win32.UI.Input.KeyboardAndMouse;
using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "binds")]
public class Binds(ITestOutputHelper output)
{
    private const string Config =
        """
        $hyper = CTRL ALT SHIFT
        bind = $hyper, F9, workspace, 4
        bind = $hyper, F10, workspace, 1
        input:follow_mouse = true

        """;

    [VerifyFact]
    public void RunsKeyBindsAndFollowsTheMouse() => VerifyRun.Drive(output, Config, run =>
    {
        var binds = run.Ask("binds");
        Assert.Contains("bind = SHIFT CTRL ALT, F9, workspace, 4", binds, StringComparison.Ordinal);
        var json = run.Json("binds").EnumerateArray().ToList();
        Assert.Contains(json, bind => bind.GetProperty("key").GetString() == "F9" && bind.GetProperty("modmask").GetInt32() == 13 && bind.GetProperty("dispatcher").GetString() == "workspace");
        run.Say("saw: binds lists the config's binds, with CTRL ALT SHIFT as modmask 13");

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
