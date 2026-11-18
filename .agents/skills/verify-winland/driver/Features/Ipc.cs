
namespace Winland.Verify.Tests.Features;

[Trait("Feature", "ipc")]
[TestClass]
public class Ipc
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void AnswersQueriesInspectsWindowsAndStreamsEvents() => VerifyRun.Drive(TestContext, run =>
    {
        Assert.AreEqual("no open windows", run.Ask("clients"));
        var empty = run.Cli("activewindow");
        Assert.AreEqual(0, empty.ExitCode);
        Assert.AreEqual("", empty.Output);
        Assert.AreEqual("null", run.Ask("-j", "activewindow"));

        run.FollowEvents("-j events");
        var window = run.OpenManaged("1");
        run.UntilEvent("openwindow", VerifyRun.EventAddress(window) + ",1,");
        run.Focus(window);
        Assert.Contains("WinlandVerify 1", run.Ask("clients"), StringComparison.Ordinal);
        Assert.AreEqual("WinlandVerify 1", run.Json("activewindow").GetProperty("title").GetString());
        Assert.AreEqual(1, run.Json("activeworkspace").GetProperty("windows").GetInt32());
        Assert.Contains(workspace => workspace.GetProperty("id").GetInt32() == 1, run.Json("workspaces").EnumerateArray());

        var found = run.Json("inspect", "address:" + VerifyRun.Address(window));
        Assert.AreEqual("found", found.GetProperty("status").GetString());
        Assert.AreEqual("selector", found.GetProperty("target").GetString());
        Assert.IsTrue(found.GetProperty("managed").GetBoolean());
        Assert.AreEqual("WinlandVerifyWindow", found.GetProperty("class").GetString());
        var patterned = run.Json("inspect", "winlandverify*");
        Assert.AreEqual(VerifyRun.Address(window), patterned.GetProperty("address").GetString());
        var byExe = run.Json("inspect", "exe:" + Path.GetFileName(Environment.ProcessPath)!.ToUpperInvariant());
        Assert.AreEqual(VerifyRun.Address(window), byExe.GetProperty("address").GetString());
        var missing = run.Json("inspect", "title:NoSuchWindow");
        Assert.AreEqual("not_found", missing.GetProperty("status").GetString());
        Assert.AreEqual("no_client", missing.GetProperty("reason").GetString());
        var malformed = run.Cli("inspect", "pid:abc");
        Assert.AreEqual(1, malformed.ExitCode);
        Assert.StartsWith("error: invalid selector", malformed.Error.Trim(), StringComparison.Ordinal);

        var unknown = run.Cli("dispatch", "nosuchdispatcher");
        Assert.AreEqual(1, unknown.ExitCode);
        Assert.StartsWith("error:", unknown.Error.Trim(), StringComparison.Ordinal);
        var drag = run.Cli("dispatch", "resizewindow");
        Assert.AreEqual(1, drag.ExitCode);
        Assert.AreEqual("error: resizewindow is a drag; bind it to a mouse button", drag.Error.Trim());
        var names = run.Cli("dispatch");
        Assert.AreEqual(0, names.ExitCode);
        Assert.Contains("workspace", names.Output.Split('\n', StringSplitOptions.TrimEntries));

        run.StopEvents();
        Assert.IsTrue(run.Daemon.TryStopClean(), "winland did not stop cleanly. " + run.Daemon.Logs());
        Assert.AreEqual(1, run.Cli("clients").ExitCode);
        var version = run.Ask("version");
        Assert.MatchesRegex(@"^winland \S+ \(\S+\)$", version);
        Assert.StartsWith("winland " + run.Json("version").GetProperty("version").GetString() + " (", version, StringComparison.Ordinal);
    });
}
