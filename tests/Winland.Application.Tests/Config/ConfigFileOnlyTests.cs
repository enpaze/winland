namespace Winland.Application.Tests;

[TestClass]
public class ConfigFileOnlyTests
{
    [TestMethod]
    [DataRow("keyword hide_taskbar true")]
    [DataRow("keyword gaps_out 0")]
    [DataRow("keyword windowrule float, class:b")]
    [DataRow("getoption gaps_out")]
    [DataRow("setprop address:0x1 border_size 9")]
    [DataRow("getprop address:0x1 border_size")]
    [DataRow("[[BATCH]]keyword gaps_out 0 ; keyword border_size 9")]
    [DataRow("[[BATCH]]j/get windows")]
    [DataRow("[[BATCH]]/reload")]
    public void NoRequestReadsOrChangesASetting(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        var core = desk.WindowManager;
        var applied = (core.HideTaskbar, core.RuleRevision);
        var windows = desk.Send("j/get windows");
        var placed = desk.PlaceOf("a");

        Assert.StartsWith("error:", desk.Send(line), StringComparison.Ordinal);

        desk.Spawn("b");
        Assert.IsFalse(TestDesktop.Floating(desk.Client("b")));
        desk.Close("b");
        Assert.AreEqual(applied, (core.HideTaskbar, core.RuleRevision));
        Assert.AreEqual(placed, desk.PlaceOf("a"));
        Assert.AreEqual(windows, desk.Send("j/get windows"));
    }

    [TestMethod]
    public void AReloadGivesEveryWindowTheRuleEffectsOfTheFileAsItIsNow()
    {
        const string options = "gaps_in = 0\ngaps_out = 0\nborder_size = 2\n";
        var rules = "windowrule = opacity 0.5, class:b\n";
        var core = new WindowManager(ConfigParser.Parse(options + rules));
        core.ReadConfig = () => new ConfigRead(ConfigParser.Parse(options + rules));
        var area = new Rectangle(0, 0, 1000, 800);
        core.Handle(new WindowAppeared(new WindowId(1), area) { Traits = WindowTraits.Normal with { Class = "a" } });
        var opened = core.Handle(new WindowAppeared(new WindowId(2), area) { Traits = WindowTraits.Normal with { Class = "b" } });

        rules = "windowrule = opacity 0.5, class:a\n";
        var reloaded = core.Execute("reload");

        Assert.AreEqual((255, 128), (AlphaOf(opened, 1), AlphaOf(opened, 2)));
        Assert.AreEqual("ok", reloaded.Text);
        Assert.AreEqual((128, 255), (AlphaOf(reloaded.Effect, 1), AlphaOf(reloaded.Effect, 2)));
    }

    private static int AlphaOf(Outcome outcome, long window) =>
        Assert.ContainsSingle(decoration => decoration.Client == new ClientId(window), outcome.Decorations).Alpha;
}
