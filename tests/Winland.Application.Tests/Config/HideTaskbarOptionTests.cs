namespace Winland.Application.Tests;

public class HideTaskbarOptionTests
{
    [Fact]
    public void TheTaskbarStaysVisibleUnlessTheConfigHidesIt()
    {
        var core = new WindowManager();

        Assert.False(core.HideTaskbar);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("1")]
    public void HideTaskbarIsHonored(string value)
    {
        var text = $"general:hide_taskbar = {value}\n";
        var document = ConfigParser.Parse(text);
        var core = new WindowManager(document);

        Assert.Empty(document.Warnings);
        Assert.True(core.HideTaskbar);
    }

    [Fact]
    public void AReloadAppliesTheOption()
    {
        var text = "general:hide_taskbar = false\n";
        var core = new WindowManager(ConfigParser.Parse("general:hide_taskbar = true\n"))
        {
            ReadConfig = () => new ConfigRead(ConfigParser.Parse(text)),
        };

        Assert.True(core.HideTaskbar);

        Assert.Equal("ok", core.Execute("reload").Text);
        Assert.False(core.HideTaskbar);
    }
}
