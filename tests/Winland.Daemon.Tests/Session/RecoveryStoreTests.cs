namespace Winland.Daemon.Tests;
[TestClass]
public sealed class RecoveryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "winland-state-" + Guid.NewGuid().ToString("N"));

    public RecoveryStoreTests() => Directory.CreateDirectory(_root);

    [TestMethod]
    public void ASavedSnapshotIsReadBackAndDeleted()
    {
        var path = Path.Combine(_root, "Winland", "state.json");
        var store = new RecoveryStore(path, _ => null);
        store.Arm();

        store.Save(Snapshot(7));
        var read = store.Read();
        store.Delete();

        Assert.IsNotNull(read);
        Assert.AreEqual(7, Assert.ContainsSingle(read.Clients).Window.Value);
        Assert.IsFalse(File.Exists(path));
        Assert.IsFalse(File.Exists(path + ".tmp"));
    }

    [TestMethod]
    public void ACleanExitKeepsItsLastSnapshotAndNothingAfter()
    {
        var path = Path.Combine(_root, "state.json");
        var store = new RecoveryStore(path);
        store.Arm();
        store.Save(Snapshot(1));

        store.Keep(Snapshot(2));
        store.Save(Snapshot(3));

        Assert.AreEqual(2, Assert.ContainsSingle(new RecoveryStore(path).Read()!.Clients).Window.Value);
    }

    [TestMethod]
    public void AnExitBeforeArmingKeepsThePreviousFile()
    {
        var path = Path.Combine(_root, "state.json");
        var previous = new RecoveryStore(path);
        previous.Arm();
        previous.Save(Snapshot(1));

        new RecoveryStore(path).Keep(Snapshot(2));

        Assert.AreEqual(1, Assert.ContainsSingle(new RecoveryStore(path).Read()!.Clients).Window.Value);
    }

    [TestMethod]
    public void ASecondSaveReplacesTheFirst()
    {
        var path = Path.Combine(_root, "state.json");
        var store = new RecoveryStore(path);
        store.Arm();

        store.Save(Snapshot(1));
        store.Save(Snapshot(2));

        Assert.AreEqual(2, Assert.ContainsSingle(new RecoveryStore(path).Read()!.Clients).Window.Value);
        Assert.AreSequenceEqual(["state.json"], Directory.GetFiles(_root).Select(Path.GetFileName));
    }

    [TestMethod]
    public void AFolderTheCheckRefusesIsNeitherWrittenNorRead()
    {
        var path = Path.Combine(_root, "state.json");
        File.WriteAllText(path, Snapshot(3).ToJson());
        var store = new RecoveryStore(path, _ => "only administrators can empty it");
        store.Arm();

        store.Save(Snapshot(4));
        var read = store.Read();
        store.Delete();

        Assert.IsNull(read);
        Assert.AreEqual(3, Assert.ContainsSingle(new RecoveryStore(path).Read()!.Clients).Window.Value);
    }

    [TestMethod]
    public void AJunctionedFolderIsCheckedWhereItLeads()
    {
        var target = Path.Combine(_root, "target");
        var link = Path.Combine(_root, "Winland");
        Directory.CreateDirectory(target);
        Junction(link, target);
        var checkedFolders = new List<string>();
        var store = new RecoveryStore(Path.Combine(link, "state.json"), folder =>
        {
            checkedFolders.Add(WindowsFileHandles.FinalPath(folder));
            return "refused";
        });
        store.Arm();

        store.Save(Snapshot(5));

        Assert.AreEqual(Path.GetFileName(target), Path.GetFileName(Assert.ContainsSingle(checkedFolders)));
        Assert.IsEmpty(Directory.GetFiles(target));
    }

    [TestMethod]
    public void ALinkAtTheFilesNameIsNotFollowed()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "kept.txt"), "kept");
        var folder = Path.Combine(_root, "Winland");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "state.json");
        Junction(path, elsewhere);
        Junction(path + ".tmp", elsewhere);
        var store = new RecoveryStore(path);

        var read = store.Read();
        store.Arm();
        store.Save(Snapshot(6));
        var saved = new RecoveryStore(path).Read();
        store.Delete();

        Assert.IsNull(read);
        Assert.AreEqual(6, Assert.ContainsSingle(saved!.Clients).Window.Value);
        Assert.IsFalse(Directory.Exists(path));
        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual("kept", File.ReadAllText(Path.Combine(elsewhere, "kept.txt")));
    }

    [TestMethod]
    public void OnlyAnArmedOpenStoreWrites()
    {
        var path = Path.Combine(_root, "state.json");
        var store = new RecoveryStore(path);

        store.Save(Snapshot(1));
        var beforeArm = File.Exists(path);
        store.Arm();
        store.Delete();
        store.Arm();
        store.Save(Snapshot(2));

        Assert.IsFalse(beforeArm);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void AnUnchangedSnapshotIsNotWrittenAgain()
    {
        var path = Path.Combine(_root, "state.json");
        var store = new RecoveryStore(path);
        store.Arm();

        store.Save(Snapshot(1));
        File.WriteAllText(path, Snapshot(9).ToJson());
        store.Save(Snapshot(1));

        Assert.AreEqual(9, Assert.ContainsSingle(store.Read()!.Clients).Window.Value);
    }

    [TestMethod]
    public void AMissingOrDamagedFileReadsAsNothing()
    {
        var path = Path.Combine(_root, "state.json");
        var store = new RecoveryStore(path);
        var stderr = Console.Error;
        using var log = new StringWriter();
        Console.SetError(log);
        try
        {
            Assert.IsNull(store.Read());
            Assert.IsEmpty(log.ToString());
            File.WriteAllText(path, "{ not json");

            Assert.IsNull(store.Read());
            Assert.AreEqual($"Could not read '{path}': invalid recovery data.{Environment.NewLine}", log.ToString());
        }
        finally
        {
            Console.SetError(stderr);
        }
    }

    [TestMethod]
    public void AFileInPlaceOfTheFolderIsNotUsed()
    {
        var blocker = Path.Combine(_root, "Winland");
        File.WriteAllText(blocker, "not a folder");
        var store = new RecoveryStore(Path.Combine(blocker, "state.json"));
        store.Arm();

        store.Save(Snapshot(1));
        var read = store.Read();
        store.Delete();

        Assert.IsNull(read);
        Assert.AreEqual("not a folder", File.ReadAllText(blocker));
    }

    [TestMethod]
    public void APathWithoutAFolderIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RecoveryStore(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new RecoveryStore(@"C:\"));
    }

    public void Dispose()
    {
        foreach (var directory in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories).Reverse())
        {
            if (new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                Directory.Delete(directory);
        }

        Directory.Delete(_root, recursive: true);
    }

    private static RecoverySnapshot Snapshot(long window) =>
        new([new RecoveredClient(new WindowId(window), WorkspaceId.Initial, MonitorId.Primary, false, "app", "App", 1)]);

    private static void Junction(string link, string target)
    {
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target })
            start.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode);
    }
}
