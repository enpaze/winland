namespace Winland.Application.Tests;

[TestClass]
public class OptionSetTests
{
    private static readonly string[] Kept =
    [
        "animations:enabled",
        "cursor:hide_on_key_press",
        "cursor:no_warps",
        "debug:disable_logs",
        "decoration:active_opacity",
        "decoration:blur:enabled",
        "decoration:inactive_opacity",
        "decoration:rounding",
        "general:border_size",
        "general:col.active_border",
        "general:col.inactive_border",
        "general:gaps_in",
        "general:gaps_out",
        "general:hide_taskbar",
        "general:layout",
        "group:groupbar:col.active",
        "group:groupbar:col.inactive",
        "group:groupbar:font_family",
        "group:groupbar:font_size",
        "group:groupbar:height",
        "group:groupbar:text_color",
        "input:follow_mouse",
        "scrolling:column_width",
    ];

    [TestMethod]
    public void TheOptionsAreTheOnesTheReferenceLists()
    {
        Assert.AreSequenceEqual(Kept, OptionTable.Names.Order(StringComparer.Ordinal));
    }

    [TestMethod]
    public void AnythingElseWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse("general:gaps_in = 3\nmisc:no_such_option = 1\n");

        Assert.AreEqual("Unsupported keyword 'misc:no_such_option'.", Assert.ContainsSingle(document.Warnings).Message);
        Assert.AreSequenceEqual([new Setting("general:gaps_in", "3")], document.Settings);
    }
}
