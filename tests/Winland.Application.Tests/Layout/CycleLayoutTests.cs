namespace Winland.Application.Tests;

[TestClass]
public class CycleLayoutTests
{
    private static readonly Rectangle Area = new(0, 0, 800, 600);

    [TestMethod]
    public void CycleLayoutWalksDwindleScrollingAndFloating()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));

        var scrolling = core.Handle(new Dispatch("cyclelayout", ""));
        var floating = core.Handle(new Dispatch("cyclelayout", ""));
        var dwindle = core.Handle(new Dispatch("cyclelayout", ""));

        Assert.AreEqual("scrolling", Layout(scrolling));
        Assert.IsFalse(Client(scrolling, 1).Floating);
        Assert.AreEqual("floating", Layout(floating));
        Assert.IsTrue(Client(floating, 1).Floating);
        Assert.AreEqual("dwindle", Layout(dwindle));
        Assert.IsFalse(Client(dwindle, 1).Floating);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("next")]
    [DataRow("prev")]
    public void OtherWordsGoForward(string arguments)
    {
        var core = new WindowManager();

        Assert.AreEqual("scrolling", Layout(core.Handle(new Dispatch("cyclelayout", arguments))));
    }

    [TestMethod]
    [DataRow("back")]
    [DataRow("BACK")]
    [DataRow("back now")]
    public void BackGoesTheOtherWay(string arguments)
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));

        var floating = core.Handle(new Dispatch("cyclelayout", arguments));
        var scrolling = core.Handle(new Dispatch("cyclelayout", "back"));

        Assert.AreEqual("floating", Layout(floating));
        Assert.IsTrue(Client(floating, 1).Floating);
        Assert.AreEqual("scrolling", Layout(scrolling));
        Assert.IsFalse(Client(scrolling, 1).Floating);
    }

    [TestMethod]
    public void AClientThatWasFloatingStaysFloating()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(new Dispatch("togglefloating", ""));

        core.Handle(new Dispatch("cyclelayout", ""));
        core.Handle(new Dispatch("cyclelayout", ""));
        var dwindle = core.Handle(new Dispatch("cyclelayout", ""));

        Assert.AreEqual("dwindle", Layout(dwindle));
        Assert.IsTrue(Client(dwindle, 1).Floating);
    }

    [TestMethod]
    public void TogglefloatingDoesNotTileInsideTheFloatingLayout()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(new Dispatch("cyclelayout", "back"));

        var tiled = core.Handle(new Dispatch("togglefloating", ""));

        Assert.AreEqual("floating", Layout(tiled));
        Assert.IsTrue(Client(tiled, 1).Floating);
    }

    [TestMethod]
    public void OnlyTheFocusedWorkspaceChanges()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(Appeared(2, Area));

        var cycled = core.Handle(new Dispatch("cyclelayout", ""));

        Assert.AreEqual("scrolling", LayoutOf(cycled, "2"));
        Assert.AreEqual("dwindle", LayoutOf(cycled, "1"));
    }

    [TestMethod]
    public void AnOpenSpecialWorkspaceIsTheOneThatCycles()
    {
        var core = new WindowManager();
        core.Handle(Appeared(1, Area));
        core.Handle(new Dispatch("movetoworkspace", "special:scratch"));

        var cycled = core.Handle(new Dispatch("cyclelayout", ""));

        Assert.AreEqual("scrolling", LayoutOf(cycled, "special:scratch"));
        Assert.AreEqual("dwindle", LayoutOf(cycled, "1"));
    }

    [TestMethod]
    public void AReloadReturnsToTheConfigLayout()
    {
        var config = ConfigParser.Parse("general:layout = scrolling\n");
        var core = new WindowManager(config);
        core.Handle(Appeared(1, Area));
        var cycled = core.Handle(new Dispatch("cyclelayout", ""));
        Assert.AreEqual("floating", Layout(cycled));

        var reloaded = core.Handle(new Configure(config));

        Assert.AreEqual("scrolling", Layout(reloaded));
        Assert.IsFalse(Client(reloaded, 1).Floating);
    }

    private static string Layout(Outcome outcome) =>
        Assert.ContainsSingle(workspace => workspace.Id == outcome.State.ActiveWorkspace, outcome.State.Workspaces).TiledLayout;

    private static string LayoutOf(Outcome outcome, string name) =>
        Assert.ContainsSingle(workspace => workspace.Id == new WorkspaceId(name), outcome.State.Workspaces).TiledLayout;
}
