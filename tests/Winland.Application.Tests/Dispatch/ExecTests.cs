namespace Winland.Application.Tests;

[TestClass]
public class ExecTests
{
    [TestMethod]
    public void ProcessSpawning()
    {
        var desk = new TestDesktop();

        var reply = desk.WindowManager.Execute("exec sleep 1");

        Assert.AreEqual("ok", reply.Text);
        Assert.AreEqual("sleep 1", Assert.ContainsSingle(reply.Effect.Launches).CommandLine);
    }
}
