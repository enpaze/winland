namespace Winland.Application.Tests;

[TestClass]
public class OptionSetTests
{
    private static readonly string[] Kept =
    [
        "active_opacity",
        "animations",
        "backdrop",
        "border_color",
        "border_color_inactive",
        "border_size",
        "column_width",
        "focus_follows_mouse",
        "gaps_in",
        "gaps_out",
        "hide_cursor_while_typing",
        "hide_taskbar",
        "inactive_opacity",
        "layout",
        "log_file",
        "rounding",
        "tab_color",
        "tab_color_inactive",
        "tab_font",
        "tab_font_size",
        "tab_height",
        "tab_text_color",
        "warp_cursor",
    ];

    [TestMethod]
    public void TheOptionsAreTheOnesTheReferenceLists()
    {
        Assert.AreSequenceEqual(Kept, OptionTable.Names.Order(StringComparer.Ordinal));
    }

    [TestMethod]
    public void AnythingElseWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse("gaps_in = 3\nmisc:no_such_option = 1\n");

        Assert.AreEqual("Unsupported keyword 'misc:no_such_option'.", Assert.ContainsSingle(document.Warnings).Message);
        Assert.AreSequenceEqual([new Setting("gaps_in", "3")], document.Settings);
    }
}
