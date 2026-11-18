namespace Winland.Application.Tests;

[TestClass]
public class ModalTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly WindowId Owner = new(1);
    private static readonly WindowId Dialog = new(2);

    [TestMethod]
    public void AnOwnerShadowedByItsModalTakesNoFocus()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(Owner, Area));
        core.Handle(new WindowEnabledChanged(Owner, Enabled: false));
        core.Handle(new WindowAppeared(Dialog, Area) { Owner = Owner });

        var clicked = core.Handle(new WindowFocused(Owner, FocusCause.User));
        Assert.AreEqual(Dialog, clicked.Focus);
        Assert.AreEqual(255, AlphaOf(clicked, Owner));

        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        Assert.AreEqual(Dialog, core.Handle(new Dispatch("killactive")).Close);

        core.Handle(new WindowEnabledChanged(Owner, Enabled: true));
        core.Handle(new WindowFocused(Owner, FocusCause.User));
        Assert.AreEqual(Owner, core.Handle(new Dispatch("killactive")).Close);
    }

    [TestMethod]
    public void ADisabledWindowWithNoOpenModalIsNotShadowed()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(Owner, Area) { Disabled = true });
        core.Handle(new WindowAppeared(new WindowId(3), Area));

        core.Handle(new WindowFocused(Owner, FocusCause.User));
        Assert.AreEqual(Owner, core.Handle(new Dispatch("killactive")).Close);
    }

    [TestMethod]
    public void AFloatingWindowWithAnOwnerOpensOverTheMiddleOfItsOwner()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:float\n"));
        var traits = WindowTraits.Normal with { Class = "float" };
        core.Handle(new WindowAppeared(Owner, Area) { Traits = traits, Bounds = new Rectangle(0, 0, 400, 300) });
        Place(core, Owner.Value, new Rectangle(100, 100, 400, 300));

        var dialog = core.Handle(new WindowAppeared(Dialog, Area) { Owner = Owner, Traits = traits, Bounds = new Rectangle(0, 0, 200, 100) });
        var lone = core.Handle(new WindowAppeared(new WindowId(3), Area) { Traits = traits, Bounds = new Rectangle(0, 0, 200, 100) });

        Assert.AreEqual(new Rectangle(200, 200, 200, 100), Assert.ContainsSingle(client => client.Window == Dialog, dialog.State.Clients).Bounds);
        Assert.AreEqual(new Rectangle(860, 490, 200, 100), Assert.ContainsSingle(client => client.Window == new WindowId(3), lone.State.Clients).Bounds);
    }

    private static byte AlphaOf(Outcome outcome, WindowId window)
    {
        var client = Assert.ContainsSingle(candidate => candidate.Window == window, outcome.State.Clients);
        return Assert.ContainsSingle(decoration => decoration.Client == client.Id, outcome.Decorations).Alpha;
    }
}
