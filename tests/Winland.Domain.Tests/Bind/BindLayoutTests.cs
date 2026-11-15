namespace Winland.Domain.Tests;

public class BindLayoutTests
{
    private static readonly KeyLayout ConfigLayout = KeyLayout.From(
        new Dictionary<int, string> { [0x51] = "q", [0x31] = "1" },
        virtualKeysByCode: new Dictionary<int, int> { [24] = 0x51, [10] = 0x31 });

    [Theory]
    [InlineData("Q", "й", 24)]
    [InlineData("Q", "A", 24)]
    [InlineData("1", "ampersand", 10)]
    public void BindsAlsoMatchThePhysicalKeyInTheConfigLayout(string bound, string activeName, int code)
    {
        var table = BindTable.From(ConfigParser.Parse($"bind = SUPER, {bound}, killactive\n", layout: ConfigLayout));
        var session = new BindSession();
        session.Decide(table, new KeyPressed("Super_L"));

        var pressed = session.Decide(table, new KeyPressed(activeName) { ScanCode = code });
        var released = session.Decide(table, new KeyReleased(activeName) { ScanCode = code });

        Assert.Equal("killactive", pressed.Dispatcher);
        Assert.True(pressed.Consumed);
        Assert.True(released.Consumed);
    }
}
