using Microsoft.Win32;

namespace Winland.Verify.Tests;

internal sealed class UserInstall
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Winland";

    private readonly TestContext _output;

    private UserInstall(TestContext output)
    {
        _output = output;
        Paths = VerifyPaths.Read();
        DotnetRoot = VerifyDaemon.ResolveDotnetRoot();
    }

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Winland");

    public static string Executable => Path.Combine(Folder, "winland.exe");

    public static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Winland.lnk");

    public static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".winland");

    public static string SettingsRecord => Path.Combine(Home, "windows-settings.json");

    public VerifyPaths Paths { get; }

    public string DotnetRoot { get; }

    public static bool Registered => Registry.CurrentUser.OpenSubKey(UninstallKey) is { } key && Dispose(key);

    public static string? RegisteredVersion
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key?.GetValue("DisplayVersion") as string;
        }
    }

    public static bool OnPath
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey("Environment");
            var path = key?.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            return path.Split(';').Any(entry => string.Equals(entry.Trim().TrimEnd('\\'), Folder, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static bool StartsAtLogon
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("Winland") is not null;
        }
    }

    public static UserInstall Begin(TestContext output)
    {
        var machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Winland");
        Assert.IsFalse(Directory.Exists(Folder), $"Winland is already installed for this user at {Folder}. The driver will not replace it.");
        Assert.IsFalse(Directory.Exists(machine), $"Winland is installed at {machine}. The driver will not install beside it.");
        Assert.IsFalse(Registered, "Settings › Apps already lists Winland for this user. The driver will not replace it.");
        Assert.IsFalse(File.Exists(SettingsRecord), $"{SettingsRecord} records Windows settings to restore. The driver will not touch them.");
        return new UserInstall(output);
    }

    public CliResult Run(string executable, params string[] arguments)
    {
        var result = Unelevated.Run(executable, DotnetRoot, arguments);
        _output.WriteLine($"{(executable == Executable ? "installed winland" : "built winland")} {string.Join(' ', arguments)}{(Unelevated.Elevated() ? " (as a standard user)" : "")} -> {result}");
        return result;
    }

    public void Install()
    {
        var installed = Run(Paths.Daemon, "install", "--user", "--no-logon", "--no-start");
        Assert.AreEqual(0, installed.ExitCode);
        Assert.Contains("Winland is installed for this user", installed.Output, StringComparison.Ordinal);
    }

    public void Uninstall()
    {
        var removed = Run(Executable, "uninstall");
        Assert.AreEqual(0, removed.ExitCode);
        Assert.Contains("Winland is uninstalled.", removed.Output, StringComparison.Ordinal);
    }

    public void CleanUp()
    {
        if (File.Exists(Executable) || Registered)
        {
            _output.WriteLine("cleanup: uninstalling what the driver installed");
            _ = Run(File.Exists(Executable) ? Executable : Paths.Daemon, "uninstall");
        }
    }

    public void Say(string text) => _output.WriteLine(text);

    public void Until(string what, Func<bool> condition, int timeoutMs = 30_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            Assert.IsTrue(Environment.TickCount64 < deadline, "Timed out waiting for: " + what);
            Thread.Sleep(100);
        }

        Say("saw: " + what);
    }

    private static bool Dispose(IDisposable disposable)
    {
        disposable.Dispose();
        return true;
    }
}
