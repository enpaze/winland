namespace Winland.Application.Tests;

[TestClass]
public class InitialMaximizeTests
{
    [TestMethod]
    public void AMaximizeRequestFromAWindowShownMaximizedIsIgnored()
    {
        var desk = new TestDesktop();
        var window = desk.Spawn("xdg-initial-maximize", shownMaximized: true);
        desk.Handle(new WindowMaximizeRequested(window));

        var client = Assert.ContainsSingle(desk.Clients());
        Assert.AreEqual("xdg-initial-maximize", TestDesktop.ClassOf(client));
        Assert.IsTrue(client.GetProperty("mapped").GetBoolean());
        Assert.AreEqual(0, TestDesktop.Fullscreen(client));
        Assert.AreEqual(0, TestDesktop.FullscreenClient(client));
    }
}
