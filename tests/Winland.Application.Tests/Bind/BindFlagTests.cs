namespace Winland.Application.Tests;

[TestClass]
public class BindFlagTests
{
    [TestMethod]
    public void ABindRunsOnceOnThePressAndSwallowsTheHeldKeyAndItsRelease()
    {
        var core = Load("bind = SUPER, Q, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        var press = Press(core, "Q");
        var held = Press(core, "Q");
        var release = Release(core, "Q");

        Assert.AreEqual(new WindowId(1), press.Close);
        Assert.IsTrue(press.Consumed);
        Assert.IsNull(held.Close);
        Assert.IsTrue(held.Consumed);
        Assert.IsNull(release.Close);
        Assert.IsTrue(release.Consumed);
    }

    [TestMethod]
    public void AmpersandIsASingleKeyWithoutTheSeparateFlag()
    {
        var core = Load("bind = SUPER, A&B, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        Assert.IsFalse(Press(core, "A").Consumed);
        Assert.IsFalse(Press(core, "B").Consumed);
    }

    [TestMethod]
    public void ScrollBindFiresThenWaitsForTheDelay()
    {
        var core = Load("bind = SUPER, mouse_down, killactive\n");
        Appear(core, 1);
        Press(core, "Super_L");

        Assert.AreEqual(new WindowId(1), core.Handle(new WheelTurned("mouse_down", 1_000)).Close);

        var blocked = core.Handle(new WheelTurned("mouse_down", 1_200));
        Assert.IsTrue(blocked.Consumed);
        Assert.IsNull(blocked.Close);

        Assert.AreEqual(new WindowId(1), core.Handle(new WheelTurned("mouse_down", 1_300)).Close);
    }

    [TestMethod]
    public void AnUnknownKeyNameWarnsAndTheBindIsSkipped()
    {
        var parsed = ConfigParser.Parse("bind = SUPER, Retrun, killactive\nbind = SUPER, a&b, killactive\nbind = SUPER, Return, killactive\n");

        Assert.AreSequenceEqual(["Unknown key 'Retrun'.", "Unknown key 'a&b'."], parsed.Warnings.Select(warning => warning.Message));
        Assert.ContainsSingle(parsed.Binds);
    }

    [TestMethod]
    public void AnUndefinedVariableInTheModifiersWarnsAndTheBindIsSkipped()
    {
        var parsed = ConfigParser.Parse("bind = $mod SHIFT, Q, killactive" + Environment.NewLine);

        Assert.AreEqual("Undefined variable in modifiers '$mod SHIFT'.", Assert.ContainsSingle(parsed.Warnings).Message);
        Assert.IsEmpty(parsed.Binds);
    }

    [TestMethod]
    public void MouseButtonNameRunsANormalBind()
    {
        var core = Load("bind = SUPER, mouse:left, killactive");
        Appear(core, 1);
        Press(core, "Super_L");

        var outcome = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(4, 5) });

        Assert.AreEqual(new WindowId(1), outcome.Close);
    }
}
