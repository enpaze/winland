using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "update")]
public partial class Update(ITestOutputHelper output)
{
    [SystemVerifyFact]
    public void UpdatesAnInstalledCopyFromTheDevRelease()
    {
        var install = UserInstall.Begin(output);
        var channel = Path.Combine(UserInstall.Home, "channel");
        var savedChannel = File.Exists(channel) ? File.ReadAllText(channel) : null;
        try
        {
            install.Install();
            var before = UserInstall.RegisteredVersion;
            install.Say($"installed version: {before}");

            var updated = install.Run(UserInstall.Executable, "update", "--channel", "dev");
            Assert.True(updated.ExitCode == 0, $"winland update --channel dev failed: {updated}");
            Assert.Equal("dev", File.ReadAllText(channel).Trim());
            install.Say("saw: update --channel dev saved the dev channel");

            if (UpdatedTo().Match(updated.Output) is { Success: true } match)
            {
                var version = match.Groups[1].Value;
                Assert.Equal(version, UserInstall.RegisteredVersion);
                Assert.True(File.Exists(UserInstall.Executable));
                install.Say($"saw: the installed copy was replaced with the dev release {version}, and Settings › Apps shows that version");
            }
            else
            {
                Assert.Contains("Winland is up to date.", updated.Output, StringComparison.Ordinal);
                install.Say("saw: this build is the dev release's commit, so update said it is up to date");
            }

            AssertNoRetirementError(updated);
            install.Say("saw: update printed no retirement error");
            var swept = install.Run(UserInstall.Executable, "check", Path.Combine(Path.GetTempPath(), "winland-verify-absent.conf"));
            AssertNoRetirementError(swept);
            Assert.False(RetiredFileRemains(UserInstall.Folder), "a .winland-old file remained after the next run");
            install.Say("saw: the next run left no file whose name contains .winland-old");

            install.Uninstall();
            install.Until("uninstall removes the updated copy", () => !Directory.Exists(UserInstall.Folder));
        }
        finally
        {
            install.CleanUp();
            if (savedChannel is null)
                File.Delete(channel);
            else
                File.WriteAllText(channel, savedChannel);
        }
    }

    [GeneratedRegex(@"Winland is updated to (\S+)\.")]
    private static partial Regex UpdatedTo();

    private static void AssertNoRetirementError(CliResult result)
    {
        var text = result.Output + result.Error;
        Assert.DoesNotContain("UnauthorizedAccessException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception calling \"Delete\"", text, StringComparison.Ordinal);
    }

    private static bool RetiredFileRemains(string folder) =>
        Directory.Exists(folder)
        && Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Any(path => Path.GetFileName(path).Contains(".winland-old", StringComparison.Ordinal));
}
