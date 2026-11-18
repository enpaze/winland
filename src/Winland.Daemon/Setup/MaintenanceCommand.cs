using System.Security.Principal;
using Winland.Install;

namespace Winland.Daemon;

static class MaintenanceCommand
{
    public static int Run(string[] args, InstallCommand command)
    {
        var needsAdministrator = command is InstallCommand.Remove
            ? MachineInstalled()
            : command.RequiresAdministrator(machineInstallPresent: false);
        if (needsAdministrator && !UserAccess.IsElevated)
        {
            var relay = command is InstallCommand.Machine { Account: null } machine ? machine.Relay(CurrentUser()) : args;
            var result = Elevation.Relaunch(relay);
            if (result == 0)
            {
                if (command is InstallCommand.Machine)
                    Maintenance().RemoveUserInstall();
                else if (command is InstallCommand.Remove remove)
                {
                    TaskbarControl.RecoverHidden();
                    Maintenance().Uninstall(remove.Purge, machine: false);
                }
            }

            return result;
        }

        return Perform(command);
    }

    private static int Perform(InstallCommand command)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The executable path is not available.");
        if (command.ElevatedRefusal is string refusal && UserAccess.IsElevated)
            throw new InvalidOperationException(refusal);

        var app = Maintenance();
        switch (command)
        {
            case InstallCommand.UserCleanup:
                app.RemoveUserInstall();
                return 0;
            case InstallCommand.Machine machine:
                var account = string.IsNullOrWhiteSpace(machine.Account) ? CurrentUser() : machine.Account;
                var start = machine.Start && account.Equals(CurrentUser(), StringComparison.OrdinalIgnoreCase);
                app.InstallMachine(executable, account, BuildStamp.Version, machine.Logon, start);
                Console.WriteLine(Installed("Winland is installed", machine.Logon, start));
                if (machine.Start && !start)
                    Console.WriteLine("Start Winland from the signed-in user's Start menu.");
                return 0;
            case InstallCommand.ForUser user:
                app.InstallForUser(executable, BuildStamp.Version, user.Logon, user.Start);
                Console.WriteLine(Installed("Winland is installed for this user", user.Logon, user.Start));
                return 0;
            case InstallCommand.Remove remove:
                if (!UserAccess.IsElevated)
                    TaskbarControl.RecoverHidden();
                app.Uninstall(remove.Purge, machine: UserAccess.IsElevated);
                Console.WriteLine(remove.Purge
                    ? "Winland is uninstalled, and its config, logs and state are removed."
                    : "Winland is uninstalled.");
                return 0;
            case InstallCommand.Update update:
                return UpdateInstalled(update.Channel);
            case InstallCommand.Tweaks tweaks:
                RecordedSettings().TurnOff(tweaks.Touchpad);
                Console.WriteLine(tweaks.Touchpad
                    ? "Snap windows and the three- and four-finger touchpad swipes are off."
                    : "Snap windows is off.");
                return 0;
            case InstallCommand.RestoreTweaks:
                Console.WriteLine(RecordedSettings().Restore()
                    ? "The Windows settings winland tweaks --off recorded are back as they were."
                    : "winland tweaks --off recorded no settings for this user. Nothing changed.");
                return 0;
            default:
                throw new InvalidOperationException("Unexpected install command.");
        }
    }

    private static string Installed(string installed, bool logon, bool start) => (logon, start) switch
    {
        (true, true) => installed + ", running, and will start at logon.",
        (true, false) => installed + " and will start at logon.",
        (false, true) => installed + " and running.",
        (false, false) => installed + ".",
    };

    private static int UpdateInstalled(UpdateChannel? requested)
    {
        var saved = new FileSettingsText(UpdateChannelFile.PathFor(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        var channel = UpdateChannelFile.Choose(requested, UserAccess.IsElevated ? null : saved.Read());
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The executable path is not available.");
        var layout = Layout();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var result = new ReleaseUpdate(
            new GitHubReleaseFeed(http),
            new ZipReleasePackage(),
            Maintenance(),
            () => AdministratorFolder.CreateTemp("winland-update-"),
            RemoveStage).Run(
                BuildStamp.Version,
                executable,
                layout.MachineDirectory,
                layout.UserDirectory,
                UserAccess.IsElevated,
                channel,
                BuildStamp.Commit);

        switch (result)
        {
            case UpdateResult.NotInstalled:
                Console.Error.WriteLine("Winland is not installed. Run winland install.");
                return 1;
            case UpdateResult.Current:
                Remember();
                Console.WriteLine("Winland is up to date.");
                return 0;
            case UpdateResult.NeedsAdministrator:
                var resultCode = Elevation.Relaunch(["update", "--channel", UpdateChannels.Name(channel)]);
                if (resultCode == 0)
                    Remember();
                return resultCode;
            case UpdateResult.Rejected:
                Console.Error.WriteLine("Refusing the update: the SHA-256 of Winland-win-x64.zip does not match SHA256SUMS. The release may still be uploading. Try again in a minute.");
                return 1;
            case UpdateResult.Updated updated:
                if (UserAccess.IsElevated)
                    _ = SchtasksCatalog.UseLauncher(executable);
                Remember();
                Console.WriteLine("Winland is updated to " + updated.Version + ".");
                return 0;
            default:
                throw new InvalidOperationException("Unexpected update result.");
        }

        void Remember()
        {
            if (!UserAccess.IsElevated && requested is UpdateChannel chosen)
                saved.Write(UpdateChannelFile.Text(chosen));
        }
    }

    private static void RemoveStage(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static bool MachineInstalled() => Maintenance().MachinePresent();

    private static InstallLayout Layout() => new(
        InstallLocations.Machine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
        InstallLocations.User(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        InstallLocations.Config(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    private static AppInstall Maintenance() => new(
        Layout(),
        new SchtasksCatalog(),
        new WindowsInstallFiles(),
        new WindowsInstallRegistry(),
        new ShellStartMenu(),
        new DaemonSession(),
        RecordedSettings());

    private static DesktopSettings RecordedSettings() => new(
        new WindowsDesktop(),
        new FileSettingsText(InstallLocations.SettingsFile(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))));

    private static string CurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.Name;
    }
}
