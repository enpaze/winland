namespace Winland.Daemon.Tests;

[TestClass]
public sealed class ConfigFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "winland-config-file-" + Guid.NewGuid().ToString("N"));

    public ConfigFileTests() => Directory.CreateDirectory(_root);

    [TestMethod]
    public void ABindForAnUnknownDispatcherWarnsWithItsLineAndTheNameItIsCloseTo()
    {
        var path = Path.Combine(_root, "winland.conf");
        File.WriteAllText(path, "bind = SUPER, Q, killactive\nbind = SUPER, W, killactiv\n");

        var document = ConfigFile.Read(path);

        Assert.ContainsSingle(document.Binds);
        var warning = Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(2, warning.Line);
        Assert.AreEqual("Unknown dispatcher 'killactiv'. Did you mean 'killactive'?", warning.Message);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
