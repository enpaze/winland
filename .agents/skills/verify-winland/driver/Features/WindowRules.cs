using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "window-rules")]
public class WindowRules(ITestOutputHelper output)
{
    private const string Rules =
        """
        windowrule = float, class:WinlandVerifyWindow, title:WinlandRule Float
        windowrule = workspace 4, class:WinlandVerifyWindow, title:WinlandRule Away
        windowrule = tile, class:WinlandVerifyWindow, title:WinlandRule Faint
        windowrule = opacity 0.5, class:WinlandVerifyWindow, title:WinlandRule Faint

        """;

    [VerifyFact]
    public void GivesWindowsWhatTheirRulesSay() => VerifyRun.Drive(output, Rules, run =>
    {
        var ignored = run.OpenTitled("WinlandIgnored");
        var floating = run.OpenTitled("WinlandRule Float", 300, 200);
        var faint = run.OpenTitled("WinlandRule Faint");

        run.Until("the float rule floats its window", () => run.Client(floating)?.GetProperty("floating").GetBoolean() == true);
        run.Until("the opacity rule makes its window half transparent", () => Desktop.Alpha(faint) is >= 120 and <= 135);
        run.Holds("a window no rule adopts stays unmanaged", () => run.Client(ignored) is null, 500);
        Assert.Equal(1, run.ClientOf(faint).GetProperty("workspace").GetProperty("id").GetInt32());

        var away = run.OpenTitled("WinlandRule Away");
        run.Until("the workspace rule puts its window on workspace 4", () =>
            run.Client(away)?.GetProperty("workspace").GetProperty("id").GetInt32() == 4);

        var inspected = run.Json("inspect", "address:" + VerifyRun.Address(floating));
        Assert.Equal("found", inspected.GetProperty("status").GetString());
        Assert.True(inspected.GetProperty("floating").GetBoolean());
        Assert.Equal("rule", inspected.GetProperty("floatingReason").GetString());
        run.Say("saw: inspect explains the float came from a rule");
    });
}
