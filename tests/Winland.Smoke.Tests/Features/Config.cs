using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "config")]
public class Config(ITestOutputHelper output)
{
    [SmokeFact]
    public void ChecksFilesAndReloadsWhenTheConfigChanges() => SmokeRun.Drive(output, "general:gaps_out = 0\n", run =>
    {
        var folder = run.Daemon.Folder;
        var good = Path.Combine(folder, "good.conf");
        var bad = Path.Combine(folder, "bad.conf");
        File.WriteAllText(good, "general:gaps_in = 3\nsource = ./extra.conf\n");
        File.WriteAllText(Path.Combine(folder, "extra.conf"), "general:gaps_out = 5\n");
        File.WriteAllText(bad, "general:gaps_in = 3\ngeneral:gaps_inn = 4\n");

        var clean = run.Cli("check", good);
        Assert.Equal(0, clean.ExitCode);
        Assert.Equal("", clean.Output + clean.Error);
        var warned = run.Cli("check", bad);
        Assert.Equal(1, warned.ExitCode);
        Assert.Contains("bad.conf:2:", warned.Error, StringComparison.Ordinal);
        Assert.Contains("Did you mean 'general:gaps_in'?", warned.Error, StringComparison.Ordinal);
        Assert.Equal(2, run.Cli("check", good, bad).ExitCode);
        Assert.Equal(1, run.Cli("check", Path.Combine(folder, "missing.conf")).ExitCode);

        var files = run.Json("files");
        Assert.Equal(run.Daemon.ConfigPath, files.GetProperty("config").GetString(), ignoreCase: true);
        Assert.Equal("no config errors", run.Ask("configerrors"));

        var window = run.OpenManaged("1");
        run.Until("the window fills the work area with gaps_out 0", () => SmokeRun.Frame(window).Near(SmokeRun.WorkArea(window), 4));

        run.FollowEvents();
        File.AppendAllText(run.Daemon.ConfigPath, "general:gaps_out = 40\ngeneral:nosuchoption = 1\n");
        run.Say("saved the config with gaps_out 40 and an unknown option");
        run.UntilEvent("configreloaded");
        run.Until("saving the config reloaded it and the window moved in by gaps_out 40", () =>
        {
            var frame = SmokeRun.Frame(window);
            var work = SmokeRun.WorkArea(window);
            return Math.Abs(frame.Left - work.Left - 40) <= 4 && Math.Abs(work.Right - frame.Right - 40) <= 4;
        });
        Assert.Contains("general:nosuchoption", run.Ask("configerrors"), StringComparison.Ordinal);

        Assert.Equal("ok", run.Ask("reload"));
        run.UntilEvent("configreloaded");
    });
}
