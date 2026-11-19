namespace Winland.Application.Tests;

[TestClass]
public class BindFlagTests
{
    [TestMethod]
    public void ABindRunsOnceOnThePressAndSwallowsTheHeldKeyAndItsRelease()
    {
        var core = Load("bind = Win+Q, close");
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
        var core = Load("bind = Win+A&B, close");
        Appear(core, 1);
        Press(core, "Super_L");

        Assert.IsFalse(Press(core, "A").Consumed);
        Assert.IsFalse(Press(core, "B").Consumed);
    }

    [TestMethod]
    public void ScrollBindFiresThenWaitsForTheDelay()
    {
        var core = Load("bind = Win+mouse_down, close\n");
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
        var parsed = ConfigParser.Parse("bind = Win+Retrun, close\nbind = Win+a&b, close\nbind = Win+Return, close\n");

        Assert.AreSequenceEqual(["Unknown key 'Retrun'.", "Unknown key 'a&b'."], parsed.Warnings.Select(warning => warning.Message));
        Assert.ContainsSingle(parsed.Binds);
    }

    [TestMethod]
    public void ADollarSignAloneIsTheDollarKey()
    {
        var parsed = ConfigParser.Parse("bind = Win+$, close\n");

        Assert.IsEmpty(parsed.Warnings);
        var bind = Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(parsed.Binds));
        Assert.AreEqual(ConfigParser.Parse("bind = Win+dollar, close\n").Binds[0], bind);
        Assert.AreEqual(bind, ConfigParser.Parse(BindText.Line(bind)).Binds[0]);
    }

    [TestMethod]
    public void AKeyTheLayoutNamesWithADollarSignIsAKey()
    {
        var layout = KeyLayout.From(new Dictionary<int, string> { [0xBA] = "$$" }, hasAltGr: false, deadKeys: []);

        var parsed = ConfigParser.Parse("bind = Win+$$, close\n", layout: layout);

        Assert.IsEmpty(parsed.Warnings);
        Assert.AreEqual(Modifier.Win, Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(parsed.Binds)).Modifiers);
    }

    [TestMethod]
    public void AnUnknownModifierBeforeTheDollarKeyIsNamed()
    {
        var parsed = ConfigParser.Parse("bind = Bogus+$, close\n");

        Assert.AreEqual(
            "Unknown modifier 'Bogus' in chord 'Bogus+$'. The modifiers are Win, Shift, Ctrl and Alt.",
            Assert.ContainsSingle(parsed.Warnings).Message);
        Assert.IsEmpty(parsed.Binds);
    }

    [TestMethod]
    [DataRow("$mod+Shift+Q")]
    [DataRow("$1+Q")]
    [DataRow("$+$mod+Q")]
    [DataRow("Win+$q")]
    public void AnUndefinedVariableInTheChordWarnsAndTheBindIsSkipped(string chord)
    {
        var parsed = ConfigParser.Parse($"bind = {chord}, close" + Environment.NewLine);

        Assert.AreEqual($"Undefined variable in chord '{chord}'.", Assert.ContainsSingle(parsed.Warnings).Message);
        Assert.IsEmpty(parsed.Binds);
    }

    [TestMethod]
    public void MouseButtonNameRunsANormalBind()
    {
        var core = Load("bind = Win+mouse:left, close");
        Appear(core, 1);
        Press(core, "Super_L");

        var outcome = core.Handle(new PointerButtonPressed("mouse:left") { Position = new Point(4, 5) });

        Assert.AreEqual(new WindowId(1), outcome.Close);
    }
}
