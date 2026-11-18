namespace Winland.Application.Tests;

[TestClass]
public class SelectLayoutTests
{
    [TestMethod]
    [DataRow("dwindle", "scrolling")]
    [DataRow("dwindle", "floating")]
    [DataRow("scrolling", "dwindle")]
    [DataRow("floating", "scrolling")]
    public void LayoutSelectsTheNamedLayout(string from, string to)
    {
        var core = Configured($"general:layout = {from}\n");
        core.Handle(Appeared(1));

        var selected = core.Handle(new Dispatch("layout", to));

        Assert.IsNull(selected.Error);
        Assert.AreEqual(to, Layout(selected));
        Assert.ContainsSingle(selected.State.Clients);
    }

    [TestMethod]
    [DataRow("master")]
    [DataRow("")]
    [DataRow("scrolling dwindle")]
    public void ANameThatIsNoLayoutErrorsAndChangesNothing(string name)
    {
        var core = Configured("general:layout = scrolling\n");
        core.Handle(Appeared(1));

        var refused = core.Handle(new Dispatch("layout", name));

        Assert.AreEqual($"error: unknown layout '{name}'", refused.Error);
        Assert.IsEmpty(refused.Placements);
        Assert.AreEqual("scrolling", Layout(refused));
    }

    [TestMethod]
    public void ATypoSuggestsTheLayout()
    {
        var core = new WindowManager();

        var refused = core.Handle(new Dispatch("layout", "scroling"));

        Assert.AreEqual("error: unknown layout 'scroling'. Did you mean 'scrolling'?", refused.Error);
    }

    [TestMethod]
    public void SelectingTheRunningScrollingLayoutKeepsItsColumnWidths()
    {
        var kept = ScrollingWithANarrowColumn();
        var untouched = ScrollingWithANarrowColumn();

        var selected = kept.Handle(new Dispatch("layout", "scrolling"));
        var after = kept.Handle(Appeared(3));
        var expected = untouched.Handle(Appeared(3));

        Assert.IsNull(selected.Error);
        Assert.IsEmpty(selected.Placements);
        Assert.AreEqual(BoundsOf(expected, 1), BoundsOf(after, 1));
        Assert.AreEqual(BoundsOf(expected, 2), BoundsOf(after, 2));
    }

    [TestMethod]
    public void SelectingTheRunningDwindleLayoutKeepsItsSplits()
    {
        var kept = DwindleWithAToggledSplit();
        var untouched = DwindleWithAToggledSplit();

        var selected = kept.Handle(new Dispatch("layout", "DWINDLE"));
        var after = kept.Handle(Appeared(3));
        var expected = untouched.Handle(Appeared(3));

        Assert.IsNull(selected.Error);
        Assert.IsEmpty(selected.Placements);
        Assert.AreEqual(BoundsOf(expected, 1), BoundsOf(after, 1));
        Assert.AreEqual(BoundsOf(expected, 2), BoundsOf(after, 2));
        Assert.AreEqual(BoundsOf(expected, 3), BoundsOf(after, 3));
    }

    [TestMethod]
    public void OnlyTheShownWorkspaceChanges()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(Appeared(2));

        var selected = core.Handle(new Dispatch("layout", "scrolling"));

        Assert.AreEqual("scrolling", LayoutOf(selected, "2"));
        Assert.AreEqual("dwindle", LayoutOf(selected, "1"));
    }

    [TestMethod]
    public void AnOpenSpecialWorkspaceIsTheOneThatChanges()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1));
        core.Handle(new Dispatch("movetoworkspace", "special:scratch"));

        var selected = core.Handle(new Dispatch("layout", "floating"));

        Assert.AreEqual("floating", LayoutOf(selected, "special:scratch"));
        Assert.AreEqual("dwindle", LayoutOf(selected, "1"));
    }

    [TestMethod]
    public void AGroupKeepsEveryMemberThroughEachLayout()
    {
        var desk = new TestDesktop();
        desk.Spawn("c");
        desk.Spawn("a");
        desk.Ok("togglegroup");
        desk.Spawn("b");

        foreach (var layout in new[] { "scrolling", "floating", "dwindle" })
        {
            desk.Ok("layout", layout);

            Assert.AreEqual(3, desk.Clients().Length);
            Assert.AreEqual(2, Grouped(desk.Client("a")));
            Assert.AreEqual(2, Grouped(desk.Client("b")));
            Assert.AreEqual(0, Grouped(desk.Client("c")));
            desk.Ok("changegroupactive", "f");
        }
    }

    [TestMethod]
    public void EachWindowKeepsItsStateThroughEachLayout()
    {
        var desk = new TestDesktop();
        desk.Spawn("tiled", "Tiled title");
        desk.Spawn("pinned");
        desk.Ok("togglefloating");
        desk.Ok("pin");
        desk.Spawn("small");
        desk.Handle(new WindowMinimized(desk.Window("small")));
        desk.Ok("focuswindow", "class:tiled");

        foreach (var layout in new[] { "scrolling", "floating", "dwindle" })
        {
            var selected = desk.Handle(new Dispatch("layout", layout));
            var tiled = Client(selected, desk.Window("tiled").Value);
            var pinned = Client(selected, desk.Window("pinned").Value);

            Assert.IsNull(selected.Error);
            Assert.AreEqual(3, selected.State.Clients.Count);
            Assert.AreEqual("tiled", desk.ActiveClass());
            Assert.AreEqual(("tiled", "Tiled title"), (tiled.Class, tiled.Title));
            Assert.AreEqual(layout == "floating", tiled.Floating);
            Assert.IsTrue(pinned.Floating);
            Assert.IsTrue(pinned.Pinned);
            Assert.IsTrue(Client(selected, desk.Window("small").Value).Minimized);
        }
    }

    [TestMethod]
    [DataRow("scrolling")]
    [DataRow("floating")]
    public void AFullscreenWindowStaysFullscreen(string layout)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("fullscreen", "0");

        desk.Ok("layout", layout);

        Assert.AreEqual("b", desk.ActiveClass());
        Assert.AreEqual(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
    }

    private static int Grouped(System.Text.Json.JsonElement client) => client.GetProperty("grouped").GetArrayLength();

    private static string LayoutOf(Outcome outcome, string name) =>
        Assert.ContainsSingle(workspace => workspace.Id == new WorkspaceId(name), outcome.State.Workspaces).TiledLayout;

    private static WindowManager ScrollingWithANarrowColumn()
    {
        var core = Flat("general:layout = scrolling\n");
        core.Handle(Appeared(1));
        core.Handle(Appeared(2));
        core.Handle(new Dispatch("layoutmsg", "colresize 0.3"));
        return core;
    }

    private static WindowManager DwindleWithAToggledSplit()
    {
        var core = Flat();
        core.Handle(Appeared(1));
        core.Handle(Appeared(2));
        core.Handle(new Dispatch("layoutmsg", "togglesplit"));
        core.Handle(new Dispatch("resizeactive", "0 100"));
        return core;
    }

    private static string Layout(Outcome outcome) =>
        Assert.ContainsSingle(workspace => workspace.Id == outcome.State.ActiveWorkspace, outcome.State.Workspaces).TiledLayout;
}
