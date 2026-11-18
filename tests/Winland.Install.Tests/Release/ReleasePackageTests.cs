using System.IO.Compression;

namespace Winland.Install.Tests;

[TestClass]
public sealed class ReleasePackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "winland-zip-" + Guid.NewGuid().ToString("N"));
    private readonly ZipReleasePackage package = new();

    public ReleasePackageTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public void ExtractReturnsWinlandExe()
    {
        var archive = Zip(
            ("winland/winland.exe", "daemon"),
            ("winland/winland.dll", "lib"),
            ("version.json", "{\"version\":\"0.2.0\"}"));

        var executable = package.Extract(archive, Path.Combine(root, "stage"));

        Assert.AreEqual(Path.Combine(root, "stage", "winland", "winland.exe"), executable);
        Assert.AreEqual("daemon", File.ReadAllText(executable));
    }

    [TestMethod]
    public void ExtractRefusesAnEntryThatLeavesTheFolder()
    {
        var archive = Zip(
            ("winland/winland.exe", "daemon"),
            ("../outside.txt", "no"));

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => package.Extract(archive, Path.Combine(root, "stage")));

        Assert.Contains("Refusing to unpack", error.Message, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(root, "outside.txt")));
    }

    [TestMethod]
    public void ExtractRefusesAZipWithNoDaemon()
    {
        var archive = Zip(("winland/winland.dll", "lib"));

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => package.Extract(archive, Path.Combine(root, "stage")));

        Assert.Contains("winland.exe", error.Message, StringComparison.Ordinal);
    }

    private static byte[] Zip(params (string Name, string Text)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(text);
            }
        }

        return memory.ToArray();
    }
}
