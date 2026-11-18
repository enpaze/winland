
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "config")]
[TestClass]
public class Config
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void ChecksFilesAndReloadsWhenTheConfigChanges() => VerifyRun.Drive(TestContext, "gaps_out = 0\n", run =>
    {
        var folder = run.Daemon.Folder;
        var good = Path.Combine(folder, "good.conf");
        var bad = Path.Combine(folder, "bad.conf");
        File.WriteAllText(good, "gaps_in = 3\nsource = ./extra.conf\n");
        File.WriteAllText(Path.Combine(folder, "extra.conf"), "gaps_out = 5\n");
        File.WriteAllText(bad, "gaps_in = 3\ngaps_inn = 4\n");

        var clean = run.Cli("check", good);
        Assert.AreEqual(0, clean.ExitCode);
        Assert.AreEqual("", clean.Output + clean.Error);
        var warned = run.Cli("check", bad);
        Assert.AreEqual(1, warned.ExitCode);
        Assert.Contains("bad.conf:2:", warned.Error, StringComparison.Ordinal);
        Assert.Contains("Did you mean 'gaps_in'?", warned.Error, StringComparison.Ordinal);
        Assert.AreEqual(2, run.Cli("check", good, bad).ExitCode);
        Assert.AreEqual(1, run.Cli("check", Path.Combine(folder, "missing.conf")).ExitCode);

        var files = run.Json("get", "files");
        Assert.AreEqual(run.Daemon.ConfigPath, files.GetProperty("config").GetString(), StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual("no config errors", run.Ask("get", "errors"));

        var window = run.OpenManaged("1");
        run.Until("the window fills the work area with gaps_out 0", () => VerifyRun.Frame(window).Near(VerifyRun.WorkArea(window), 4));

        run.FollowEvents();
        File.AppendAllText(run.Daemon.ConfigPath, "gaps_out = 40\nnosuchoption = 1\n");
        run.Say("saved the config with gaps_out 40 and an unknown option");
        run.UntilEvent("configreloaded");
        Assert.Contains("nosuchoption", run.Ask("get", "errors"), StringComparison.Ordinal);
        Assert.Contains(error =>
            string.Equals(error.GetProperty("file").GetString(), run.Daemon.ConfigPath, StringComparison.OrdinalIgnoreCase)
            && error.GetProperty("line").GetInt32() > 0
            && error.GetProperty("message").GetString()!.Contains("nosuchoption", StringComparison.Ordinal),
            run.Json("get", "errors").EnumerateArray());
        run.Say("saw: get errors names nosuchoption with its file and line, in text and JSON");
        run.Until("saving the config reloaded it and the window moved in by gaps_out 40", () =>
        {
            var frame = VerifyRun.Frame(window);
            var work = VerifyRun.WorkArea(window);
            return Math.Abs(frame.Left - work.Left - 40) <= 4 && Math.Abs(work.Right - frame.Right - 40) <= 4;
        });

        Assert.AreEqual("ok", run.Ask("reload"));
        run.UntilEvent("configreloaded");
    });
}
