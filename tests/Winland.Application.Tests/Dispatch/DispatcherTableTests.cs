namespace Winland.Application.Tests;

public class DispatcherTableTests
{
    [Fact]
    public void AnUnknownDispatcherIsAnError()
    {
        var reply = new WindowManager().Execute("dispatch notadispatcher");

        Assert.Equal("error: unknown dispatcher 'notadispatcher'", reply.Text);
    }

    [Theory]
    [InlineData("sendshortcut SUPER, A,")]
    [InlineData("pseudo")]
    [InlineData("closeall")]
    [InlineData("cyclenext")]
    [InlineData("focusurgentorlast")]
    [InlineData("centerwindow")]
    [InlineData("movegroupwindow f")]
    [InlineData("swapsplit")]
    [InlineData("rotatesplit")]
    [InlineData("splitratio 0.1")]
    public void ARemovedDispatcherIsUnknownLikeAnyOtherName(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        var name = line.Split(' ')[0];

        Assert.StartsWith("error: unknown dispatcher '" + name + "'", desk.Dispatch(line), StringComparison.Ordinal);
        Assert.DoesNotContain(name, WindowManager.DispatcherNames, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("preselect l")]
    [InlineData("swapsplit")]
    [InlineData("rotatesplit")]
    [InlineData("movetoroot")]
    [InlineData("splitratio 0.1")]
    public void ARemovedLayoutMessageIsABadMessage(string message)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");

        Assert.Equal("error: bad layoutmsg '" + message.Split(' ')[0] + "'", desk.Dispatch("layoutmsg", message));
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

    [Fact]
    public void ClickToKillIsNotARequest()
    {
        Assert.Equal("error: unknown request", new WindowManager().Execute("kill").Text);
    }
}
