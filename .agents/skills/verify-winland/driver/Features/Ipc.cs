using System.Text.Json;

namespace Winland.Verify.Tests.Features;

[Trait("Feature", "ipc")]
[TestClass]
public class Ipc
{

    public TestContext TestContext { get; set; } = null!;
    [VerifyFact]
    public void AnswersQueriesInspectsWindowsAndStreamsEvents() => VerifyRun.Drive(TestContext, run =>
    {
        Assert.AreEqual("no open windows", run.Ask("get", "windows"));
        var empty = run.Cli("get", "window");
        Assert.AreEqual(0, empty.ExitCode);
        Assert.AreEqual("", empty.Output);
        Assert.AreEqual("null", run.Ask("-j", "get", "window"));

        run.FollowEvents("-j events");
        var window = run.OpenManaged("1");
        run.UntilJsonEvent("openwindow", line =>
            line.GetProperty("address").GetString() == VerifyRun.Address(window)
            && line.GetProperty("workspace").GetProperty("id").GetInt32() == 1);
        run.UntilJsonEvent("focus", line =>
            line.GetProperty("window") is { ValueKind: JsonValueKind.Object } focused
            && focused.GetProperty("address").GetString() == VerifyRun.Address(window)
            && focused.GetProperty("title").GetString() == "WinlandVerify 1"
            && focused.GetProperty("class").GetString() == "WinlandVerifyWindow");
        run.Focus(window);
        Assert.Contains("WinlandVerify 1", run.Ask("get", "windows"), StringComparison.Ordinal);
        var active = run.Json("get", "window");
        Assert.AreEqual("WinlandVerify 1", active.GetProperty("title").GetString());
        Assert.AreEqual("address hidden visible at size workspace floating monitor class title initialClass initialTitle pid pinned fullscreen fullscreenHandler allowedOverFullscreen grouped", Keys(active));
        var activeWorkspace = run.Json("get", "workspace");
        Assert.AreEqual(1, activeWorkspace.GetProperty("windows").GetInt32());
        Assert.AreEqual(activeWorkspace.GetProperty("monitor").GetString(), active.GetProperty("monitor").GetString());
        Assert.AreEqual("id type name monitor monitorId windows hasFullscreen lastWindow lastWindowTitle tiledLayout", Keys(activeWorkspace));
        Assert.Contains(workspace => workspace.GetProperty("id").GetInt32() == 1, run.Json("get", "workspaces").EnumerateArray());

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

        Assert.AreEqual(1, activeWorkspace.GetProperty("id").GetInt32());
        Assert.AreEqual(run.Ask("-j", "get", "window"), run.Ask("get", "window", "-j"));

        foreach (var unknown in new[] { run.Cli("nosuchcommand"), run.Cli("dispatch", "workspace", "2"), run.Cli("clients") })
        {
            Assert.AreEqual(2, unknown.ExitCode);
            Assert.AreEqual("", unknown.Output);
            Assert.StartsWith("usage: winland", unknown.Error, StringComparison.Ordinal);
        }

        var drag = run.Cli("resizewindow");
        Assert.AreEqual(1, drag.ExitCode);
        Assert.AreEqual("error: resizewindow is a drag; bind it to a mouse button", drag.Error.Trim());
        foreach (var help in new[] { run.Cli("help"), run.Cli("--help") })
        {
            Assert.AreEqual(0, help.ExitCode);
            Assert.AreEqual("", help.Error);
            var actions = help.Output.Split('\n', StringSplitOptions.TrimEntries).Single(line => line.StartsWith("actions: ", StringComparison.Ordinal));
            Assert.Contains("workspace", actions["actions: ".Length..].Split(", "));
            Assert.Contains("reload", actions["actions: ".Length..].Split(", "));
        }

        run.StopEvents();
        Assert.IsTrue(run.Daemon.TryStopClean(), "winland did not stop cleanly. " + run.Daemon.Logs());
        Assert.AreEqual(1, run.Cli("get", "windows").ExitCode);
        var version = run.Ask("version");
        Assert.MatchesRegex(@"^winland \S+ \(\S+\)$", version);
        var versionJson = run.Json("version");
        Assert.StartsWith("winland " + versionJson.GetProperty("version").GetString() + " (", version, StringComparison.Ordinal);
        Assert.AreEqual("version commit dirty", Keys(versionJson));
    });

    private static string Keys(JsonElement json) => string.Join(' ', json.EnumerateObject().Select(property => property.Name));
}
