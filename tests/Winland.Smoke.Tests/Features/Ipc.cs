using Xunit.Abstractions;

namespace Winland.Smoke.Tests.Features;

[Trait("Feature", "ipc")]
public class Ipc(ITestOutputHelper output)
{
    [SmokeFact]
    public void AnswersQueriesInspectsWindowsAndStreamsEvents() => SmokeRun.Drive(output, run =>
    {
        Assert.Matches(@"^winland \S+ \(\S+\)$", run.Ask("version"));
        Assert.Equal("no open windows", run.Ask("clients"));
        Assert.Equal("Invalid", run.Ask("activewindow"));
        Assert.Equal("{}", run.Ask("-j", "activewindow"));

        run.FollowEvents();
        var window = run.OpenManaged("1");
        run.UntilEvent("openwindow", SmokeRun.EventAddress(window) + ",1,");
        run.Focus(window);
        Assert.Contains("WinlandSmoke 1", run.Ask("clients"), StringComparison.Ordinal);
        Assert.Equal("WinlandSmoke 1", run.Json("activewindow").GetProperty("title").GetString());
        Assert.Equal(1, run.Json("activeworkspace").GetProperty("windows").GetInt32());
        Assert.Contains(run.Json("workspaces").EnumerateArray(), workspace => workspace.GetProperty("id").GetInt32() == 1);

        var found = run.Json("inspect", "address:" + SmokeRun.Address(window));
        Assert.Equal("found", found.GetProperty("status").GetString());
        Assert.Equal("selector", found.GetProperty("target").GetString());
        Assert.True(found.GetProperty("managed").GetBoolean());
        Assert.Equal("WinlandSmokeWindow", found.GetProperty("class").GetString());
        var missing = run.Json("inspect", "title:NoSuchWindow");
        Assert.Equal("not_found", missing.GetProperty("status").GetString());
        Assert.Equal("no_client", missing.GetProperty("reason").GetString());
        var malformed = run.Cli("inspect", "pid:abc");
        Assert.Equal(1, malformed.ExitCode);
        Assert.StartsWith("error: invalid selector", malformed.Error.Trim(), StringComparison.Ordinal);

        var unknown = run.Cli("dispatch", "nosuchdispatcher");
        Assert.Equal(1, unknown.ExitCode);
        Assert.StartsWith("error:", unknown.Error.Trim(), StringComparison.Ordinal);
        var names = run.Cli("dispatch");
        Assert.Equal(0, names.ExitCode);
        Assert.Contains("workspace", names.Output.Split('\n', StringSplitOptions.TrimEntries));
    });
}
