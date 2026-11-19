using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Winland.Daemon.Tests;

[SupportedOSPlatform("windows")]
[TestClass]
public class WindowsFileHandlesTests
{
    [TestMethod]
    public void SecurityIsReadFromTheOpenFolder()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(Path.GetTempPath(), "winland-handle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var user = identity.User!;
            var security = new DirectorySecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(security);

            using (var handle = WindowsFileHandles.Open(directory, WindowsFileHandles.ReadControl, FileShare.Read, followReparse: false))
            {
                Assert.IsTrue(WindowsAccessReader.TryRead(handle, out var access));
                Assert.AreEqual(user.Value, access.Owner);
                Assert.AreEqual(user.Value, Assert.ContainsSingle(access.Grants).Sid);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void TheFinalPathOfAFileIsItsPlainPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "winland-final-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "");
        try
        {
            using var handle = WindowsFileHandles.Open(path, WindowsFileHandles.ReadAttributes, FileShare.ReadWrite, followReparse: false);

            Assert.AreEqual(path, WindowsFileHandles.FinalPath(handle), StringComparer.OrdinalIgnoreCase);
            Assert.IsFalse(WindowsFileHandles.Attributes(handle).HasFlag(FileAttributes.Directory));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ANewFileCanBeWrittenReadAndDeletedButNotMadeTwice()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(Path.GetTempPath(), "winland-new-" + Guid.NewGuid().ToString("N") + ".txt");
        byte[] read;
        using (var handle = WindowsFileHandles.CreateNewFile(path, FileShare.None))
        {
            RandomAccess.Write(handle, "state"u8, 0);
            read = new byte[5];
            RandomAccess.Read(handle, read, 0);
            Assert.ThrowsExactly<IOException>(() => WindowsFileHandles.CreateNewFile(path, FileShare.ReadWrite));
            WindowsFileHandles.MarkForDeletion(handle);
        }

        Assert.AreSequenceEqual("state"u8.ToArray(), read);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    [DataRow(@"sub\state.json")]
    [DataRow("sub/state.json")]
    [DataRow("C:state.json")]
    public void ARenameToAnythingButOneNameIsRefused(string name)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(Path.GetTempPath(), "winland-rename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var folder = WindowsFileHandles.Open(directory, WindowsFileHandles.ReadAttributes, FileShare.ReadWrite | FileShare.Delete, followReparse: true);
            using var file = WindowsFileHandles.CreateNewFile(Path.Combine(directory, "state.tmp"), FileShare.None);

            Assert.ThrowsExactly<ArgumentException>(() => WindowsFileHandles.Rename(file, folder, name));
            WindowsFileHandles.Rename(file, folder, "state.json");
            Assert.AreEqual(Path.Combine(directory, "state.json"), WindowsFileHandles.FinalPath(file), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MissingArgumentsAreRefused()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.ThrowsExactly<ArgumentException>(() => WindowsFileHandles.Open(" ", WindowsFileHandles.ReadAttributes, FileShare.Read, followReparse: false));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsFileHandles.CreateNewFile(" ", FileShare.Read));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsFileHandles.Attributes(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsFileHandles.FinalPath(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsFileHandles.ReadSecurity(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsFileHandles.MarkForDeletion(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => WindowsFileHandles.Rename(null!, null!, "a"));
        Assert.ThrowsExactly<FileNotFoundException>(() => WindowsFileHandles.Open(Path.Combine(Path.GetTempPath(), "winland-missing-" + Guid.NewGuid().ToString("N")), WindowsFileHandles.ReadAttributes, FileShare.Read, followReparse: false));
    }

    [TestMethod]
    public void AJunctionOpenedWithoutFollowingShowsItselfAndItsOwnPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "winland-handle-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "link");
        Directory.CreateDirectory(target);
        try
        {
            Junctions.Create(link, target);

            using (var itself = WindowsFileHandles.Open(link, WindowsFileHandles.ReadAttributes, FileShare.ReadWrite, followReparse: false))
            using (var followed = WindowsFileHandles.Open(link, WindowsFileHandles.ReadAttributes, FileShare.ReadWrite, followReparse: true))
            {
                Assert.IsTrue(WindowsFileHandles.Attributes(itself).HasFlag(FileAttributes.ReparsePoint));
                Assert.IsFalse(WindowsFileHandles.Attributes(followed).HasFlag(FileAttributes.ReparsePoint));
                Assert.AreEqual(
                    Path.GetFileName(target),
                    Path.GetFileName(WindowsFileHandles.FinalPath(followed)));
            }
        }
        finally
        {
            Junctions.Delete(link);
            Directory.Delete(root, recursive: true);
        }
    }
}
