namespace Winland.Install.Tests;

[TestClass]
public sealed class TextFileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "winland-settings-" + Guid.NewGuid().ToString("N"));

    [TestMethod]
    public void WriteCreatesTheFolderAndDeleteRemovesOnlyTheFile()
    {
        var path = Path.Combine(directory, ".winland", "windows-settings.json");
        var file = new TextFile(path);

        Assert.IsNull(file.Read());

        file.Write("{\"windowArranging\":true}");

        Assert.AreEqual("{\"windowArranging\":true}", file.Read());
        Assert.IsTrue(Directory.Exists(Path.Combine(directory, ".winland")));

        file.Delete();

        Assert.IsNull(file.Read());
        Assert.IsTrue(Directory.Exists(Path.Combine(directory, ".winland")));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
