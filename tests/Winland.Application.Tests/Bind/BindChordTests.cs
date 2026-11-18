namespace Winland.Application.Tests;

[TestClass]
public class BindChordTests
{
    [TestMethod]
    public void ModifierKeyBindLeavesSuperHeldForAMouseBind()
    {
        var core = Load("bind = , Super_L, exec, menu\nbind = SUPER, mouse:left, movewindow\n");
        Appear(core, 1);

        var super = Press(core, "Super_L");
        var button = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(500, 400) });

        Assert.AreEqual("menu", super.Launch?.CommandLine);
        Assert.IsTrue(super.Consumed);
        Assert.IsTrue(button.Consumed);
    }

    [TestMethod]
    public void ClickBindOnASharedChordStaysQuietOnceThePointerLeaves()
    {
        var core = Load("binds:drag_threshold = 10\nbind = , V, exec, notepad\nbindc = , V, killactive\n");
        Appear(core, 1);
        core.Handle(new PointerMoved(new Point(0, 0)));
        Press(core, "V");
        core.Handle(new PointerMoved(new Point(40, 0)));

        var released = Release(core, "V");

        Assert.IsTrue(released.Consumed);
        Assert.IsNull(released.Close);
    }

    [TestMethod]
    public void ClickBindMeasuresFromItsOwnPressNotTheNewestOne()
    {
        var core = Load("binds:drag_threshold = 10\nbindc = SUPER, V, killactive\n");
        Appear(core, 1);
        core.Handle(new PointerMoved(new Point(0, 0)));
        Press(core, "Super_L");
        Press(core, "V");
        core.Handle(new PointerMoved(new Point(40, 0)));
        Press(core, "B");
        Release(core, "B");

        var released = Release(core, "V");

        Assert.IsNull(released.Close);
    }

    [TestMethod]
    public void ScrollBindsOnOneNotchAllFireThenWait()
    {
        var core = Load("bind = , mouse_down, killactive\nbind = , mouse_down, workspace, 2\n");
        Appear(core, 1);
        Appear(core, 2);

        var first = core.Handle(new WheelTurned("mouse_down", 1_000));
        var blocked = core.Handle(new WheelTurned("mouse_down", 1_200));
        var later = core.Handle(new WheelTurned("mouse_down", 1_300));

        Assert.IsTrue(first.Consumed);
        Assert.AreEqual(new WindowId(2), first.Close);
        Assert.AreEqual(new WorkspaceId("2"), first.State.ActiveWorkspace);
        Assert.IsTrue(blocked.Consumed);
        Assert.AreEqual(new WorkspaceId("2"), blocked.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("2"), later.State.ActiveWorkspace);
    }

    [TestMethod]
    public void EmptyMouseBindConsumesAndTheOtherMouseBindStillRuns()
    {
        var core = Load("bind = , mouse:left,\nbind = , mouse:left, movewindow\n");
        Appear(core, 1);

        var pressed = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(500, 400) });

        Assert.IsTrue(pressed.Consumed);
    }
}
