namespace Winland.Application.Tests;

[TestClass]
public class DispatcherTableTests
{
    [TestMethod]
    public void AnUnknownDispatcherIsAnError()
    {
        var reply = new WindowManager().Execute("dispatch notadispatcher");

        Assert.AreEqual("error: unknown dispatcher 'notadispatcher'", reply.Text);
    }

    [TestMethod]
    public void TogglesplitRunsAsALayoutMessageAndAsADispatcher()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        desk.Ok("layoutmsg", "togglesplit");
        desk.Ok("togglesplit");
    }

    [TestMethod]
    public void ResizewindowOutsideADragIsAnError()
    {
        var core = new WindowManager();
        Appear(core, 1);

        Assert.AreEqual("error: resizewindow is a drag; bind it to a mouse button", core.Execute("dispatch resizewindow").Text);
    }

    [TestMethod]
    public void AKeyBoundToResizewindowWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse(
            "bind = SUPER, R, resizewindow\n"
            + "bind = SUPER, mouse_up, resizewindow\n"
            + "bind = SUPER, mouse:right, resizewindow\n");

        Assert.AreSequenceEqual([1, 2], document.Warnings.Select(warning => warning.Line));
        Assert.IsTrue(document.Warnings.All(warning => warning.Message == "resizewindow is a drag; bind it to a mouse button."));
        Assert.AreEqual("mouse:right", Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds)).Key);
    }
}
