using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "install")]
public class Install(ITestOutputHelper output)
{
    [SystemSmokeFact]
    public void InstallsForThisUserAndUninstallsCleanly()
    {
        var install = UserInstall.Begin(output);
        var config = Path.Combine(Path.GetTempPath(), "winland-install-" + Guid.NewGuid().ToString("N") + ".conf");
        File.WriteAllText(config, "");
        try
        {
            install.Install();
            Assert.True(File.Exists(UserInstall.Executable), $"{UserInstall.Executable} was not installed.");
            Assert.True(File.Exists(UserInstall.Shortcut), $"The Start menu shortcut {UserInstall.Shortcut} was not made.");
            Assert.True(UserInstall.Registered, "Settings › Apps does not list Winland.");
            Assert.True(UserInstall.OnPath, $"{UserInstall.Folder} is not on the user PATH.");
            Assert.False(UserInstall.StartsAtLogon, "--no-logon still added the Run key entry.");
            install.Say("saw: the per-user copy, its Start menu shortcut, its Settings › Apps entry and its PATH entry, and no logon entry");

            var check = install.Run(UserInstall.Executable, "check", config);
            Assert.Equal(0, check.ExitCode);
            install.Say("saw: the installed winland runs");

            install.Uninstall();
            install.Until("uninstall's helper removes the install folder once winland has exited", () => !Directory.Exists(UserInstall.Folder));
            Assert.False(File.Exists(UserInstall.Shortcut), "The Start menu shortcut is still there.");
            Assert.False(UserInstall.Registered, "Settings › Apps still lists Winland.");
            Assert.False(UserInstall.OnPath, "The user PATH still has the install folder.");
            install.Say("saw: uninstall removed the files, the shortcut, the Settings › Apps entry and the PATH entry");
        }
        finally
        {
            install.CleanUp();
            File.Delete(config);
        }
    }
}
