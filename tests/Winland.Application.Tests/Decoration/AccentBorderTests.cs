namespace Winland.Application.Tests;

[TestClass]
public class AccentBorderTests
{
    private static readonly Rectangle Area = new(0, 0, 800, 600);

    private static readonly BorderColor Accent = new(0xFF, 0x11, 0x22, 0x33);

    private static readonly BorderColor Chosen = new(0xFF, 0xAA, 0xBB, 0xCC);

    [TestMethod]
    public void AnEmptyConfigFollowsTheAccentAndASetColorOverridesIt()
    {
        var core = Configured("");
        var opened = core.Handle(Appeared(1, Area));
        Assert.AreEqual(new BorderColor(0xFF, 0xFF, 0xFF, 0xFF), Border(opened, 1));

        var followed = core.Handle(new AccentChanged(Accent));
        Assert.AreEqual(Accent, Border(followed, 1));

        var changed = core.Handle(new AccentChanged(new BorderColor(0xFF, 0x01, 0x02, 0x03)));
        Assert.AreEqual(new BorderColor(0xFF, 0x01, 0x02, 0x03), Border(changed, 1));

        var pinned = core.Handle(new Configure(ConfigParser.Parse("general:col.active_border = rgba(aabbccff)\n")));
        Assert.AreEqual(Chosen, Border(pinned, 1));
        var ignored = core.Handle(new AccentChanged(Accent));
        Assert.AreEqual(Chosen, Border(ignored, 1));

        var restored = core.Handle(new Configure(ConfigParser.Parse("")));
        Assert.AreEqual(Accent, Border(restored, 1));
    }

    [TestMethod]
    public void AGradientDoesNotOverrideTheAccent()
    {
        var core = Configured("general:col.active_border = rgba(ff00ffff) 90deg\n");
        core.Handle(Appeared(1, Area));

        var followed = core.Handle(new AccentChanged(Accent));

        Assert.AreEqual(Accent, Border(followed, 1));
        Assert.IsFalse(DecorationOptions.From(ConfigParser.Parse("general:col.active_border = rgba(ff00ffff) 90deg\n")).ActiveBorderSet);
    }

    [TestMethod]
    public void AnInactiveClientKeepsTheDimBorder()
    {
        var core = Configured("");
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        var followed = core.Handle(new AccentChanged(Accent));

        var borders = new[] { Border(followed, 1), Border(followed, 2) };
        Assert.Contains(Accent, borders);
        Assert.Contains(new BorderColor(0xFF, 0x40, 0x40, 0x40), borders);
    }

    private static readonly HighContrast Contrast = new(
        new BorderColor(0xFF, 0x1A, 0xEB, 0xFF), new BorderColor(0xFF, 0xFF, 0xFF, 0xFF), new BorderColor(0xFF, 0x3F, 0xF2, 0x3F));

    [TestMethod]
    public void HighContrastColorsReplaceTheUnsetBorderAndTabColorsUntilItEnds()
    {
        var core = Configured("");
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        core.Execute("group");

        var contrasted = core.Handle(new AccentChanged(Accent) { Contrast = Contrast });
        var borders = new[] { Border(contrasted, 1), Border(contrasted, 2) };
        Assert.Contains(Contrast.Highlight, borders);
        Assert.Contains(Contrast.GrayText, borders);
        var style = Assert.ContainsSingle(core.Execute("resize").Effect.GroupBars).Style;
        Assert.AreEqual((Contrast.Text, Contrast.Highlight, Contrast.GrayText), (style.TextColor, style.Active, style.Inactive));

        var ended = core.Handle(new AccentChanged(Accent));
        Assert.Contains(new BorderColor(0xFF, 0x40, 0x40, 0x40), new[] { Border(ended, 1), Border(ended, 2) });
    }

    [TestMethod]
    public void AConfiguredColorWinsOverHighContrast()
    {
        var core = Configured("general:col.inactive_border = rgba(aabbccff)\ngroup:groupbar:text_color = rgba(aabbccff)\n");
        core.Handle(Appeared(1, Area));
        core.Handle(Appeared(2, Area));
        core.Execute("group");

        var contrasted = core.Handle(new AccentChanged(Accent) { Contrast = Contrast });

        Assert.Contains(Chosen, new[] { Border(contrasted, 1), Border(contrasted, 2) });
        Assert.AreEqual(Chosen, Assert.ContainsSingle(core.Execute("resize").Effect.GroupBars).Style.TextColor);
    }

    [TestMethod]
    public void TheTextSizeGrowsTheTabFontAndTitleRow()
    {
        var core = Configured("");
        core.Handle(Appeared(1, Area));
        core.Execute("group");
        var plain = Assert.ContainsSingle(core.Execute("resize").Effect.GroupBars).Style;

        core.Handle(new AccentChanged(Accent) { TextScale = 1.5 });
        var larger = Assert.ContainsSingle(core.Execute("resize").Effect.GroupBars).Style;

        Assert.AreEqual((12, 21), (larger.FontSize, larger.Height));
        Assert.AreEqual(plain.IndicatorHeight, larger.IndicatorHeight);
    }

    private static BorderColor Border(Outcome outcome, long window)
    {
        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(window), outcome.State.Clients);
        return Assert.ContainsSingle(decoration => decoration.Client == client.Id, outcome.Decorations).Border;
    }
}
