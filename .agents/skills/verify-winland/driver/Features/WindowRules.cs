
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "window-rules")]
[TestClass]
public class WindowRules
{

    public TestContext TestContext { get; set; } = null!;
        private const string Rules =
        """
        windowrule = float, class:WinlandVerifyWindow, title:WinlandRule Float
        windowrule = workspace 4, class:WinlandVerifyWindow, title:WinlandRule Away
        windowrule = tile; no_blur; opacity 0.5, class:WinlandVerifyWindow, title:WinlandRule Faint

        """;

    [VerifyFact]
    public void GivesWindowsWhatTheirRulesSay() => VerifyRun.Drive(TestContext, Rules, run =>
    {
        var ignored = run.OpenTitled("WinlandIgnored");
        var floating = run.OpenTitled("WinlandRule Float", 300, 200);
        var faint = run.OpenTitled("WinlandRule Faint");

        run.Until("the float rule floats its window", () => run.Client(floating)?.GetProperty("floating").GetBoolean() == true);
        run.Until("the opacity rule makes its window half transparent", () => Desktop.Alpha(faint) is >= 120 and <= 135);
        run.Holds("a window no rule adopts stays unmanaged", () => run.Client(ignored) is null, 500);
        Assert.AreEqual(1, run.ClientOf(faint).GetProperty("workspace").GetProperty("id").GetInt32());
        Assert.Contains("Unsupported window rule 'no_blur'.", run.Ask("get", "errors"), StringComparison.Ordinal);
        run.Say("saw: one rule tiled and faded its window past the effect it warned about");

        var away = run.OpenTitled("WinlandRule Away");
        run.Until("the workspace rule puts its window on workspace 4", () =>
            run.Client(away)?.GetProperty("workspace").GetProperty("id").GetInt32() == 4);

        var inspected = run.Json("inspect", "address:" + VerifyRun.Address(floating));
        Assert.AreEqual("found", inspected.GetProperty("status").GetString());
        Assert.IsTrue(inspected.GetProperty("floating").GetBoolean());
        Assert.AreEqual("rule", inspected.GetProperty("floatingReason").GetString());
        run.Say("saw: inspect explains the float came from a rule");
    });
}
