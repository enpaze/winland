namespace Winland.Domain.Tests;

[TestClass]
public class ConfigDispatchersTests
{
    private static readonly string[] Known = ["killactive", "workspace"];

    private const string Binds = """
        bind = SUPER, Q, killactive
        bind = SUPER, W, killactiv
        bind = SUPER, E, teleport
        """;

    [TestMethod]
    public void ABindForANameOutsideTheGivenOnesWarnsOnItsLine()
    {
        var document = ConfigParser.Parse(Binds, dispatchers: Known);

        Assert.ContainsSingle(document.Binds);
        Assert.AreSequenceEqual(
            [
                new ConfigWarning(2, "Unknown dispatcher 'killactiv'. Did you mean 'killactive'?"),
                new ConfigWarning(3, "Unknown dispatcher 'teleport'."),
            ],
            document.Warnings);
    }

    [TestMethod]
    public void ACaseThatDiffersStillMatches()
    {
        var document = ConfigParser.Parse("bind = SUPER, Q, KillActive", dispatchers: Known);

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
