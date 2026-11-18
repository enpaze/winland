namespace Winland.Application.Tests;

[TestClass]
public class DispatcherRegressionTests
{
    private static readonly Rectangle Area = new(0, 0, 1000, 800);

    [TestMethod]
    [DataRow("resizeactive", "10 0")]
    [DataRow("killactive", "")]
    public void APreviousLayoutMessageDoesNotOverrideFocusOnAnotherDispatcher(string name, string argument)
    {
        var core = Configured("general:layout = scrolling");
        for (var id = 1; id <= 3; id++)
            core.Handle(new WindowAppeared(new WindowId(id), Area));
        core.Handle(new Dispatch("layoutmsg", "focus l"));
        core.Handle(new Dispatch("focuswindow", "address:0x3"));

        var result = core.Handle(new Dispatch(name, argument));

        Assert.IsNull(result.Focus);
        Assert.AreEqual("0x3", Json(core.Execute("j/get window").Text).GetProperty("address").GetString());
    }

    [TestMethod]
    public void MovefocusUsesTheOpenSpecialWorkspace()
    {
        var core = new WindowManager();
        core.Handle(new Dispatch("togglespecialworkspace", "tools"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        Assert.AreEqual(new WindowId(1), core.Handle(new Dispatch("movefocus", "l")).Focus);
    }

    [TestMethod]
    public void FocuswindowRefusesANoFocusClient()
    {
        var core = Configured("windowrule = no_focus, class:quiet");
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area) { Traits = WindowTraits.Normal with { Class = "quiet" } });

        var result = core.Handle(new Dispatch("focuswindow", "address:0x2"));

        Assert.IsNull(result.Focus);
        Assert.AreEqual("0x1", Json(core.Execute("j/get window").Text).GetProperty("address").GetString());
    }

    [TestMethod]
    public void ANativeFloatingMoveJoinsTheMonitorUnderItsMiddle()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([Display("left", 0, 1000, 800), Display("right", 1000, 1000, 800)]));
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Dialog = true } });

        var moved = Place(core, 1, new Rectangle(1200, 100, 300, 200));

        Assert.AreEqual(1, Client(moved, 1).Workspace.Monitor);
        Assert.AreEqual(new Rectangle(1200, 100, 300, 200), Client(moved, 1).Bounds);
    }

    [TestMethod]
    public void LayoutMessagesUseTheLayoutmsgDispatcher()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var refused = core.Handle(new Dispatch("layout", "togglesplit"));

        Assert.AreEqual("error: unknown layout 'togglesplit'", refused.Error);
        Assert.IsEmpty(refused.Placements);
    }
}
