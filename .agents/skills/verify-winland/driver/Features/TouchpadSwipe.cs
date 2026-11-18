
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "touchpad-swipe")]
[TestClass]
public class TouchpadSwipe
{

    public TestContext TestContext { get; set; } = null!;
        private const string Config =
        """
        gesture = 3, horizontal, workspace
        gesture = 4, vertical, workspace
        gesture = 3, diagonal, workspace
        gesture = 3, horizontal, workspace, mod:SUPER

        """;

    [VerifyFact]
    public void LoadsSwipeGesturesAndRejectsTheOnesItCannotDo() => VerifyRun.Drive(TestContext, Config, run =>
    {
        var errors = run.Ask("get", "errors").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        run.Say("get errors: " + string.Join(" | ", errors));
        Assert.AreEqual(2, errors.Length);
        Assert.Contains(line => line.Contains("winland.conf:10: Invalid direction: diagonal.", StringComparison.Ordinal), errors);
        Assert.Contains(line => line.Contains("winland.conf:11: A gesture is <fingers>, horizontal or vertical, workspace.", StringComparison.Ordinal), errors);
        run.Say("saw: winland runs with the two valid gestures and warns about the two it cannot do");
    });
}
