namespace Winland.Application.Tests;

public class ExecTests
{
    [Fact]
    public void ProcessSpawning()
    {
        var desk = new TestDesktop();

        var reply = desk.WindowManager.Execute("dispatch exec sleep 1");

        Assert.Equal("ok", reply.Text);
        Assert.Equal("sleep 1", Assert.Single(reply.Effect.Launches).CommandLine);
    }
}
