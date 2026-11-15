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
    public void ARemovedDispatcherIsUnknownLikeAnyOtherName(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        var name = line.Split(' ')[0];

        Assert.StartsWith("error: unknown dispatcher '" + name + "'", desk.Dispatch(line), StringComparison.Ordinal);
        Assert.DoesNotContain(name, WindowManager.DispatcherNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClickToKillIsNotARequest()
    {
        Assert.Equal("error: unknown request", new WindowManager().Execute("kill").Text);
    }
}
