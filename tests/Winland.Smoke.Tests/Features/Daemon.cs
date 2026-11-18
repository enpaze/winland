using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "daemon")]
public class Daemon(ITestOutputHelper output)
{
    [SmokeFact]
    public void RunsOncePerSessionRecoversFromACrashAndExits() => SmokeRun.Drive(output, run =>
    {
        var second = SmokeCli.Run(
            run.Paths.Daemon,
            run.DotnetRoot,
            run.Daemon.Pipe,
            run.Daemon.Folder,
            ["--config", run.Daemon.ConfigPath],
            TimeSpan.FromSeconds(20),
            new Dictionary<string, string> { ["WINLAND_STATE"] = run.Daemon.State });
        run.Say("started a second winland on the same pipe -> " + second);
        Assert.Equal(1, second.ExitCode);
        Assert.Contains("Winland is already running in this session.", second.Error, StringComparison.Ordinal);

        var usage = run.Cli("update", "--channel", "nightly");
        Assert.Equal(2, usage.ExitCode);

        var (left, right) = run.OpenPair();
        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides both windows", () => !SmokeRun.Visible(left) && !SmokeRun.Visible(right));
        run.Until("winland saved its state", () => File.Exists(run.Daemon.State));
        Thread.Sleep(500);

        run.Kill();
        Assert.False(SmokeRun.Visible(left) || SmokeRun.Visible(right), "The windows came back without a restart.");
        run.Restart();
        run.Until("the next start shows what the killed winland left hidden and manages both windows again", () =>
            SmokeRun.Visible(left) && SmokeRun.Visible(right) && run.Client(left) is not null && run.Client(right) is not null);

        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides them again", () => !SmokeRun.Visible(left) && !SmokeRun.Visible(right));
        run.Cli("dispatch", "exit");
        run.Until("exit stops winland", () => run.Daemon.Exited);
        run.Until("exit puts the hidden windows back", () => SmokeRun.Visible(left) && SmokeRun.Visible(right));
    });
}
