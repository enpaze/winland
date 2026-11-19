namespace Winland.Domain.Tests;

[TestClass]
public class BindLayoutTests
{
    private static readonly KeyLayout ConfigLayout = KeyLayout.From(
        new Dictionary<int, string> { [0x51] = "q", [0x31] = "1" },
        virtualKeysByCode: new Dictionary<int, int> { [24] = 0x51, [10] = 0x31 });

    [TestMethod]
    [DataRow("Q", "й", 24)]
    [DataRow("Q", "A", 24)]
    [DataRow("1", "ampersand", 10)]
    public void BindsAlsoMatchThePhysicalKeyInTheConfigLayout(string bound, string activeName, int code)
    {
        var table = BindTable.From(ConfigParser.Parse($"bind = Win+{bound}, close\n", layout: ConfigLayout));
        var session = new BindSession();
        session.Decide(table, new KeyPressed("Super_L"));

        var pressed = session.Decide(table, new KeyPressed(activeName) { ScanCode = code });
        var released = session.Decide(table, new KeyReleased(activeName) { ScanCode = code });

        Assert.AreEqual("close", pressed.Dispatcher);
        Assert.IsTrue(pressed.Consumed);
        Assert.IsTrue(released.Consumed);
    }
}
