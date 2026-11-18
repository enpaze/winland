namespace Winland.Daemon.Tests;

[TestClass]
public sealed class ShellShortcutTests : IDisposable
{
    private readonly string link = Path.Combine(Path.GetTempPath(), "winland-shortcut-" + Guid.NewGuid().ToString("N") + ".lnk");

    public void Dispose()
    {
        if (File.Exists(link))
            File.Delete(link);
    }

    [TestMethod]
    [DataRow(@"C:\Program Files\Winland\winland.exe")]
    [DataRow(@"C:\Program Files\Winland2\winland.exe")]
    public void TheShortcutCarriesTheAppUserModelId(string target)
    {
        ShellShortcut.Write(
            link,
            target,
            "--no-elevate",
            "Winland.Winland");

        var bytes = File.ReadAllBytes(link);
        var appUserModelId = System.Text.Encoding.Unicode.GetBytes("Winland.Winland");
        Assert.IsTrue(bytes.AsSpan().IndexOf(appUserModelId) >= 0, "The shortcut must contain the AppUserModelID.");
    }
}
