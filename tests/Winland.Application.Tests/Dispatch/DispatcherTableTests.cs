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
}
