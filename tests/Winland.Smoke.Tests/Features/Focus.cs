using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "focus")]
public class Focus(ITestOutputHelper output)
{
    [SmokeFact]
    public void FocusesByAddressAndByDirection() => SmokeRun.Drive(output, run =>
    {
        var (left, right) = run.OpenPair();

        run.Focus(left);

        run.Dispatch("movefocus r");
        run.Until("movefocus r focuses the right window", () => run.Active() == SmokeRun.Address(right));
        var frame = SmokeRun.Frame(right);
        var (x, y) = Desktop.Cursor();
        Assert.True(Math.Abs(x - ((frame.Left + frame.Right) / 2)) <= 4 && Math.Abs(y - ((frame.Top + frame.Bottom) / 2)) <= 4, $"movefocus left the cursor at {x},{y}, not the middle of {frame}.");
        run.Say($"saw: movefocus put the cursor in the middle of the window it focused ({x},{y})");

        run.Dispatch("movefocus l");
        run.Until("movefocus l focuses the left window again", () => run.Active() == SmokeRun.Address(left));

        run.Cli("dispatch", "focuswindow", "title:NoSuchWindow");
        run.Holds("focuswindow on an unmatched selector leaves focus where it is", () => run.Active() == SmokeRun.Address(left), 500);
    });
}
