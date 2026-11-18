using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class ReclassTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ANewClassIsKeptAndTellsNoEvent()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(0x2a), Area) { Traits = WindowTraits.Normal with { Class = "kitty", Title = "old" } });
        core.Handle(new WindowAppeared(new WindowId(0x2b), Area) { Traits = WindowTraits.Normal with { Class = "foot", Title = "other" } });

        var outcome = core.Handle(new WindowClassChanged(new WindowId(0x2a), "new"));

        Assert.AreEqual("new", Assert.ContainsSingle(client => client.Window == new WindowId(0x2a), outcome.State.Clients).Class);
        Assert.IsEmpty(outcome.Events);
    }

    [TestMethod]
    public void TheFocusedWindowsNewClassTellsNoFocusEvent()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(0x2a), Area) { Traits = WindowTraits.Normal with { Class = "kitty", Title = "old" } });

        var outcome = core.Handle(new WindowClassChanged(new WindowId(0x2a), "new"));

        Assert.AreEqual("new", Assert.ContainsSingle(outcome.State.Clients).Class);
        Assert.IsEmpty(outcome.Events);
    }

    [TestMethod]
    public void TheSameClassTellsNothing()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Class = "same" } });

        Assert.IsEmpty(core.Handle(new WindowClassChanged(new WindowId(1), "same")).Events);
    }

    [TestMethod]
    public void ARuleOnTheClassAppliesOnceTheClassMatches()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = opacity 0.5, class:*Video*\n"));
        var opened = core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Class = "Browser" } });
        Assert.AreEqual(255, Assert.ContainsSingle(opened.Decorations).Alpha);

        var reclassed = core.Handle(new WindowClassChanged(new WindowId(1), "A Video - Browser"));

        Assert.AreEqual((double)127, (double)(Assert.ContainsSingle(reclassed.Decorations).Alpha), 1d);

        var restored = core.Handle(new WindowClassChanged(new WindowId(1), "Browser"));

        Assert.AreEqual(255, Assert.ContainsSingle(restored.Decorations).Alpha);
    }

    [TestMethod]
    public void TheInitialClassStaysWhileTheClassChanges()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(0x2a), Area) { Traits = WindowTraits.Normal with { Class = "kitty", Title = "old" } });

        var outcome = core.Handle(new WindowClassChanged(new WindowId(0x2a), "new"));

        var client = Assert.ContainsSingle(outcome.State.Clients);
        Assert.AreEqual("new", client.Class);
        Assert.AreEqual("kitty", client.InitialClass);
        using var listed = JsonDocument.Parse(core.Execute("j/get windows").Text);
        var json = Assert.ContainsSingle(listed.RootElement.EnumerateArray());
        Assert.AreEqual("new", json.GetProperty("class").GetString());
        Assert.AreEqual("kitty", json.GetProperty("initialClass").GetString());
        using var active = JsonDocument.Parse(core.Execute("j/get window").Text);
        Assert.AreEqual("new", active.RootElement.GetProperty("class").GetString());
        Assert.AreEqual("kitty", active.RootElement.GetProperty("initialClass").GetString());
    }

    [TestMethod]
    public void AGroupedWindowsClassFollowsTheWindow()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Class = "one" } });
        core.Execute("togglegroup");
        core.Handle(new WindowAppeared(new WindowId(2), Area) { Traits = WindowTraits.Normal with { Class = "two" } });

        var reclassed = core.Handle(new WindowClassChanged(new WindowId(1), "renamed"));

        var hidden = Assert.ContainsSingle(client => client.Window == new WindowId(1), reclassed.State.Clients);
        Assert.AreEqual("renamed", hidden.Class);
        Assert.AreEqual("one", hidden.InitialClass);
    }

    [TestMethod]
    public void AClassForAWindowThatIsNotAClientTellsNothing()
    {
        var core = new WindowManager();

        Assert.IsEmpty(core.Handle(new WindowClassChanged(new WindowId(1), "class")).Events);
    }
}
