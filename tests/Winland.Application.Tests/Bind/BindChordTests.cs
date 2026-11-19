namespace Winland.Application.Tests;

[TestClass]
public class BindChordTests
{
    [TestMethod]
    public void ModifierKeyBindLeavesSuperHeldForAMouseBind()
    {
        var core = Load("bind = Super_L, exec menu\nbind = Win+mouse:left, move\n");
        Appear(core, 1);

        var super = Press(core, "Super_L");
        var button = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(500, 400) });

        Assert.AreEqual("menu", super.Launch?.CommandLine);
        Assert.IsTrue(super.Consumed);
        Assert.IsTrue(button.Consumed);
    }

    [TestMethod]
    public void ScrollBindsOnOneNotchAllFireThenWait()
    {
        var core = Load("bind = mouse_down, close\nbind = mouse_down, workspace 2\n");
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
}
