namespace Winland.Domain.Tests;

[TestClass]
public class ConfigDispatchersTests
{
    private static readonly string[] Known = ["close", "workspace"];

    private const string Binds = """
        bind = Win+Q, close
        bind = Win+W, closs
        bind = Win+E, teleport
        """;

    [TestMethod]
    public void ABindForANameOutsideTheGivenOnesWarnsOnItsLine()
    {
        var document = ConfigParser.Parse(Binds, dispatchers: Known);

        Assert.ContainsSingle(document.Binds);
        Assert.AreSequenceEqual(
            [
                new ConfigWarning(2, "Unknown dispatcher 'closs'. Did you mean 'close'?"),
                new ConfigWarning(3, "Unknown dispatcher 'teleport'."),
            ],
            document.Warnings);
    }

    [TestMethod]
    public void ACaseThatDiffersStillMatches()
    {
        var document = ConfigParser.Parse("bind = Win+Q, close", dispatchers: Known);

        Assert.IsEmpty(document.Warnings);
        Assert.ContainsSingle(document.Binds);
    }

    [TestMethod]
    public void WithNoNamesGivenABindIsNotChecked()
    {
        var document = ConfigParser.Parse(Binds);

        Assert.IsEmpty(document.Warnings);
        Assert.AreEqual(3, document.Binds.Count);
    }
}
