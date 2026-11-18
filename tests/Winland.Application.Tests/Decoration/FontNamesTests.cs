namespace Winland.Application.Tests;

[TestClass]
public class FontNamesTests
{
    [TestMethod]
    [DataRow("Sans", "Segoe UI")]
    [DataRow("sans-serif", "Segoe UI")]
    [DataRow("", "Segoe UI")]
    [DataRow(null, "Segoe UI")]
    [DataRow("Monospace", "Consolas")]
    [DataRow("serif", "Cambria")]
    [DataRow(" Fira Code ", "Fira Code")]
    public void GenericNamesMapToWindowsFonts(string? family, string font) =>
        Assert.AreEqual(font, FontNames.Windows(family));

    [TestMethod]
    public void AnUnnamedTabBarUsesTheWindowsInterfaceFont()
    {
        var core = new WindowManager(ConfigParser.Parse(""));
        core.Handle(new MonitorsChanged([new Monitor("DISPLAY1", "", 0, 0, 1920, 1080, 1, true, new Rectangle(0, 0, 1920, 1040))]));
        core.Handle(new WindowAppeared(new WindowId(1), new Rectangle(0, 0, 800, 600)));
        core.Execute("dispatch togglegroup");

        Assert.AreEqual("Segoe UI", Assert.ContainsSingle(core.Execute("dispatch resizewindow").Effect.GroupBars).Style.FontFamily);

        core.Handle(new Configure(ConfigParser.Parse("group:groupbar:font_family = Fira Code\n")));
        Assert.AreEqual("Fira Code", Assert.ContainsSingle(core.Execute("dispatch resizewindow").Effect.GroupBars).Style.FontFamily);
    }
}
