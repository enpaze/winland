using Windows.Win32.UI.Input.KeyboardAndMouse;
using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "virtual-desktops")]
public class VirtualDesktops(ITestOutputHelper output)
{
    [VerifyFact]
    public void ReleasesWindowsOnAnotherDesktopAndTakesThemBack() => VerifyRun.Drive(output, run =>
    {
        var (left, right) = run.OpenPair();
        var leftBox = VerifyRun.Frame(left);
        var rightBox = VerifyRun.Frame(right);

        var created = false;
        try
        {
            Desktop.Chord(VIRTUAL_KEY.VK_D, VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_CONTROL);
            created = true;
            run.Say("pressed Win+Ctrl+D to make and show a new virtual desktop");
            run.Until("the windows left behind stop being Clients", () => run.Client(left) is null && run.Client(right) is null, 15_000);
        }
        finally
        {
            if (created)
            {
                Desktop.Chord(VIRTUAL_KEY.VK_F4, VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_CONTROL);
                run.Say("pressed Win+Ctrl+F4 to close that desktop and go back");
            }
        }

        run.Until("both windows are Clients again on workspace 1", () =>
            run.Client(left)?.GetProperty("workspace").GetProperty("id").GetInt32() == 1
            && run.Client(right)?.GetProperty("workspace").GetProperty("id").GetInt32() == 1, 15_000);
        run.Until("they come back to their former tiles", () => VerifyRun.Frame(left).Near(leftBox) && VerifyRun.Frame(right).Near(rightBox));
    });
}
