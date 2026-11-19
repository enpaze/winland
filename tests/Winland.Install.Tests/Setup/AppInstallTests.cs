namespace Winland.Install.Tests;

[TestClass]
public class AppInstallTests
{
    private const string Source = @"C:\Users\me\Downloads\Winland\winland\winland.exe";

    private const string Machine = @"C:\Program Files\Winland";

    private const string User = @"C:\Users\me\AppData\Local\Programs\Winland";

    private const string CommonPrograms = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs";

    private const string UserPrograms = @"C:\Users\me\AppData\Roaming\Microsoft\Windows\Start Menu\Programs";

    private static readonly string Config = InstallLocations.Config(@"C:\Users\me");

    private readonly MemoryFileTree files = new();
    private readonly MemoryTaskCatalog tasks = new();
    private readonly MemoryRegistry registry = new();
    private readonly MemoryStartMenu shortcuts = new();
    private readonly MemorySession session = new();
    private readonly AppInstall install;

    public AppInstallTests()
    {
        install = new AppInstall(Layout(), tasks, files, registry, shortcuts, session);
        files.Seed(Source, [1]);
        files.Seed(InstallLocations.Launcher(Source), [3]);
        files.Seed(@"C:\Users\me\Downloads\Winland\winland\winland.dll", [2]);
        files.Seed(@"C:\Users\me\Downloads\Winland\winland\Winland.Domain.dll", [5]);
        files.Seed(Path.Combine(Config, "winland.conf"), [9]);
    }

    [TestMethod]
    public void MachineInstallCopiesTheFolderAndPointsTheTaskAtProgramFiles()
    {
        files.Seed(Path.Combine(User, "winland.exe"), [8]);
        registry.SetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue, "old", expandable: false);

        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(Machine, "winland.exe")));
        TestSequence.Equal((byte[])[2], files.Read(Path.Combine(Machine, "winland.dll")));
        TestSequence.Equal((byte[])[5], files.Read(Path.Combine(Machine, "Winland.Domain.dll")));
        TestSequence.Equal((byte[])[1], files.Read(Source));

        var task = tasks.Only();
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), task.Executable);
        Assert.AreEqual(@"DESKTOP\dante", task.User);
        Assert.DoesNotContain(Source, task.Executable, StringComparison.OrdinalIgnoreCase);

        var shortcut = Assert.ContainsSingle(shortcuts.Shortcuts);
        Assert.AreEqual(Path.Combine(CommonPrograms, "Winland.lnk"), shortcut.LinkPath);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), shortcut.Target);
        Assert.AreEqual("", shortcut.Arguments);
        Assert.AreEqual(InstallLocations.AppUserModelId, shortcut.AppUserModelId);

        Assert.AreEqual("Winland", Text(RegistryRoot.Machine, "DisplayName"));
        Assert.AreEqual(
            "\"" + Path.Combine(Machine, "winland.exe") + "\" uninstall",
            Text(RegistryRoot.Machine, "UninstallString"));
        Assert.AreEqual(Machine, Text(RegistryRoot.Machine, "InstallLocation"));
        Assert.AreEqual("0.1.0", Text(RegistryRoot.Machine, "DisplayVersion"));
        Assert.AreEqual(1, Dword(RegistryRoot.Machine, "NoModify"));
        Assert.AreEqual(Machine, Text(RegistryRoot.Machine, InstallLocations.PathValue, InstallLocations.MachinePathKey));
        Assert.IsTrue(registry.Values[(RegistryRoot.Machine, InstallLocations.MachinePathKey, InstallLocations.PathValue)].Expandable);

        Assert.IsTrue(files.Exists(Path.Combine(User, "winland.exe")));
        Assert.AreEqual("old", registry.GetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue));
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Config, "winland.conf")));
        Assert.AreSequenceEqual(["stop", "start"], session.Operations);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), session.Started);
        Assert.IsEmpty(session.StartedArguments);
    }

    [TestMethod]
    public void AnUpgradeRemovesFilesTheNewReleaseDoesNotShip()
    {
        files.Seed(Path.Combine(Machine, "Dropped.Dependency.dll"), [7]);
        files.Seed(Path.Combine(Machine, "winland.exe.winland-old"), [6]);

        install.InstallMachine(Source, @"DESKTOP\dante", "0.2.0");

        Assert.IsFalse(files.Exists(Path.Combine(Machine, "Dropped.Dependency.dll")));
        Assert.IsTrue(files.Exists(Path.Combine(Machine, "winland.exe.winland-old")));
        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(Machine, "winland.exe")));
    }

    [TestMethod]
    public void MachineInstallKeepsTheRestOfTheMachinePath()
    {
        registry.SetString(
            RegistryRoot.Machine,
            InstallLocations.MachinePathKey,
            InstallLocations.PathValue,
            @"%SystemRoot%\system32;C:\Tools",
            expandable: true);

        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        Assert.AreEqual(@"%SystemRoot%\system32;C:\Tools;" + Machine, Text(RegistryRoot.Machine, InstallLocations.PathValue, InstallLocations.MachinePathKey));
    }

    [TestMethod]
    public void UserInstallUsesTheRunKeyAndNeverTheTask()
    {
        install.InstallForUser(Source, "0.1.0");

        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(User, "winland.exe")));
        Assert.IsFalse(tasks.Contains("Winland"));
        Assert.IsEmpty(tasks.Operations);
        Assert.AreEqual(
            "\"" + Path.Combine(User, "winland.exe") + "\" --no-elevate",
            registry.GetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue));

        var shortcut = Assert.ContainsSingle(shortcuts.Shortcuts);
        Assert.AreEqual(Path.Combine(UserPrograms, "Winland.lnk"), shortcut.LinkPath);
        Assert.AreEqual("--no-elevate", shortcut.Arguments);
        Assert.AreEqual(InstallLocations.AppUserModelId, shortcut.AppUserModelId);
        Assert.AreEqual(
            "\"" + Path.Combine(User, "winland.exe") + "\" uninstall",
            Text(RegistryRoot.User, "UninstallString"));
        Assert.AreEqual(User, Text(RegistryRoot.User, InstallLocations.PathValue, InstallLocations.UserPathKey));
        Assert.IsNull(registry.GetString(RegistryRoot.Machine, InstallLocations.UninstallKey, "DisplayName"));
        Assert.AreSequenceEqual(["stop", "start"], session.Operations);
        Assert.AreSequenceEqual(["--no-elevate"], session.StartedArguments);
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Config, "winland.conf")));
    }

    [TestMethod]
    public void MachineInstallWithoutLogonRemovesTheTaskAnEarlierInstallMade()
    {
        tasks.Seed(LogonTaskDefinition.Build(Path.Combine(Machine, "winland.exe"), @"DESKTOP\dante"));

        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0", logon: false);

        Assert.IsFalse(tasks.Contains(LogonTaskDefinition.TaskName));
        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(Machine, "winland.exe")));
        Assert.AreSequenceEqual(["stop", "start"], session.Operations);
    }

    [TestMethod]
    public void MachineInstallWithoutStartLeavesWinlandStopped()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0", start: false);

        Assert.AreSequenceEqual(["stop"], session.Operations);
        Assert.IsNull(session.Started);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), tasks.Only().Executable);
    }

    [TestMethod]
    public void UserInstallWithoutLogonOrStartLeavesNoRunKeyAndWinlandStopped()
    {
        registry.SetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue, "old", expandable: false);

        install.InstallForUser(Source, "0.1.0", logon: false, start: false);

        Assert.IsNull(registry.GetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue));
        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(User, "winland.exe")));
        Assert.AreSequenceEqual(["stop"], session.Operations);
    }

    [TestMethod]
    public void AFailedReplaceKeepsThePreviousBuildStampAndRestartsWinland()
    {
        files.Seed(Path.Combine(Machine, "winland.exe"), [8]);
        files.Seed(Path.Combine(Machine, "Winland.Domain.dll"), [9]);
        files.BeforeCopy = source =>
        {
            if (Path.GetFileName(source) == "winland.dll")
                throw new IOException("disk full");
        };

        Assert.ThrowsExactly<IOException>(() => install.ReplaceInstalled(Source, Machine, perUser: false, "0.2.0"));

        TestSequence.Equal((byte[])[8], files.Read(Path.Combine(Machine, "winland.exe")));
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Machine, "Winland.Domain.dll")));
        Assert.AreSequenceEqual(["stop", "start"], session.Operations);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), session.Started);
        Assert.IsNull(Text(RegistryRoot.Machine, "DisplayVersion"));
    }

    [TestMethod]
    public void ReplaceInstalledLeavesWinlandStoppedWhenItWasNotRunning()
    {
        session.Running = false;

        install.ReplaceInstalled(Source, Machine, perUser: false, "0.2.0");

        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(Machine, "winland.exe")));
        Assert.AreSequenceEqual(["stop"], session.Operations);
        Assert.IsNull(session.Started);
    }

    [TestMethod]
    public void ALinkInTheCopiedFolderIsRefusedBeforeWinlandIsStopped()
    {
        files.Seed(@"C:\Users\me\Downloads\Winland\winland\swapped.exe", [1], reparsePoint: true);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0"));

        Assert.Contains("it is a link", error.Message, StringComparison.Ordinal);
        Assert.IsEmpty(session.Operations);
        Assert.IsFalse(tasks.Contains("Winland"));
        Assert.IsFalse(files.DirectoryExists(Machine));
    }

    [TestMethod]
    public void AFolderWithoutWinlandIsRefused()
    {
        var session = new MemorySession();
        var install = new AppInstall(Layout(), new MemoryTaskCatalog(), new MemoryFileTree(), new MemoryRegistry(), new MemoryStartMenu(), session);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0"));

        Assert.Contains("winland.exe", error.Message, StringComparison.Ordinal);
        Assert.IsEmpty(session.Operations);
    }

    [TestMethod]
    public void ReplaceInstalledCopiesIntoTheRunningFolderAndKeepsTheConfig()
    {
        files.Seed(Path.Combine(Machine, "winland.exe"), [8]);
        files.Seed(Path.Combine(User, "winland.exe"), [7]);
        tasks.Seed(LogonTaskDefinition.Build(Path.Combine(Machine, "winland.exe"), @"DESKTOP\dante"));
        registry.SetString(RegistryRoot.Machine, InstallLocations.MachinePathKey, InstallLocations.PathValue, Machine, expandable: true);
        registry.SetString(RegistryRoot.Machine, InstallLocations.UninstallKey, "DisplayVersion", "0.1.0", expandable: false);

        install.ReplaceInstalled(Source, Machine, perUser: false, "0.2.0");

        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(Machine, "winland.exe")));
        TestSequence.Equal((byte[])[2], files.Read(Path.Combine(Machine, "winland.dll")));
        TestSequence.Equal((byte[])[7], files.Read(Path.Combine(User, "winland.exe")));
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Config, "winland.conf")));
        Assert.AreSequenceEqual(["stop", "start"], session.Operations);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), session.Started);
        Assert.IsEmpty(session.StartedArguments);
        Assert.AreEqual("0.2.0", Text(RegistryRoot.Machine, "DisplayVersion"));
        Assert.IsEmpty(tasks.Operations);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), tasks.Only().Executable);
        Assert.AreEqual(Machine, Text(RegistryRoot.Machine, InstallLocations.PathValue, InstallLocations.MachinePathKey));
    }

    [TestMethod]
    public void ReplaceInstalledForTheUserStartsWithoutElevation()
    {
        install.ReplaceInstalled(Source, User, perUser: true, "0.2.0");

        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(User, "winland.exe")));
        Assert.AreSequenceEqual(["--no-elevate"], session.StartedArguments);
        Assert.AreEqual("0.2.0", Text(RegistryRoot.User, "DisplayVersion"));
        Assert.IsNull(registry.GetString(RegistryRoot.Machine, InstallLocations.UninstallKey, "DisplayVersion"));
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Config, "winland.conf")));
        Assert.IsEmpty(tasks.Operations);
    }

    [TestMethod]
    public void ReplaceInstalledRefusesAFolderWithoutWinlandBeforeStopping()
    {
        var waited = new MemorySession();
        var replacing = new AppInstall(Layout(), new MemoryTaskCatalog(), new MemoryFileTree(), new MemoryRegistry(), new MemoryStartMenu(), waited);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => replacing.ReplaceInstalled(Source, Machine, perUser: false, "0.2.0"));

        Assert.Contains("winland.exe", error.Message, StringComparison.Ordinal);
        Assert.IsEmpty(waited.Operations);
    }

    [TestMethod]
    public void MachineUpdateWithoutTheLauncherLeavesTheRunningInstallAlone()
    {
        files.DeleteFile(InstallLocations.Launcher(Source));
        files.Seed(Path.Combine(Machine, "winland.exe"), [8]);
        files.Seed(Path.Combine(Machine, InstallLocations.LauncherName), [9]);
        registry.SetString(RegistryRoot.Machine, InstallLocations.UninstallKey, "DisplayVersion", "0.1.0", expandable: false);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            install.ReplaceInstalled(Source, Machine, perUser: false, "0.2.0"));

        Assert.Contains(InstallLocations.LauncherName, error.Message, StringComparison.Ordinal);
        Assert.IsEmpty(session.Operations);
        TestSequence.Equal((byte[])[8], files.Read(Path.Combine(Machine, "winland.exe")));
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Machine, InstallLocations.LauncherName)));
        Assert.AreEqual("0.1.0", Text(RegistryRoot.Machine, "DisplayVersion"));
    }

    [TestMethod]
    public void InstallingAgainFromTheInstalledCopyRefreshesTheTask()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");
        var again = new MemorySession();
        var refresh = new AppInstall(Layout(), tasks, files, registry, shortcuts, again);

        refresh.InstallMachine(Path.Combine(Machine, "winland.exe"), @"DESKTOP\dante", "0.1.0");

        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), tasks.Only().Executable);
        Assert.AreEqual("replace", tasks.Operations[^1]);
        TestSequence.Equal((byte[])[1], files.Read(Path.Combine(Machine, "winland.exe")));
        Assert.AreSequenceEqual(["stop", "start"], again.Operations);
    }

    [TestMethod]
    public void MachineUninstallLeavesUserFilesAndSettingsForTheUnelevatedCaller()
    {
        var restored = false;
        var watched = new AppInstall(Layout(), tasks, files, registry, shortcuts, session, new WatchingRestore(() => restored = true));
        files.Seed(Path.Combine(User, "winland.exe"), [8]);

        watched.Uninstall(purge: true, machine: true);

        Assert.IsFalse(restored);
        Assert.IsTrue(files.Exists(Path.Combine(User, "winland.exe")));
        Assert.IsTrue(files.Exists(Path.Combine(Config, "winland.conf")));

        watched.Uninstall(purge: true, machine: false);

        Assert.IsTrue(restored);
        Assert.IsFalse(files.DirectoryExists(User));
        Assert.IsFalse(files.DirectoryExists(Config));
    }

    [TestMethod]
    public void UninstallRemovesTheInstallAndKeepsTheConfig()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");
        files.Seed(Path.Combine(Config, "state.json"), [7]);
        files.Seed(Path.Combine(Config, "winland.log"), [8]);

        install.Uninstall(purge: false, machine: true);

        Assert.IsFalse(files.DirectoryExists(Machine));
        Assert.IsFalse(tasks.Contains("Winland"));
        Assert.IsEmpty(shortcuts.Shortcuts);
        Assert.IsNull(registry.GetString(RegistryRoot.Machine, InstallLocations.UninstallKey, "DisplayName"));
        Assert.AreEqual("", Text(RegistryRoot.Machine, InstallLocations.PathValue, InstallLocations.MachinePathKey));
        TestSequence.Equal((byte[])[9], files.Read(Path.Combine(Config, "winland.conf")));
        TestSequence.Equal((byte[])[7], files.Read(Path.Combine(Config, "state.json")));
        TestSequence.Equal((byte[])[8], files.Read(Path.Combine(Config, "winland.log")));
    }

    [TestMethod]
    public void PurgeRemovesTheConfigTheLogsAndTheState()
    {
        install.InstallForUser(Source, "0.1.0");
        files.Seed(Path.Combine(Config, "state.json"), [7]);
        files.Seed(Path.Combine(Config, "winland.log"), [8]);

        install.Uninstall(purge: true, machine: false);

        Assert.IsFalse(files.DirectoryExists(User));
        Assert.IsFalse(files.DirectoryExists(Config));
        Assert.IsFalse(files.Exists(Path.Combine(Config, "winland.conf")));
        Assert.IsFalse(files.Exists(Path.Combine(Config, "state.json")));
        Assert.IsFalse(files.Exists(Path.Combine(Config, "winland.log")));
        Assert.IsNull(registry.GetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue));
    }

    [TestMethod]
    public void APerUserUninstallLeavesTheMachineInstall()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        install.Uninstall(purge: false, machine: false);

        Assert.IsTrue(files.Exists(Path.Combine(Machine, "winland.exe")));
        Assert.IsTrue(tasks.Contains("Winland"));
        Assert.AreEqual(Machine, Text(RegistryRoot.Machine, InstallLocations.PathValue, InstallLocations.MachinePathKey));
    }

    [TestMethod]
    public void UninstallRestoresRecordedSettingsBeforePurgeDeletesTheFolder()
    {
        var folderStillThere = false;
        var watching = new WatchingRestore(() => folderStillThere = files.Exists(Path.Combine(Config, "winland.conf")));
        var watched = new AppInstall(Layout(), tasks, files, registry, shortcuts, session, watching);

        watched.Uninstall(purge: true, machine: false);

        Assert.IsTrue(folderStillThere);
        Assert.IsFalse(files.Exists(Path.Combine(Config, "winland.conf")));
    }

    [TestMethod]
    public void UninstallRemovesOnlyTheInstallFolderFromThePath()
    {
        registry.SetString(
            RegistryRoot.User,
            InstallLocations.UserPathKey,
            InstallLocations.PathValue,
            User + @"\;C:\Tools",
            expandable: true);
        files.Seed(Path.Combine(User, "winland.exe"), [1]);

        install.Uninstall(purge: false, machine: false);

        Assert.AreEqual(@"C:\Tools", Text(RegistryRoot.User, InstallLocations.PathValue, InstallLocations.UserPathKey));
    }

    [TestMethod]
    public void AUserInstallIsRefusedWhileTheMachineInstallIsThere()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        Assert.ThrowsExactly<InvalidOperationException>(() => install.InstallForUser(Source, "0.1.0"));
        Assert.IsFalse(files.Exists(Path.Combine(User, "winland.exe")));
    }

    [TestMethod]
    public void ASettingsRecordThatFailsStillLetsUninstallFinish()
    {
        var failing = new AppInstall(Layout(), tasks, files, registry, shortcuts, session, new WatchingRestore(() => throw new IOException("bad record")));
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        failing.Uninstall(purge: false, machine: true);
        Assert.ThrowsExactly<InvalidOperationException>(() => failing.Uninstall(purge: false, machine: false));
        Assert.IsFalse(files.DirectoryExists(Machine));
        Assert.IsFalse(tasks.Contains("Winland"));
    }

    [TestMethod]
    public void MachineInstallCreatesTheLogonTaskWhenNoneExists()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        var task = tasks.Only();
        Assert.AreEqual("Winland", task.Name);
        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), task.Executable);
        Assert.AreEqual(@"DESKTOP\dante", task.User);
        Assert.AreEqual("create", Assert.ContainsSingle(tasks.Operations));
    }

    [TestMethod]
    public void MachineInstallReplacesTheExistingTaskAndLeavesOne()
    {
        tasks.Seed(LogonTaskDefinition.Build(@"C:\old\winland.exe", @"DESKTOP\dante"));

        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), tasks.Only().Executable);
        Assert.AreEqual("replace", Assert.ContainsSingle(tasks.Operations));
    }

    [TestMethod]
    public void MachineInstallTwiceLeavesOneTask()
    {
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");
        install.InstallMachine(Source, @"DESKTOP\dante", "0.1.0");

        Assert.AreEqual(Path.Combine(Machine, "winland.exe"), tasks.Only().Executable);
        Assert.AreSequenceEqual(["create", "replace"], tasks.Operations);
    }

    [TestMethod]
    public void MachineInstallRejectsABlankExecutable()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => install.InstallMachine(" ", @"DESKTOP\dante", "0.1.0"));

        Assert.AreEqual("executable", error.ParamName);
        Assert.IsEmpty(tasks.Operations);
    }

    [TestMethod]
    public void MachineInstallRejectsABlankAccount()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => install.InstallMachine(Source, " ", "0.1.0"));

        Assert.AreEqual("account", error.ParamName);
        Assert.IsEmpty(tasks.Operations);
    }

    [TestMethod]
    public void MachineUninstallRemovesTheLogonTask()
    {
        tasks.Seed(LogonTaskDefinition.Build(Path.Combine(Machine, "winland.exe"), @"DESKTOP\dante"));

        install.Uninstall(purge: false, machine: true);

        Assert.IsFalse(tasks.Contains("Winland"));
        Assert.AreEqual("delete", Assert.ContainsSingle(tasks.Operations));
    }

    [TestMethod]
    public void MachineUninstallWithoutTheTaskLeavesTheCatalogAlone()
    {
        install.Uninstall(purge: false, machine: true);

        Assert.IsEmpty(tasks.Operations);
    }

    private sealed class WatchingRestore(Action onRestore) : ITweakRestore
    {
        public void Restore() => onRestore();
    }

    private string? Text(RegistryRoot root, string name, string? key = null) =>
        registry.GetString(root, key ?? InstallLocations.UninstallKey, name);

    private int Dword(RegistryRoot root, string name) =>
        registry.Values[(root, InstallLocations.UninstallKey, name)].Dword ?? throw new InvalidOperationException(name);

    private static InstallLayout Layout() => new(Machine, User, CommonPrograms, UserPrograms, Config);
}
