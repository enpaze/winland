namespace Winland.Application.Tests;

[TestClass]
public class HideTaskbarOptionTests
{
    [TestMethod]
    public void TheTaskbarStaysVisibleUnlessTheConfigHidesIt()
    {
        var core = new WindowManager();

        Assert.IsFalse(core.HideTaskbar);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("yes")]
    [DataRow("on")]
    [DataRow("1")]
    public void HideTaskbarIsHonored(string value)
    {
        var text = $"hide_taskbar = {value}\n";
        var document = ConfigParser.Parse(text);
        var core = new WindowManager(document);

        Assert.IsEmpty(document.Warnings);
        Assert.IsTrue(core.HideTaskbar);
    }

    [TestMethod]
    public void AReloadAppliesTheOption()
    {
        var text = "hide_taskbar = false\n";
        var core = new WindowManager(ConfigParser.Parse("hide_taskbar = true\n"))
        {
            ReadConfig = () => new ConfigRead(ConfigParser.Parse(text)),
        };

        Assert.IsTrue(core.HideTaskbar);

        Assert.AreEqual("ok", core.Execute("reload").Text);
        Assert.IsFalse(core.HideTaskbar);
    }
}
