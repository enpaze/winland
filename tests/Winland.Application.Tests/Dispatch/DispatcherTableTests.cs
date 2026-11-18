namespace Winland.Application.Tests;

[TestClass]
public class DispatcherTableTests
{
    [TestMethod]
    public void AnUnknownDispatcherIsAnError()
    {
        var reply = new WindowManager().Execute("notadispatcher");

        Assert.AreEqual("error: unknown command 'notadispatcher'", reply.Text);
    }

    [TestMethod]
    public void SplitRunsAsADispatcher()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        desk.Ok("split");
        desk.Ok("split");
    }

    [TestMethod]
    public void ResizeWithNoArgumentOutsideADragIsAnError()
    {
        var core = new WindowManager();
        Appear(core, 1);

        Assert.AreEqual("error: resize is a drag; bind it to a mouse button", core.Execute("resize").Text);
    }

    [TestMethod]
    public void AKeyBoundToAResizeDragWarnsAndIsSkipped()
    {
        var document = ConfigParser.Parse(
            "bind = SUPER, R, resize\n"
            + "bind = SUPER, mouse_up, resize\n"
            + "bind = SUPER, mouse:right, resize\n");

        Assert.AreSequenceEqual([1, 2], document.Warnings.Select(warning => warning.Line));
        Assert.IsTrue(document.Warnings.All(warning => warning.Message == "resize is a drag; bind it to a mouse button."));
        Assert.AreEqual("mouse:right", Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds)).Key);
    }
}
