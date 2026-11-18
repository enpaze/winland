namespace Winland.Application.Tests;

[TestClass]
public class StuckModifierTests
{
    [TestMethod]
    public void SuperReleaseAfterAChordPassesAndTheChordMasksSuper()
    {
        var (core, session) = Load("bind = SUPER, T, exec, teams");

        var super = core.Decide(new KeyPressed("Super_L"), session);
        var t = core.Decide(new KeyPressed("T"), session);
        var tUp = core.Decide(new KeyReleased("T"), session);
        var superUp = core.Decide(new KeyReleased("Super_L"), session);

        Assert.IsFalse(super.Consumed);
        Assert.IsFalse(super.Mask);
        Assert.IsTrue(t.Consumed);
        Assert.IsTrue(t.Mask);
        Assert.AreEqual("exec", t.Dispatcher);
        Assert.IsTrue(tUp.Consumed);
        Assert.IsFalse(tUp.Mask);
        Assert.IsFalse(superUp.Consumed);
        Assert.IsFalse(superUp.Mask);
    }

    [TestMethod]
    public void SuperReleasedBeforeTheKeyStillPasses()
    {
        var (core, session) = Load("bind = SUPER, T, exec, teams");
        core.Decide(new KeyPressed("Super_L"), session);
        core.Decide(new KeyPressed("T"), session);

        var superUp = core.Decide(new KeyReleased("Super_L"), session);
        var tUp = core.Decide(new KeyReleased("T"), session);

        Assert.IsFalse(superUp.Consumed);
        Assert.IsTrue(tUp.Consumed);
        Assert.IsFalse(core.Decide(new KeyPressed("T"), session).Consumed);
    }

    [TestMethod]
    public void EveryHeldModifierReleasePasses()
    {
        var (core, session) = Load("bind = SUPER CTRL ALT SHIFT, T, close");
        foreach (var key in new[] { "Super_L", "Control_L", "Alt_L", "Shift_L" })
            Assert.IsFalse(core.Decide(new KeyPressed(key), session).Consumed);

        var t = core.Decide(new KeyPressed("T"), session);
        Assert.IsTrue(t.Consumed);
        Assert.IsTrue(t.Mask);
        Assert.IsTrue(core.Decide(new KeyReleased("T"), session).Consumed);

        foreach (var key in new[] { "Super_L", "Control_L", "Alt_L", "Shift_L" })
            Assert.IsFalse(core.Decide(new KeyReleased(key), session).Consumed);
    }

    [TestMethod]
    [DataRow("CTRL", "Control_L")]
    [DataRow("SHIFT", "Shift_R")]
    public void ChordWithoutSuperOrAltIsNotMasked(string modifier, string key)
    {
        var (core, session) = Load($"bind = {modifier}, T, close");
        core.Decide(new KeyPressed(key), session);

        var t = core.Decide(new KeyPressed("T"), session);

        Assert.IsTrue(t.Consumed);
        Assert.IsFalse(t.Mask);
        Assert.IsFalse(core.Decide(new KeyReleased(key), session).Consumed);
    }

    [TestMethod]
    public void AltChordIsMasked()
    {
        var (core, session) = Load("bind = ALT, T, close");
        core.Decide(new KeyPressed("Alt_R"), session);

        Assert.IsTrue(core.Decide(new KeyPressed("T"), session).Mask);
        Assert.IsFalse(core.Decide(new KeyReleased("Alt_R"), session).Consumed);
    }

    [TestMethod]
    public void SuperIsMaskedOncePerHold()
    {
        var (core, session) = Load("bind = SUPER, T, close");
        core.Decide(new KeyPressed("Super_L"), session);

        var first = core.Decide(new KeyPressed("T"), session);
        core.Decide(new KeyReleased("T"), session);
        var second = core.Decide(new KeyPressed("T"), session);
        core.Decide(new KeyReleased("T"), session);
        core.Decide(new KeyReleased("Super_L"), session);
        core.Decide(new KeyPressed("Super_L"), session);
        var third = core.Decide(new KeyPressed("T"), session);

        Assert.IsTrue(first.Mask);
        Assert.IsFalse(second.Mask);
        Assert.IsTrue(third.Mask);
    }

    [TestMethod]
    public void SuperAloneIsNotMasked()
    {
        var (core, session) = Load("bind = SUPER, T, close");

        var down = core.Decide(new KeyPressed("Super_L"), session);
        var up = core.Decide(new KeyReleased("Super_L"), session);

        Assert.IsFalse(down.Mask);
        Assert.IsFalse(up.Consumed);
        Assert.IsFalse(up.Mask);
    }

    [TestMethod]
    public void MouseChordWithSuperMasksSuperAndPassesItsRelease()
    {
        var (core, session) = Load("bind = SUPER, mouse:left, move");
        core.Decide(new KeyPressed("Super_L"), session);

        var down = core.Decide(new PointerButtonPressed("mouse:left") { Position = new Point(0, 0) }, session);
        core.Decide(new PointerMoved(new Point(200, 200)), session);
        var superUp = core.Decide(new KeyReleased("Super_L"), session);
        var buttonUp = core.Decide(new PointerButtonReleased("mouse:left") { Position = new Point(200, 200) }, session);

        Assert.IsTrue(down.Consumed);
        Assert.IsTrue(down.Mask);
        Assert.IsFalse(superUp.Consumed);
        Assert.IsTrue(buttonUp.Consumed);
    }

    [TestMethod]
    public void WheelChordWithSuperMasksSuper()
    {
        var (core, session) = Load("bind = SUPER, mouse_down, workspace, next");
        core.Decide(new KeyPressed("Super_L"), session);

        var wheel = core.Decide(new WheelTurned("mouse_down", 1000), session);

        Assert.IsTrue(wheel.Consumed);
        Assert.IsTrue(wheel.Mask);
        Assert.IsFalse(core.Decide(new KeyReleased("Super_L"), session).Consumed);
    }

    [TestMethod]
    public void InhibitedSessionPassesTheModifierRelease()
    {
        var (core, session) = Load("bind = SUPER, T, close");
        core.Decide(new KeyPressed("Super_L"), session);
        core.Decide(new KeyPressed("T"), session);
        session.Inhibit(true);

        var up = core.Decide(new KeyReleased("Super_L"), session);

        Assert.IsFalse(up.Consumed);
    }

    [TestMethod]
    public void ForgetDropsKeysThatAreNoLongerDown()
    {
        var (core, session) = Load("bind = SUPER, T, close");
        core.Decide(new KeyPressed("Super_L"), session);
        core.Decide(new KeyPressed("T"), session);

        session.Forget(_ => false);

        Assert.IsFalse(core.Decide(new KeyPressed("T"), session).Consumed);
        Assert.IsFalse(core.Decide(new KeyReleased("T"), session).Consumed);
    }

    [TestMethod]
    public void ForgetKeepsKeysThatAreStillDown()
    {
        var (core, session) = Load("bind = SUPER, T, close");
        core.Decide(new KeyPressed("Super_L"), session);

        session.Forget(key => key == "Super_L");

        Assert.IsTrue(core.Decide(new KeyPressed("T"), session).Consumed);
    }

    [TestMethod]
    public void SuperMouseChordAfterASuperKeyChordIsNotMaskedTwice()
    {
        var (core, session) = Load("bind = SUPER, F23, close\nbind = SUPER, mouse:left, move\n");
        core.Decide(new KeyPressed("Super_L"), session);

        var key = core.Decide(new KeyPressed("F23"), session);
        core.Decide(new KeyReleased("F23"), session);
        var button = core.Decide(new PointerButtonPressed("mouse:left"), session);

        Assert.IsTrue(key.Mask);
        Assert.IsTrue(button.Consumed);
        Assert.IsFalse(button.Mask);
        Assert.IsTrue(core.Decide(new PointerButtonReleased("mouse:left"), session).Consumed);
        Assert.IsFalse(core.Decide(new KeyReleased("Super_L"), session).Consumed);
    }

    [TestMethod]
    public void ModifierOnlyBindSwallowsBothEdgesAndTheChordsUnderIt()
    {
        var (core, session) = Load("bind = , Super_L, exec, menu\nbind = SUPER, F23, close\n");

        var down = core.Decide(new KeyPressed("Super_L"), session);
        var chord = core.Decide(new KeyPressed("F23"), session);
        core.Decide(new KeyReleased("F23"), session);
        var up = core.Decide(new KeyReleased("Super_L"), session);

        Assert.AreEqual("exec", down.Dispatcher);
        Assert.IsTrue(down.Consumed);
        Assert.IsFalse(down.Mask);
        Assert.IsTrue(chord.Consumed);
        Assert.IsFalse(chord.Mask);
        Assert.IsTrue(up.Consumed);
        Assert.IsFalse(core.Decide(new KeyPressed("F23"), session).Consumed);
    }

    private static (WindowManager WindowManager, BindSession Session) Load(string config)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(config)));
        return (core, new BindSession());
    }
}
