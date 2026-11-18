using System.Security.AccessControl;
using System.Security.Principal;

namespace Winland.Daemon.Tests;

[TestClass]
public sealed class FileConfigSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "winland-source-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _locked = [];

    public FileConfigSourceTests() => Directory.CreateDirectory(_root);

    [TestMethod]
    public void APatternInTheConfigFolderDoesNotLookIntoSubfolders()
    {
        Write("a.conf");
        Write(@"sub\b.conf");
        Lock("locked");

        var matches = new FileConfigSource(_root).Match("*.conf");

        Assert.AreSequenceEqual([Path.Combine(_root, "a.conf")], matches);
    }

    [TestMethod]
    public void AnUnreadableFolderDoesNotHideTheOtherMatches()
    {
        Write(@"one\x.conf");
        Write(@"two\y.conf");
        Write(@"two\deep\z.conf");
        Lock("locked");

        var matches = new FileConfigSource(_root).Match(@"*\*.conf");

        Assert.AreSequenceEqual([Path.Combine(_root, "one", "x.conf"), Path.Combine(_root, "two", "y.conf")], matches);
    }

    [TestMethod]
    [DataRow(@"C:\*.conf", @"C:\", 0)]
    [DataRow(@"C:\conf\*.conf", @"C:\conf", 0)]
    [DataRow(@"C:\conf\*\*.conf", @"C:\conf", 1)]
    [DataRow(@"C:\conf\**\x\*.conf", @"C:\conf", 2)]
    [DataRow(@"C:\conf\a?\*.conf", @"C:\conf", 1)]
    [DataRow(@"C:/conf/*/*.conf", @"C:\conf", 1)]
    [DataRow(@"\*.conf", @"\", 0)]
    [DataRow(@"*\x.conf", "", 0)]
    public void TheSearchStartsAtTheLastFolderWithoutAWildcard(string pattern, string root, int depth)
    {
        var plan = FileConfigSource.Plan(pattern);

        Assert.AreEqual((root, depth), plan);
    }

    [TestMethod]
    public void ARelativeSourceIsReadFromTheConfigFolder()
    {
        File.WriteAllText(Path.Combine(_root, "colors.conf"), "$accent = red");

        var text = new FileConfigSource(_root).Read("colors.conf");

        Assert.AreEqual("$accent = red", text);
    }

    [TestMethod]
    public void AFileAnotherProgramHasOpenIsStillRead()
    {
        var path = Path.Combine(_root, "winland.conf");
        File.WriteAllText(path, "general { gaps_in = 5 }");
        using var editor = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var text = new FileConfigSource(_root).Read(path);

        Assert.AreEqual("general { gaps_in = 5 }", text);
    }

    [TestMethod]
    public void AByteOrderMarkSelectsTheEncoding()
    {
        var path = Path.Combine(_root, "winland.conf");
        File.WriteAllText(path, "$name = Åsa", System.Text.Encoding.Unicode);

        var text = new FileConfigSource(_root).Read(path);

        Assert.AreEqual("$name = Åsa", text);
    }

    [TestMethod]
    public void AMissingFileReadsAsNothing()
    {
        Assert.IsNull(new FileConfigSource(_root).Read("missing.conf"));
    }

    public void Dispose()
    {
        foreach (var folder in _locked)
            Unlock(folder);

        Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }

    private void Lock(string relative)
    {
        var folder = Path.Combine(_root, relative);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "hidden.conf"), "");
        var info = new DirectoryInfo(folder);
        var security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(Me(), FileSystemRights.ListDirectory, AccessControlType.Deny));
        info.SetAccessControl(security);
        _locked.Add(folder);
    }

    private static void Unlock(string folder)
    {
        var info = new DirectoryInfo(folder);
        var security = info.GetAccessControl();
        security.RemoveAccessRule(new FileSystemAccessRule(Me(), FileSystemRights.ListDirectory, AccessControlType.Deny));
        info.SetAccessControl(security);
    }

    private static SecurityIdentifier Me()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User!;
    }
}
