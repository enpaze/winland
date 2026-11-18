namespace Winland.Application.Tests;

[TestClass]
public class CursorInputTests
{
    [TestMethod]
    public void DocumentedCursorFollowsFocusAndHidesOnTyping()
    {
        var options = CursorOptions.From(ConfigParser.Parse(""));

        Assert.IsFalse(options.NoWarps);
        Assert.IsTrue(options.HideOnKeyPress);
    }

    [TestMethod]
    public void CursorBlockIsHonored()
    {
        var document = ConfigParser.Parse("""
            cursor {
                no_warps = true
                hide_on_key_press = on
            }
            """);

        Assert.IsEmpty(document.Warnings);
        var options = CursorOptions.From(document);
        Assert.IsTrue(options.NoWarps);
        Assert.IsTrue(options.HideOnKeyPress);
    }

    [TestMethod]
    public void ACutCursorKeyWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse("cursor:persistent_warps = true\n");

        Assert.IsEmpty(document.Settings);
        Assert.AreEqual(new ConfigWarning(1, "Unsupported keyword 'cursor:persistent_warps'."), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void ASystemInputKeyWarnsAndIsNotApplied()
    {
        var document = ConfigParser.Parse("""
            input:follow_mouse = 1
            input:repeat_rate = 25
            input:sensitivity = 0
            input:left_handed = true
            """);

        Assert.AreSequenceEqual(
            [
                new ConfigWarning(2, "Unsupported keyword 'input:repeat_rate'."),
                new ConfigWarning(3, "Unsupported keyword 'input:sensitivity'."),
                new ConfigWarning(4, "Unsupported keyword 'input:left_handed'."),
            ],
            document.Warnings);
        Assert.AreSequenceEqual([new Setting("input:follow_mouse", "1")], document.Settings);
    }

    [TestMethod]
    public void FollowMouseIsReadAndATabletKeyWarns()
    {
        var document = ConfigParser.Parse("input:follow_mouse = true\ninput:tablet:enabled = true\n");

        Assert.AreSequenceEqual([new Setting("input:follow_mouse", "true")], document.Settings);
        Assert.AreEqual(new ConfigWarning(2, "Unsupported keyword 'input:tablet:enabled'."), Assert.ContainsSingle(document.Warnings));
        Assert.IsTrue(InputOptions.From(document).FollowMouse);
    }

    [TestMethod]
    public void WheelDirectionIsNotReversed()
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse("bind = mouse_down, workspace 2\n")));
        core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600)));

        var outcome = core.Handle(new WheelTurned("mouse_up", 1_000));

        Assert.AreEqual(WorkspaceId.Initial, outcome.State.ActiveWorkspace);
    }

    [TestMethod]
    public void ConfigurePublishesTheCursorAndSkipsASystemInputKey()
    {
        var document = ConfigParser.Parse("input:repeat_rate = 30\ninput:scroll_factor = 2\n");
        var outcome = new WindowManager().Handle(new Configure(document));

        Assert.AreSequenceEqual(
            [
                new ConfigWarning(1, "Unsupported keyword 'input:repeat_rate'."),
                new ConfigWarning(2, "Unsupported keyword 'input:scroll_factor'."),
            ],
            document.Warnings);
        Assert.AreEqual(new CursorBehavior(true), outcome.Cursor);
    }

    [TestMethod]
    public void ClientsRoundTripAndAnOldInputObjectIsIgnored()
    {
        var snapshot = new RecoverySnapshot(
        [
            new RecoveredClient(new WindowId(7), WorkspaceId.Initial, MonitorId.Primary, false, "CabinetWClass", "Home", 9)
            {
                MonitorIdentity = @"\\.\DISPLAY1",
            },
        ]);

        Assert.IsTrue(RecoverySnapshot.TryParse(snapshot.ToJson(), out var parsed));
        Assert.AreSequenceEqual(snapshot.Clients, parsed.Clients);
        Assert.DoesNotContain("input", snapshot.ToJson(), StringComparison.Ordinal);

        const string Older = """
            {"clients":[{"window":7,"workspace":"1","monitor":"\\\\.\\DISPLAY1","hidden":false,"class":"CabinetWClass","title":"Home","process":9}],"input":{"keyboardDelay":1},"mirrors":[],"vrr":{},"colors":[]}
            """;
        Assert.IsTrue(RecoverySnapshot.TryParse(Older, out var legacy));
        Assert.AreSequenceEqual(snapshot.Clients, legacy.Clients);
    }
}
