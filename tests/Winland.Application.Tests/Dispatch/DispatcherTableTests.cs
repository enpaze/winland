namespace Winland.Application.Tests;

public class DispatcherTableTests
{
    [Fact]
    public void AnUnknownDispatcherIsAnError()
    {
        var reply = new WindowManager().Execute("dispatch notadispatcher");

        Assert.Equal("error: unknown dispatcher 'notadispatcher'", reply.Text);
    }

    [Fact]
    public void FlippingASplitIsStillALayoutMessageAndADispatcher()
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        desk.Ok("layoutmsg", "togglesplit");
        desk.Ok("togglesplit");
    }
}
