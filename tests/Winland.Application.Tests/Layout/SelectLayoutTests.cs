namespace Winland.Application.Tests;

public class SelectLayoutTests
{
    [Theory]
    [InlineData("dwindle", "scrolling")]
    [InlineData("dwindle", "floating")]
    [InlineData("scrolling", "dwindle")]
    [InlineData("floating", "scrolling")]
    public void LayoutSelectsTheNamedLayout(string from, string to)
    {
        var core = Configured($"general:layout = {from}\n");
        core.Handle(Appeared(1));

        var selected = core.Handle(new Dispatch("layout", to));

        Assert.Null(selected.Error);
        Assert.Equal(to, Layout(selected));
        Assert.Single(selected.State.Clients);
    }

    [Theory]
    [InlineData("master")]
    [InlineData("")]
    [InlineData("scrolling dwindle")]
    public void ANameThatIsNoLayoutErrorsAndChangesNothing(string name)
    {
        var core = Configured("general:layout = scrolling\n");
        core.Handle(Appeared(1));

        var refused = core.Handle(new Dispatch("layout", name));

        Assert.Equal($"error: unknown layout '{name}'", refused.Error);
        Assert.Empty(refused.Placements);
        Assert.Equal("scrolling", Layout(refused));
    }

    [Fact]
    public void ATypoSuggestsTheLayout()
    {
        var core = new WindowManager();

        var refused = core.Handle(new Dispatch("layout", "scroling"));

        Assert.Equal("error: unknown layout 'scroling'. Did you mean 'scrolling'?", refused.Error);
    }

    [Fact]
    public void SelectingTheRunningScrollingLayoutKeepsItsColumnWidths()
    {
        var kept = ScrollingWithANarrowColumn();
        var untouched = ScrollingWithANarrowColumn();

        var selected = kept.Handle(new Dispatch("layout", "scrolling"));
        var after = kept.Handle(Appeared(3));
        var expected = untouched.Handle(Appeared(3));

        Assert.Null(selected.Error);
        Assert.Empty(selected.Placements);
        Assert.Equal(BoundsOf(expected, 1), BoundsOf(after, 1));
        Assert.Equal(BoundsOf(expected, 2), BoundsOf(after, 2));
    }

    [Fact]
    public void SelectingTheRunningDwindleLayoutKeepsItsSplits()
    {
        var kept = DwindleWithAToggledSplit();
        var untouched = DwindleWithAToggledSplit();

        var selected = kept.Handle(new Dispatch("layout", "DWINDLE"));
        var after = kept.Handle(Appeared(3));
        var expected = untouched.Handle(Appeared(3));

        Assert.Null(selected.Error);
        Assert.Empty(selected.Placements);
        Assert.Equal(BoundsOf(expected, 1), BoundsOf(after, 1));
        Assert.Equal(BoundsOf(expected, 2), BoundsOf(after, 2));
        Assert.Equal(BoundsOf(expected, 3), BoundsOf(after, 3));
    }

    [Fact]
    public void OnlyTheCurrentWorkspaceChanges()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(Appeared(2));

        var selected = core.Handle(new Dispatch("layout", "scrolling"));

        Assert.Equal("scrolling", LayoutOf(selected, "2"));
        Assert.Equal("dwindle", LayoutOf(selected, "1"));
    }

    [Fact]
    public void AnOpenSpecialWorkspaceIsTheOneThatChanges()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1));
        core.Handle(new Dispatch("movetoworkspace", "special:scratch"));

        var selected = core.Handle(new Dispatch("layout", "floating"));

        Assert.Equal("floating", LayoutOf(selected, "special:scratch"));
        Assert.Equal("dwindle", LayoutOf(selected, "1"));
    }

    [Fact]
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

            Assert.Equal(3, desk.Clients().Length);
            Assert.Equal(2, Grouped(desk.Client("a")));
            Assert.Equal(2, Grouped(desk.Client("b")));
            Assert.Equal(0, Grouped(desk.Client("c")));
            desk.Ok("changegroupactive", "f");
        }
    }

    [Fact]
    public void EachWindowKeepsItsStateThroughEachLayout()
    {
        var desk = new TestDesktop();
        desk.Spawn("tiled", "Tiled title");
        desk.Spawn("pinned");
        desk.Ok("togglefloating");
        desk.Ok("pin");
        desk.Spawn("small");
        desk.Handle(new WindowMinimized(desk.Window("small")));
        desk.Dispatch("setprop", "class:tiled opacity 0.5");
        desk.Ok("focuswindow", "class:tiled");
        var opacity = desk.Send("getprop class:tiled opacity");

        foreach (var layout in new[] { "scrolling", "floating", "dwindle" })
        {
            var selected = desk.Handle(new Dispatch("layout", layout));
            var tiled = Client(selected, desk.Window("tiled").Value);
            var pinned = Client(selected, desk.Window("pinned").Value);

            Assert.Null(selected.Error);
            Assert.Equal(3, selected.State.Clients.Count);
            Assert.Equal("tiled", desk.ActiveClass());
            Assert.Equal(("tiled", "Tiled title"), (tiled.Class, tiled.Title));
            Assert.Equal(layout == "floating", tiled.Floating);
            Assert.True(pinned.Floating);
            Assert.True(pinned.Pinned);
            Assert.True(Client(selected, desk.Window("small").Value).Minimized);
            Assert.Equal(opacity, desk.Send("getprop class:tiled opacity"));
        }
    }

    [Theory]
    [InlineData("scrolling")]
    [InlineData("floating")]
    public void AFullscreenWindowStaysFullscreen(string layout)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("fullscreen", "0");

        desk.Ok("layout", layout);

        Assert.Equal("b", desk.ActiveClass());
        Assert.Equal(2, TestDesktop.Fullscreen(desk.ActiveWindow()));
    }

    [Fact]
    public void APseudoWindowStaysPseudoBetweenTiledLayouts()
    {
        var core = Flat();
        core.Handle(Appeared(1, bounds: new Rectangle(0, 0, 800, 600)));
        core.Handle(new Pseudo(new WindowId(1)));

        var scrolling = core.Handle(new Dispatch("layout", "scrolling"));
        var dwindle = core.Handle(new Dispatch("layout", "dwindle"));

        Assert.Equal(new Rectangle(560, 240, 800, 600), BoundsOf(scrolling, 1));
        Assert.Equal(new Rectangle(560, 240, 800, 600), BoundsOf(dwindle, 1));
    }

    private static int Grouped(System.Text.Json.JsonElement client) => client.GetProperty("grouped").GetArrayLength();

    private static string LayoutOf(Outcome outcome, string name) =>
        Assert.Single(outcome.State.Workspaces, workspace => workspace.Id == new WorkspaceId(name)).TiledLayout;

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
        core.Handle(new Dispatch("splitratio", "0.3"));
        return core;
    }

    private static string Layout(Outcome outcome) =>
        Assert.Single(outcome.State.Workspaces, workspace => workspace.Id == outcome.State.ActiveWorkspace).TiledLayout;
}
