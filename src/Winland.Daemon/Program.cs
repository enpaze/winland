using Winland.Daemon;
using Winland.Install;

RetireStaleInstallFiles();

args = Elevation.SendOutputBack(args);

if (args.Where(arg => arg != "-j").ToArray() is ["help" or "--help"])
{
    Program.WriteUsage(Console.Out);
    return 0;
}

if (ControlInvocation.TryAnswerLocally(args, out var local))
{
    Console.WriteLine(local);
    return 0;
}

if (args.Length > 0 && args[0].Equals("check", StringComparison.OrdinalIgnoreCase))
    return Program.Check(args[1..], ConfigPaths.Default(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), Console.Error);

if (ControlInvocation.TryCreate(args, out var line, out var events))
    return ControlCommand.Run(line, events, CommandClient.ConfiguredName());

var log = new RollingLogWriter(Console.Error);
Console.SetError(log);

if (!InstallCommand.TryParse(args, out var maintenance, out var parseError))
{
    Console.Error.WriteLine(parseError);
    return 2;
}

if (maintenance is InstallCommand command)
    return Guard(() => MaintenanceCommand.Run(args, command));

var userInstall = InstallLocations.IsPerUserInstall(
    Environment.ProcessPath,
    InstallLocations.User(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
switch (ManualStartElevation.Decide(
    UserAccess.IsElevated,
    Environment.GetEnvironmentVariable(ManualStartElevation.OptOutVariable),
    args,
    userInstall,
    limitedToken: UserAccess.HasLimitedToken()))
{
    case ElevationChoice.Relaunch relaunch:
        return Guard(() => IsStart([.. relaunch.Arguments.Skip(1)]) ? Relaunch(relaunch.Arguments) : Usage());
    case ElevationChoice.Refuse refuse:
        Console.Error.WriteLine(refuse.Message);
        return 1;
    case ElevationChoice.Continue continued:
        args = [.. continued.Arguments];
        break;
    default:
        throw new InvalidOperationException("Unexpected start decision.");
}

if (!IsStart(args))
    return Usage();

return Guard(() => Run(args.Length == 0 ? null : args[1], log), rethrow: true);

static void RetireStaleInstallFiles()
{
    var image = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(image))
        return;

    var directory = Path.GetDirectoryName(image);
    if (string.IsNullOrWhiteSpace(directory))
        return;

    WindowsInstallFiles.DeleteRetired(directory);
}

static bool IsStart(string[] arguments) =>
    arguments.Length == 0 || (arguments.Length == 2 && arguments[0].Equals("--config", StringComparison.OrdinalIgnoreCase));

static int Relaunch(IReadOnlyList<string> arguments)
{
    using (var probe = InstanceLock.TryTake(InstanceLock.NameFor(CommandClient.ConfiguredName() ?? CommandProtocol.PipeName)))
    {
        if (probe is null)
        {
            Console.Error.WriteLine("Winland is already running in this session.");
            return 1;
        }
    }

    return Elevation.Relaunch(arguments);
}

static void LaunchConfigured(WindowManager core, IEnumerable<Launch> launches)
{
    foreach (var launch in launches)
    {
        var pid = ProcessLauncher.TryLaunch(launch);
        if (pid is int id)
            core.NoteLaunched(id, launch);
    }
}

static int Run(string? path, RollingLogWriter log)
{
    using var instance = InstanceLock.TryTake(InstanceLock.NameFor(CommandClient.ConfiguredName() ?? CommandClient.DefaultName));
    if (instance is null)
    {
        Console.Error.WriteLine("Winland is already running in this session.");
        return 1;
    }

    if (path is not null && !path.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"--config takes a .conf file, not '{path}'.");
        return 1;
    }

    var configPath = path ?? ConfigPaths.Default(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    var firstStart = path is null && DefaultConfig.WriteIfMissing(configPath);
    if (firstStart)
        Console.Error.WriteLine($"Wrote the default config to {configPath}.");

    if (!UserAccess.Run(() => File.Exists(configPath)))
    {
        Console.Error.WriteLine($"Could not read config '{Path.GetFullPath(configPath)}'.");
        return 1;
    }

    var current = ConfigFile.Read(configPath);
    ConfigFile.WriteWarnings(current.Warnings, configPath, Console.Error);

    var core = new WindowManager { FirstStart = firstStart };
    core.ReadConfig = () => ConfigFile.TryRead(configPath);
    core.LogFile = () => log.FilePath;
    ConfigWatcher? watcher = null;
    string? logFile = null;
    core.AfterReload = document =>
    {
        current = document;
        CopyLog(log, core.Debug, logFile);
        watcher?.Watch(ConfigPaths.Watched(configPath, document.Sources));
    };

    core.Handle(new Configure(current));
    var store = StateStore();
    logFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(store.File)) ?? ".", "winland.log");
    CopyLog(log, core.Debug, logFile);
    var prior = store.Read();

    if (prior is not null)
        SessionEffects.RestoreCursors();
    SessionEffects.InstallSystem();
    using var host = new SessionLoop(core, store.Save, CommandClient.ConfiguredName(), controlTaskbar: true);
    var stopGate = new Lock();
    var stopped = false;
    void StopOnce()
    {
        lock (stopGate)
        {
            if (stopped)
                return;

            stopped = true;
            Stop(host, store, core);
        }
    }

    host.SessionEnding = StopOnce;
    using var exit = new ExitWatch(() => host.OnLoop(StopOnce));
    UnhandledExceptionEventHandler onCrash = (_, e) =>
    {
        Console.Error.WriteLine(e.ExceptionObject);
        Salvage(host);
    };
    AppDomain.CurrentDomain.UnhandledException += onCrash;
    using var watching = new ConfigWatcher(host.PostReload);
    watcher = watching;
    try
    {
        host.OnOpenFile = TrayFiles.Open;
        host.LogExists = () => logFile is not null && File.Exists(logFile);
        host.OnTray = command =>
        {
            switch (command)
            {
                case TrayCommand.Reload:
                    host.Reload();
                    break;
                case TrayCommand.EditConfig:
                    TrayFiles.Open(configPath);
                    break;
                case TrayCommand.OpenLog:
                    TrayFiles.Open(logFile);
                    break;
                case TrayCommand.Quit:
                    host.Command("exit");
                    break;
            }
        };

        host.Run(prior, () =>
        {
            store.Arm();
            watching.Watch(ConfigPaths.Watched(configPath, current.Sources));
            Persist(store, core.Recovery());
            LaunchConfigured(core, core.StartupLaunches(current));
            Console.WriteLine("Winland is running.");
        });
        return 0;
    }
    finally
    {
        watching.Dispose();
        StopOnce();
        AppDomain.CurrentDomain.UnhandledException -= onCrash;
        Console.WriteLine("Winland stopped.");
    }
}

static void Persist(RecoveryStore store, RecoverySnapshot snapshot)
{
    store.Save(WindowIdentity.Annotate(snapshot));
}

static void Stop(SessionLoop host, RecoveryStore store, WindowManager core)
{
    try
    {
        var final = core.Recovery();
        host.RevealHidden();
        host.ResetDecorations();
        host.RestoreTaskbar();
        SessionEffects.Restore();

        store.Keep(WindowIdentity.Annotate(host.Stamp(final)) with { Clean = true });
    }
    catch (Exception exception) when (exception is not OutOfMemoryException)
    {
        Console.Error.WriteLine(exception);
        Salvage(host);
    }
}

static void Salvage(SessionLoop host)
{
    try
    {
        host.Salvage();
    }
    catch (Exception exception) when (exception is not OutOfMemoryException)
    {
        Console.Error.WriteLine(exception);
    }

    try
    {
        SessionEffects.Restore();
    }
    catch (Exception exception) when (exception is not OutOfMemoryException)
    {
        Console.Error.WriteLine(exception);
    }
}

static RecoveryStore StateStore()
{
    var standard = Path.Combine(
        InstallLocations.Config(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        "state.json");
    var variable = Environment.GetEnvironmentVariable(StateLocation.Variable);
    if (!UserAccess.IsElevated)
        return new RecoveryStore(StateLocation.Choose(variable, standard, elevated: false, _ => null).Path);

    string user;
    using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
        user = identity.User?.Value ?? throw new InvalidOperationException("The current user has no SID.");

    string? RefuseOpen(Microsoft.Win32.SafeHandles.SafeFileHandle folder) =>
        StateLocation.Refusal(WindowsFileHandles.FinalPath(folder), WindowsAccessReader.TryRead(folder, out var access) ? access : null, user);

    string? RefusePath(string folder)
    {
        try
        {
            using var handle = WindowsFileHandles.Open(folder, WindowsFileHandles.ReadAttributes | WindowsFileHandles.ReadControl, FileShare.ReadWrite | FileShare.Delete, followReparse: true);
            return RefuseOpen(handle);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    var (path, ignored) = StateLocation.Choose(variable, standard, elevated: true, RefusePath);
    if (ignored is not null)
        Console.Error.WriteLine(ignored);

    return new RecoveryStore(path, RefuseOpen);
}

static int Usage()
{
    Program.WriteUsage(Console.Error);
    return 2;
}

static void CopyLog(RollingLogWriter log, DebugOptions options, string? file)
{
    log.Copy(options, OpenLog);

    TextWriter? OpenLog()
    {
        if (file is null)
            return null;

        if (UserAccess.IsElevated)
        {
            Console.Error.WriteLine("debug:disable_logs is false, but an elevated Winland writes no log file.");
            return null;
        }

        try
        {
            return RollingLogWriter.OpenFile(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not open the log file '{file}': {exception.Message}");
            return null;
        }
    }
}

internal partial class Program
{
    internal static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("usage: winland");
        writer.WriteLine("       winland --config <path>");
        writer.WriteLine("       winland check [file]");
        writer.WriteLine("       winland install [--user] [--no-logon] [--no-start]");
        writer.WriteLine("       winland update [--channel <stable|dev>]");
        writer.WriteLine("       winland uninstall [--purge]");
        writer.WriteLine("       winland tweaks --off [--touchpad]");
        writer.WriteLine("       winland tweaks --restore");
        writer.WriteLine("       winland help");
        writer.Write(ControlInvocation.Usage);
        writer.WriteLine("-j prints JSON, before or after the command. To pass -j to a program, quote its exec command line as one argument.");
        writer.WriteLine("check reads a config as a start would, and changes nothing: 0 when clean, 1 for any warning or a file it cannot read, 2 for bad usage.");
        writer.WriteLine("version prints this winland's version and needs no running Winland.");
        writer.WriteLine("WINLAND_NO_ELEVATE=1 or --no-elevate starts without elevation. install --user uses it.");
        writer.WriteLine("Elevated features then do not work. A per-user install never asks for elevation.");
    }

    internal static int Check(string[] arguments, string defaultConfig, TextWriter error)
    {
        if (arguments.Length > 1 || (arguments.Length == 1 && (string.IsNullOrWhiteSpace(arguments[0]) || arguments[0].StartsWith('-'))))
        {
            error.WriteLine("usage: winland check [file]");
            return 2;
        }

        return ConfigFile.Check(arguments.Length == 0 ? defaultConfig : arguments[0], error);
    }

    internal static int Guard(Func<int> action, bool rethrow = false)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (!rethrow && ex is InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine(ex);
            if (rethrow)
                throw;
            return 1;
        }
    }
}
