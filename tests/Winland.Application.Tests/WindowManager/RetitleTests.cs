using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class RetitleTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ANewTitleIsKeptAndToldAsAWindowTitleEvent()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(0x2a), Area) { Traits = WindowTraits.Normal with { Class = "kitty", Title = "old" } });
        core.Handle(new WindowAppeared(new WindowId(0x2b), Area) { Traits = WindowTraits.Normal with { Class = "foot", Title = "other" } });

        var outcome = core.Handle(new WindowTitleChanged(new WindowId(0x2a), "new"));

        Assert.AreEqual("new", Assert.ContainsSingle(client => client.Window == new WindowId(0x2a), outcome.State.Clients).Title);
        Assert.AreSequenceEqual(["windowtitle>>2a,new"], CommandProtocol.FormatStream(outcome.Events, outcome.State));
    }

    [TestMethod]
    public void TheFocusedWindowsNewTitleIsToldOnceAndLeavesFocusAlone()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(0x2a), Area) { Traits = WindowTraits.Normal with { Class = "kitty", Title = "old" } });

        var outcome = core.Handle(new WindowTitleChanged(new WindowId(0x2a), "new"));

        Assert.AreSequenceEqual(
            ["windowtitle>>2a,new"],
            CommandProtocol.FormatStream(outcome.Events, outcome.State));
    }

    [TestMethod]
    public void TheSameTitleTellsNothing()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Title = "same" } });

        Assert.IsEmpty(core.Handle(new WindowTitleChanged(new WindowId(1), "same")).Events);
    }

    [TestMethod]
    public void ARuleOnTheTitleAppliesOnceTheTitleMatches()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = opacity 0.5, title:*Video*\n"));
        var opened = core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Title = "Browser" } });
        Assert.AreEqual(255, Assert.ContainsSingle(opened.Decorations).Alpha);

        var retitled = core.Handle(new WindowTitleChanged(new WindowId(1), "A Video - Browser"));

        Assert.AreEqual((double)127, (double)(Assert.ContainsSingle(retitled.Decorations).Alpha), 1d);

        var restored = core.Handle(new WindowTitleChanged(new WindowId(1), "Browser"));

        Assert.AreEqual(255, Assert.ContainsSingle(restored.Decorations).Alpha);
    }

    [TestMethod]
    public void TheInitialTitleStaysWhileTheTitleChanges()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(0x2a), Area) { Traits = WindowTraits.Normal with { Class = "kitty", Title = "old" } });

        var outcome = core.Handle(new WindowTitleChanged(new WindowId(0x2a), "new"));

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual("new", client.Title);
        Assert.AreEqual("old", client.InitialTitle);
        using var listed = JsonDocument.Parse(core.Execute("j/get windows").Text);
        var json = Assert.ContainsSingle(listed.RootElement.EnumerateArray());
        Assert.AreEqual("new", json.GetProperty("title").GetString());
        Assert.AreEqual("old", json.GetProperty("initialTitle").GetString());
        using var active = JsonDocument.Parse(core.Execute("j/get window").Text);
        Assert.AreEqual("new", active.RootElement.GetProperty("title").GetString());
        Assert.AreEqual("old", active.RootElement.GetProperty("initialTitle").GetString());
    }

    [TestMethod]
    public void AGroupTabsTitleFollowsTheWindow()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Title = "one" } });
        core.Execute("group");
        core.Handle(new WindowAppeared(new WindowId(2), Area) { Traits = WindowTraits.Normal with { Title = "two" } });

        var retitled = core.Handle(new WindowTitleChanged(new WindowId(1), "renamed"));

        Assert.AreSequenceEqual(["renamed", "two"], Assert.ContainsSingle(retitled.GroupBars).Tabs.Select(tab => tab.Title));
        var hidden = Assert.ContainsSingle(client => client.Window == new WindowId(1), retitled.State.Clients);
        Assert.AreEqual("renamed", hidden.Title);
        Assert.AreEqual("one", hidden.InitialTitle);
    }

    [TestMethod]
    public void ATitleForAWindowThatIsNotAClientTellsNothing()
    {
        var core = new WindowManager();

        Assert.IsEmpty(core.Handle(new WindowTitleChanged(new WindowId(1), "title")).Events);
    }
}
