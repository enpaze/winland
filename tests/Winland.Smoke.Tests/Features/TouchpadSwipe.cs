using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "touchpad-swipe")]
public class TouchpadSwipe(ITestOutputHelper output)
{
    private const string Config =
        """
        gesture = 3, horizontal, workspace
        gesture = 4, vertical, workspace
        gesture = 3, diagonal, workspace
        gesture = 3, horizontal, workspace, mod:SUPER

        """;

    [SmokeFact]
    public void LoadsSwipeGesturesAndRejectsTheOnesItCannotDo() => SmokeRun.Drive(output, Config, run =>
    {
        var errors = run.Ask("configerrors").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        run.Say("configerrors: " + string.Join(" | ", errors));
        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, line => line.Contains("winland.conf:10: Invalid direction: diagonal.", StringComparison.Ordinal));
        Assert.Contains(errors, line => line.Contains("winland.conf:11: A gesture is <fingers>, horizontal or vertical, workspace.", StringComparison.Ordinal));
        run.Say("saw: winland runs with the two valid gestures and warns about the two it cannot do");
    });
}
