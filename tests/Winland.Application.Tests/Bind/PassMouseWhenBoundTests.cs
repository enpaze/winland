namespace Winland.Application.Tests;

[TestClass]
public class PassMouseWhenBoundTests
{
    [TestMethod]
    public void ABoundButtonIsSwallowedByDefault()
    {
        var core = Loaded("bind = SUPER, mouse:middle, killactive\n");
        var session = new BindSession();
        core.Decide(new KeyPressed("Super_L"), session);

        var pressed = core.Decide(new PointerButtonPressed("mouse:middle"), session);
        var released = core.Decide(new PointerButtonReleased("mouse:middle"), session);

        Assert.IsTrue(pressed.Consumed);
        Assert.AreEqual("killactive", pressed.Dispatcher);
        Assert.IsTrue(released.Consumed);
    }

    private static WindowManager Loaded(string text)
    {
        var core = new WindowManager();
        core.Handle(new ConfigLoaded(ConfigParser.Parse(text)));
        return core;
    }
}
