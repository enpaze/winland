namespace Winland.Install;

public sealed record InstallLayout(
    string MachineDirectory,
    string UserDirectory,
    string CommonPrograms,
    string UserPrograms,
    string ConfigDirectory);

public sealed class AppInstall
{
    private readonly InstallLayout layout;
    private readonly ITaskCatalog tasks;
    private readonly IFileTree files;
    private readonly IRegistry registry;
    private readonly IStartMenu shortcuts;
    private readonly IDaemonSession session;
    private readonly ITweakRestore tweaks;

    public AppInstall(
        InstallLayout layout,
        ITaskCatalog tasks,
        IFileTree files,
        IRegistry registry,
        IStartMenu shortcuts,
        IDaemonSession session,
        ITweakRestore? tweaks = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(session);
        this.layout = layout;
        this.tasks = tasks;
        this.files = files;
        this.registry = registry;
        this.shortcuts = shortcuts;
        this.session = session;
        this.tweaks = tweaks ?? NoTweakRestore.Instance;
    }

    public bool MachinePresent() =>
        files.DirectoryExists(layout.MachineDirectory)
        || tasks.Contains(LogonTaskDefinition.TaskName)
        || registry.GetString(RegistryRoot.Machine, InstallLocations.UninstallKey, "DisplayName") is not null;

    public void InstallMachine(string executable, string account, string version, bool logon = true, bool start = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var plan = Plan(executable, layout.MachineDirectory);
        Require(plan.Files, InstallLocations.LauncherName, plan.SourceDirectory);
        ApplyStopped(plan, perUser: false);
        var installed = plan.InstalledExecutable;
        if (logon)
            InstallLogonTask(installed, account);
        else
            RemoveLogonTask();
        Place(installed, layout.CommonPrograms, arguments: "");
        RegisterUninstall(RegistryRoot.Machine, installed, layout.MachineDirectory, version);
        SetPath(RegistryRoot.Machine, InstallLocations.MachinePathKey, layout.MachineDirectory);
        if (start)
            session.Start(installed, []);
    }

    public void InstallForUser(string executable, string version, bool logon = true, bool start = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (MachinePresent())
            throw new InvalidOperationException("Winland is already installed for this PC. Run winland uninstall first to install it for you only.");

        var plan = Plan(executable, layout.UserDirectory);
        ApplyStopped(plan, perUser: true);
        var installed = plan.InstalledExecutable;
        var command = Quote(installed) + " " + ManualStartElevation.OptOutArgument;
        if (logon)
            registry.SetString(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue, command, expandable: false);
        else
            registry.DeleteValue(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue);
        Place(installed, layout.UserPrograms, ManualStartElevation.OptOutArgument);
        RegisterUninstall(RegistryRoot.User, installed, layout.UserDirectory, version);
        SetPath(RegistryRoot.User, InstallLocations.UserPathKey, layout.UserDirectory);
        if (start)
            session.Start(installed, [ManualStartElevation.OptOutArgument]);
    }

    public void ReplaceInstalled(string sourceExecutable, string destination, bool perUser, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var plan = Plan(sourceExecutable, destination);
        if (!perUser)
            Require(plan.Files, InstallLocations.LauncherName, plan.SourceDirectory);

        var running = ApplyStopped(plan, perUser);
        var installed = plan.InstalledExecutable;
        var root = perUser ? RegistryRoot.User : RegistryRoot.Machine;
        RegisterUninstall(root, installed, destination, version);
        if (running)
            session.Start(installed, perUser ? [ManualStartElevation.OptOutArgument] : []);
    }

    public void Uninstall(bool purge, bool machine)
    {
        session.Stop();
        if (machine)
        {
            RemoveMachine();
            return;
        }

        Exception? unrestored = null;
        try
        {
            tweaks.Restore();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            unrestored = exception;
        }

        RemoveUserInstall();
        if (purge)
            files.DeleteTree(layout.ConfigDirectory);

        if (unrestored is not null)
            throw new InvalidOperationException("Winland is uninstalled, but the Windows settings it turned off were not put back: " + unrestored.Message, unrestored);
    }

    public void RemoveUserInstall()
    {
        registry.DeleteValue(RegistryRoot.User, InstallLocations.RunKey, InstallLocations.RunValue);
        shortcuts.Remove(InstallLocations.Shortcut(layout.UserPrograms));
        registry.DeleteKey(RegistryRoot.User, InstallLocations.UninstallKey);
        ClearPath(RegistryRoot.User, InstallLocations.UserPathKey, layout.UserDirectory);
        files.DeleteTree(layout.UserDirectory);
    }

    private sealed class NoTweakRestore : ITweakRestore
    {
        public static readonly NoTweakRestore Instance = new();

        public void Restore()
        {
        }
    }

    private sealed record CopyPlan(
        string SourceDirectory,
        IReadOnlyList<ListedFile> Files,
        string Destination,
        string InstalledExecutable);

    private CopyPlan Plan(string executable, string destination)
    {
        var source = Path.GetDirectoryName(Path.GetFullPath(executable))
            ?? throw new InvalidOperationException("The executable folder is not available.");
        var listed = files.List(source);
        Require(listed, InstallLocations.ExecutableName, source);
        RefuseLinks(source, listed);
        return new CopyPlan(source, listed, destination, InstallLocations.Executable(destination));
    }

    private void Apply(CopyPlan plan)
    {
        if (InstallLocations.SameFolder(plan.SourceDirectory, plan.Destination))
            return;

        files.PrepareDirectory(plan.Destination);
        foreach (var file in plan.Files.OrderBy(file => file.RelativePath.Equals("Winland.Domain.dll", StringComparison.OrdinalIgnoreCase) ? 2
                     : file.RelativePath.Equals(InstallLocations.ExecutableName, StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            files.Copy(Path.Combine(plan.SourceDirectory, file.RelativePath), Path.Combine(plan.Destination, file.RelativePath));

        var shipped = new HashSet<string>(plan.Files.Select(file => file.RelativePath), StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.List(plan.Destination))
        {
            if (!shipped.Contains(file.RelativePath) && !file.RelativePath.Contains(".winland-old", StringComparison.OrdinalIgnoreCase))
                files.DeleteFile(Path.Combine(plan.Destination, file.RelativePath));
        }
    }

    private bool ApplyStopped(CopyPlan plan, bool perUser)
    {
        var existed = files.Exists(plan.InstalledExecutable);
        var running = session.Stop();
        try
        {
            Apply(plan);
            return running;
        }
        catch
        {
            if (running && existed)
            {
                try
                {
                    session.Start(plan.InstalledExecutable, perUser ? [ManualStartElevation.OptOutArgument] : []);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    Console.Error.WriteLine("Could not restart the previous Winland: " + exception.Message);
                }
            }

            throw;
        }
    }

    private static void Require(IReadOnlyList<ListedFile> files, string name, string directory)
    {
        if (files.Any(file => file.RelativePath.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new InvalidOperationException($"'{name}' was not found in '{directory}'.");
    }

    private static void RefuseLinks(string directory, IReadOnlyList<ListedFile> files)
    {
        var link = files.FirstOrDefault(file => file.ReparsePoint);
        if (link is null)
            return;

        throw new InvalidOperationException(
            $"Refusing to install '{Path.Combine(directory, link.RelativePath)}': it is a link.");
    }

    private void Place(string executable, string programsDirectory, string arguments)
    {
        shortcuts.Place(new Shortcut(
            InstallLocations.Shortcut(programsDirectory),
            executable,
            arguments,
            InstallLocations.AppUserModelId));
    }

    private void RegisterUninstall(RegistryRoot root, string executable, string directory, string version)
    {
        registry.SetString(root, InstallLocations.UninstallKey, "DisplayName", "Winland", expandable: false);
        registry.SetString(root, InstallLocations.UninstallKey, "UninstallString", Quote(executable) + " uninstall", expandable: false);
        registry.SetString(root, InstallLocations.UninstallKey, "InstallLocation", directory, expandable: false);
        registry.SetString(root, InstallLocations.UninstallKey, "DisplayVersion", version, expandable: false);
        registry.SetDword(root, InstallLocations.UninstallKey, "NoModify", 1);
        registry.SetDword(root, InstallLocations.UninstallKey, "NoRepair", 1);
    }

    private void SetPath(RegistryRoot root, string key, string folder)
    {
        var current = registry.GetString(root, key, InstallLocations.PathValue);
        if (PathList.Add(current, folder) is string updated)
            registry.SetString(root, key, InstallLocations.PathValue, updated, expandable: true);
    }

    private void RemoveMachine()
    {
        RemoveLogonTask();
        shortcuts.Remove(InstallLocations.Shortcut(layout.CommonPrograms));
        registry.DeleteKey(RegistryRoot.Machine, InstallLocations.UninstallKey);
        ClearPath(RegistryRoot.Machine, InstallLocations.MachinePathKey, layout.MachineDirectory);
        files.DeleteTree(layout.MachineDirectory);
    }

    private void InstallLogonTask(string executable, string user)
    {
        var task = LogonTaskDefinition.Build(executable, user);
        if (tasks.Contains(task.Name))
            tasks.Replace(task);
        else
            tasks.Create(task);
    }

    private void RemoveLogonTask()
    {
        if (tasks.Contains(LogonTaskDefinition.TaskName))
            tasks.Delete(LogonTaskDefinition.TaskName);
    }

    private void ClearPath(RegistryRoot root, string key, string folder)
    {
        var current = registry.GetString(root, key, InstallLocations.PathValue);
        if (PathList.Remove(current, folder) is string updated)
            registry.SetString(root, key, InstallLocations.PathValue, updated, expandable: true);
    }

    private static string Quote(string value)
    {
        if (value.Contains('"'))
            throw new ArgumentException($"Cannot quote '{value}': it contains a quote.", nameof(value));

        return "\"" + value + "\"";
    }
}
