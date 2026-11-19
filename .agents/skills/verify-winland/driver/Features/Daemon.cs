
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "daemon")]
[TestClass]
public class Daemon
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void RunsOncePerSessionRecoversFromACrashAndExits() => VerifyRun.Drive(TestContext, run =>
    {
        var second = VerifyCli.Run(
            run.Paths.Daemon,
            run.DotnetRoot,
            run.Daemon.Pipe,
            run.Daemon.Folder,
            ["--config", run.Daemon.ConfigPath],
            TimeSpan.FromSeconds(20),
            new Dictionary<string, string> { ["WINLAND_STATE"] = run.Daemon.State });
        run.Say("started a second winland on the same pipe -> " + second);
        Assert.AreEqual(1, second.ExitCode);
        Assert.Contains("Winland is already running in this session.", second.Error, StringComparison.Ordinal);

        var usage = run.Cli("update", "--channel", "nightly");
        Assert.AreEqual(2, usage.ExitCode);

        var (left, right) = run.OpenPair();
        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides both windows", () => !VerifyRun.Visible(left) && !VerifyRun.Visible(right));
        run.Until("winland saved its state", () => File.Exists(run.Daemon.State));
        Thread.Sleep(500);

        run.Kill();
        Assert.IsFalse(VerifyRun.Visible(left) || VerifyRun.Visible(right), "The windows came back without a restart.");
        var renamed = Path.Combine(run.Daemon.Folder, "winland.txt");
        File.Copy(run.Daemon.ConfigPath, renamed);
        run.Restart(renamed);
        Assert.AreEqual(renamed, run.Json("get", "files").GetProperty("config").GetString(), StringComparer.OrdinalIgnoreCase);
        run.Until("the next start shows what the killed winland left hidden and manages both windows again", () =>
            VerifyRun.Visible(left) && VerifyRun.Visible(right) && run.Client(left) is not null && run.Client(right) is not null);

        run.Dispatch("workspace 2");
        run.Until("workspace 2 hides them again", () => !VerifyRun.Visible(left) && !VerifyRun.Visible(right));
        run.Cli("exit");
        run.Until("exit stops winland", () => run.Daemon.Exited);
        run.Until("exit puts the hidden windows back", () => VerifyRun.Visible(left) && VerifyRun.Visible(right));
    });
}
