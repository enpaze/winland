using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Winland.Daemon.Tests;

[SupportedOSPlatform("windows")]
[TestClass]
public sealed class WindowsAccessReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "winland-access-" + Guid.NewGuid().ToString("N"));
    private string? _locked;

    public WindowsAccessReaderTests() => Directory.CreateDirectory(_root);

    [TestMethod]
    public void TheTreeIsTheFolderAndEverythingInIt()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "Plugin.dll"), "");
        var hidden = Path.Combine(_root, "hidden.dll");
        File.WriteAllText(hidden, "");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var tree = WindowsAccessReader.Tree(_root);

        Assert.AreEqual(_root, tree[0].Path);
        Assert.AreSequenceEqual(
            [_root, hidden, Path.Combine(_root, "sub"), Path.Combine(_root, "sub", "Plugin.dll")],
            tree.Select(entry => entry.Path).Order(StringComparer.OrdinalIgnoreCase));
        foreach (var entry in tree)
{
    Assert.IsNotNull(entry.Access);
}
    }

    [TestMethod]
    public void AFolderThatCannotBeListedReadsAsUnknown()
    {
        if (!OperatingSystem.IsWindows())
            return;

        _locked = Path.Combine(_root, "locked");
        Directory.CreateDirectory(_locked);
        File.WriteAllText(Path.Combine(_locked, "Plugin.dll"), "");
        var info = new DirectoryInfo(_locked);
        var security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(Me(), FileSystemRights.ListDirectory, AccessControlType.Deny));
        info.SetAccessControl(security);

        var tree = WindowsAccessReader.Tree(_root);

        Assert.Contains(entry => entry.Path == _root && entry.Access is null, tree);
    }

    [TestMethod]
    public void AnOpenFileAndAFolderCanBeRead()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(_root, "approved.json");
        File.WriteAllText(path, "[]");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.IsTrue(WindowsAccessReader.TryRead(stream, out var opened));
        Assert.IsTrue(WindowsAccessReader.TryRead(_root, out var folder, out var error));
        Assert.IsTrue(WindowsAccessReader.TryRead(path, out var file, out _));
        Assert.IsNull(error);
        Assert.IsFalse(string.IsNullOrEmpty(opened.Owner));
        Assert.AreEqual(file.Owner, opened.Owner);
        Assert.IsFalse(string.IsNullOrEmpty(folder.Owner));
        Assert.IsNotEmpty(opened.Grants);
    }

    [TestMethod]
    public void APathThatIsNotThereCannotBeRead()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var missing = Path.Combine(_root, "missing", "Plugin.dll");

        Assert.IsFalse(WindowsAccessReader.TryRead(missing, out _, out var error));
        Assert.AreEqual($"Could not verify that '{missing}' is protected from non-administrators.", error);
    }

    [TestMethod]
    public void TheAncestorsAreNearestFirst()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var file = Path.Combine(_root, "sub", "Plugin.dll");

        var ancestors = WindowsAccessReader.Ancestors(file).Select(entry => entry.Path).ToList();

        Assert.AreEqual(Path.Combine(_root, "sub"), ancestors[0]);
        Assert.AreEqual(_root, ancestors[1]);
        Assert.AreEqual(Path.GetPathRoot(_root), ancestors[^1]);
    }

    [TestMethod]
    public void MissingArgumentsAreRefused()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.ThrowsExactly<ArgumentException>(() => WindowsAccessReader.TryRead(" ", out _, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsAccessReader.TryRead((FileStream)null!, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsAccessReader.TryRead((Microsoft.Win32.SafeHandles.SafeFileHandle)null!, out _));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsAccessReader.FirstNonAdministrator(" "));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsAccessReader.Tree(" "));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsAccessReader.FirstNonAdministratorAbove(" "));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsAccessReader.Ancestors(" "));
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows() && _locked is not null)
        {
            var info = new DirectoryInfo(_locked);
            var security = info.GetAccessControl();
            security.RemoveAccessRule(new FileSystemAccessRule(Me(), FileSystemRights.ListDirectory, AccessControlType.Deny));
            info.SetAccessControl(security);
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(_root, recursive: true);
    }

    private static SecurityIdentifier Me()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User!;
    }
}
