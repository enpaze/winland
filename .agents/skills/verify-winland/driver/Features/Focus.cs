
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "focus")]
[TestClass]
public class Focus
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void FocusesBySelectorAndByDirection() => VerifyRun.Drive(TestContext, run =>
    {
        var (left, right) = run.OpenPair();

        run.Focus(left);

        run.Dispatch("focus right");
        run.Until("focus right focuses the right window", () => run.Active() == VerifyRun.Address(right));
        var frame = VerifyRun.Frame(right);
        var (x, y) = Desktop.Cursor();
        Assert.IsTrue(Math.Abs(x - ((frame.Left + frame.Right) / 2)) <= 4 && Math.Abs(y - ((frame.Top + frame.Bottom) / 2)) <= 4, $"focus right left the cursor at {x},{y}, not the middle of {frame}.");
        run.Say($"saw: focus right put the cursor in the middle of the window it focused ({x},{y})");

        run.Dispatch("focus left");
        run.Until("focus left focuses the left window again", () => run.Active() == VerifyRun.Address(left));

        var title = run.ClientOf(right).GetProperty("title").GetString()!;
        run.Dispatch($"focus title:{title.Replace(' ', '*').ToLowerInvariant()}");
        run.Until("a title pattern focuses the right window", () => run.Active() == VerifyRun.Address(right));

        run.Dispatch($"focus exe:{Path.GetFileName(Environment.ProcessPath)!.ToUpperInvariant()}");
        run.Until("an exe pattern focuses a window of the driver's process", () => run.Active() == VerifyRun.Address(left) || run.Active() == VerifyRun.Address(right));

        run.Dispatch("focus address:" + VerifyRun.Address(left));
        run.Until("address focuses the left window", () => run.Active() == VerifyRun.Address(left));

        run.Cli("focus", "title:NoSuchWindow");
        run.Holds("focus on an unmatched selector leaves focus where it is", () => run.Active() == VerifyRun.Address(left), 500);
    });
}
