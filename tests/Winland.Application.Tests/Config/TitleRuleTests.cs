namespace Winland.Application.Tests;

[TestClass]
public class TitleRuleTests
{
    [TestMethod]
    [DataRow("windowrule = float, title:Settings", true)]
    [DataRow("windowrule = float, class:kitty, title:negative:Settings", true)]
    [DataRow("windowrule = float, initial_title:Settings", false)]
    [DataRow("windowrule = float, class:kitty", false)]
    [DataRow("gaps_in = 0", false)]
    public void OnlyARuleOnTitleReadsTitles(string line, bool expected)
    {
        var core = new WindowManager(ConfigParser.Parse(line + "\n"));

        Assert.AreEqual(expected, core.RulesMatchTitles);
    }

    [TestMethod]
    public void ReloadingTheConfigChangesWhatTheRulesReadAndTheirRevision()
    {
        var core = new WindowManager();
        Assert.IsFalse(core.RulesMatchTitles);
        var before = core.RuleRevision;

        core.Handle(new Configure(ConfigParser.Parse("windowrule = ignore, title:Player\n")));
        var reloaded = core.RuleRevision;

        Assert.IsTrue(core.RulesMatchTitles);
        Assert.AreNotEqual(before, reloaded);

        core.Handle(new Configure(ConfigParser.Parse("windowrule = ignore, class:kitty\n")));

        Assert.IsFalse(core.RulesMatchTitles);
        Assert.AreNotEqual(reloaded, core.RuleRevision);
    }
}
