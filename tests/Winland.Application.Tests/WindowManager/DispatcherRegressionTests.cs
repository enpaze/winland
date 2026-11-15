namespace Winland.Application.Tests;

public class DispatcherRegressionTests
{
    private static readonly Rectangle Area = new(0, 0, 1000, 800);

    [Theory]
    [InlineData("resizeactive", "10 0")]
    [InlineData("killactive", "")]
    public void APreviousLayoutMessageDoesNotOverrideFocusOnAnotherDispatcher(string name, string argument)
    {
        var core = Configured("general:layout = scrolling");
        for (var id = 1; id <= 3; id++)
            core.Handle(new WindowAppeared(new WindowId(id), Area));
        core.Handle(new Dispatch("layoutmsg", "focus l"));
        core.Handle(new Dispatch("focuswindow", "address:0x3"));

        var result = core.Handle(new Dispatch(name, argument));

        Assert.Null(result.Focus);
        Assert.Equal("0x3", Json(core.Execute("j/activewindow").Text).GetProperty("address").GetString());
    }

    [Fact]
    public void MovefocusUsesTheOpenSpecialWorkspace()
    {
        var core = new WindowManager();
        core.Handle(new Dispatch("togglespecialworkspace", "tools"));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        Assert.Equal(new WindowId(1), core.Handle(new Dispatch("movefocus", "l")).Focus);
    }

    [Fact]
    public void FocuswindowRefusesANoFocusClient()
    {
        var core = Configured("windowrule = no_focus, class:quiet");
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area) { Traits = WindowTraits.Normal with { Class = "quiet" } });

        var result = core.Handle(new Dispatch("focuswindow", "address:0x2"));

        Assert.Null(result.Focus);
        Assert.Equal("0x1", Json(core.Execute("j/activewindow").Text).GetProperty("address").GetString());
    }

    [Fact]
    public void ANativeFloatingMoveJoinsTheMonitorUnderItsMiddle()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([Display("left", 0, 1000, 800), Display("right", 1000, 1000, 800)]));
        core.Handle(new WindowAppeared(new WindowId(1), Area) { Traits = WindowTraits.Normal with { Dialog = true } });

        var moved = Place(core, 1, new Rectangle(1200, 100, 300, 200));

        Assert.Equal(1, Client(moved, 1).Workspace.Monitor);
        Assert.Equal(new Rectangle(1200, 100, 300, 200), Client(moved, 1).Bounds);
    }

    [Fact]
    public void LayoutMessagesUseTheLayoutmsgDispatcher()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var refused = core.Handle(new Dispatch("layout", "togglesplit"));

        Assert.Equal("error: unknown layout 'togglesplit'", refused.Error);
        Assert.Empty(refused.Placements);
    }
}
