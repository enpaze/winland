using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class ExtraCommandTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private const string SuperKill =
        """
        [{"mouse":false,"hasDescription":false,"modMask":64,"key":"Q","keyCode":0,"description":"","dispatcher":"killactive","arg":""}]
        """;

    [TestMethod]
    public void FlagPrefixSelectsJson()
    {
        var json = Assert.IsExactInstanceOfType<CommandParsed>(CommandProtocol.Parse("j/get binds"));
        Assert.IsExactInstanceOfType<BindsRequest>(json.Request);
        Assert.AreEqual(CommandFlags.Json, json.Request.Flags);
    }

    [TestMethod]
    public void PluginListIsAnUnknownCommand()
    {
        Assert.IsExactInstanceOfType<CommandRejected>(CommandProtocol.Parse("plugin list"));
    }

    [TestMethod]
    public void BindsReportModifierBitsDescriptionsAndKeycodes()
    {
        var core = new WindowManager();
        Assert.AreEqual("[]", core.Execute("j/get binds").Text);

        core.Handle(new Configure(ConfigParser.Parse("bind = SUPER, Q, killactive\n")));
        Assert.AreEqual(SuperKill, core.Execute("j/get binds").Text);

        core.Handle(new Configure(ConfigParser.Parse("""
            bind = SUPER, Q, killactive
            bind = SUPER SHIFT, E, killactive
            bindd = SUPER, F, Close, killactive
            bind = SUPER, code:42, killactive
            """)));
        var binds = Json(core.Execute("j/get binds").Text);
        var second = binds[1];
        Assert.AreEqual(65, second.GetProperty("modMask").GetInt32());
        Assert.AreEqual("E", second.GetProperty("key").GetString());

        var described = binds[2];
        Assert.IsTrue(described.GetProperty("hasDescription").GetBoolean());
        Assert.AreEqual("Close", described.GetProperty("description").GetString());

        var coded = binds[3];
        Assert.AreEqual("", coded.GetProperty("key").GetString());
        Assert.AreEqual(42, coded.GetProperty("keyCode").GetInt32());
    }

    [TestMethod]
    public void VersionHasItsShape()
    {
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["-j", "version"], out var json));
        Assert.AreEqual(
            JsonSerializer.Serialize(new { version = BuildStamp.Version, commit = BuildStamp.Commit, dirty = BuildStamp.Dirty }),
            json);
    }

    [TestMethod]
    public void VersionIsAnsweredByTheCliAndNotByThePipe()
    {
        Assert.Contains("winland [-j] version\n", ControlInvocation.Usage, StringComparison.Ordinal);
        Assert.IsFalse(ControlInvocation.TryCreate(["version"], out _, out _));

        var core = new WindowManager();
        Assert.AreEqual("error: unknown command 'version'", core.Execute("version").Text);
        Assert.AreEqual("error: unknown command 'version'", core.Execute("j/version").Text);
    }

    [TestMethod]
    public void KeyboardLayoutAndTheDevicesCommandAreNotApplied()
    {
        var core = new WindowManager();

        Assert.AreEqual("Unsupported keyword 'input:kb_variant'.", Assert.ContainsSingle(ConfigParser.Parse("input:kb_variant = nodeadkeys\n").Warnings).Message);
        Assert.AreEqual("error: unknown command 'devices'", core.Execute("devices").Text);
        Assert.AreEqual("error: unknown command 'devices'", core.Execute("j/devices").Text);
        Assert.AreEqual("error: unknown command 'switchxkblayout'", core.Execute("switchxkblayout all next").Text);
    }

    [TestMethod]
    public void ClientsOmitMinimizedWindows()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 400, 300);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        core.Handle(new WindowMinimized(new WindowId(2)));
        core.Execute("workspace 2");
        core.Handle(new WindowAppeared(new WindowId(3), area));

        var shown = Json(core.Execute("j/get windows").Text).EnumerateArray().ToArray();
        Assert.AreEqual(2, shown.Length);
        Assert.AreEqual("0x1", shown[0].GetProperty("address").GetString());
        Assert.AreEqual("0x3", shown[1].GetProperty("address").GetString());
    }

    [TestMethod]
    public void ControlToolMapsFlagsAndSubcommands()
    {
        Assert.IsTrue(ControlInvocation.TryCreate(["-j", "get", "binds"], out var line, out var events));
        Assert.IsFalse(events);
        Assert.AreEqual("j/get binds", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["get", "monitors", "all", "-j"], out line, out events));
        Assert.AreEqual("j/get monitors all", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["reload"], out line, out events));
        Assert.AreEqual("reload", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["resizeactive", "-20", "0"], out line, out events));
        Assert.AreEqual("resizeactive -20 0", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["exec", "wt", "-p", "pwsh"], out line, out events));
        Assert.AreEqual("exec wt -p pwsh", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["exec", @"C:\Program Files\App\app.exe", "--flag"], out line, out events));
        Assert.AreEqual(@"exec ""C:\Program Files\App\app.exe"" --flag", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["exec", @"notepad C:\a b.txt"], out line, out events));
        Assert.AreEqual(@"exec notepad C:\a b.txt", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["exec", "[float; size 800 600] notepad", "x y"], out line, out events));
        Assert.AreEqual(@"exec [float; size 800 600] notepad ""x y""", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["exec", "wsl", "--", "ls"], out line, out events));
        Assert.AreEqual("exec wsl -- ls", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["exec", "notepad", "-j"], out line, out events));
        Assert.AreEqual("exec notepad -j", line);
        Assert.IsTrue(ControlInvocation.TryCreate(["-j", "exec", "notepad"], out line, out events));
        Assert.AreEqual("j/exec notepad", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["EXEC", "wt", "-p", "pwsh"], out line, out events));
        Assert.AreEqual("EXEC wt -p pwsh", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["events"], out line, out events));
        Assert.IsTrue(events);
        Assert.IsTrue(ControlInvocation.TryCreate(["-j", "events"], out line, out events));
        Assert.IsTrue(events);
        Assert.AreEqual("j/events", line);
        Assert.IsFalse(ControlInvocation.TryCreate(["nope"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["dispatch", "killactive"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["clients"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["get", "clients"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["get"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["-r", "get", "windows"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["-a", "get", "windows"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["eval", "1"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["repl"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["plugin", "list"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["plugin", "install", "https://example.com/plugins.git"], out _, out _));
    }

    [TestMethod]
    public void ControlToolTakesEveryVerbThePipeAnswersAndListsIt()
    {
        foreach (var form in CommandProtocol.VerbForms)
            Assert.Contains(" " + form + "\n", ControlInvocation.Usage, StringComparison.Ordinal);

        foreach (var verb in CommandProtocol.VerbNames)
            Assert.IsTrue(ControlInvocation.TryCreate(verb.Split(' '), out _, out _), verb);

        Assert.Contains("winland [-j] <action> [args]\n", ControlInvocation.Usage, StringComparison.Ordinal);
        Assert.IsFalse(ControlInvocation.TryCreate(["get", "windows", "extra"], out _, out _));
    }

    [TestMethod]
    [DataRow(new[] { "resizeactive", "-0.1", "0" }, "resizeactive -0.1 0")]
    [DataRow(new[] { "resizeactive", "-1.5", "-2.25" }, "resizeactive -1.5 -2.25")]
    [DataRow(new[] { "-j", "resizeactive", "-12", "0" }, "j/resizeactive -12 0")]
    public void ControlToolPassesNegativeNumbersThrough(string[] args, string expected)
    {
        Assert.IsTrue(ControlInvocation.TryCreate(args, out var line, out _));
        Assert.AreEqual(expected, line);
    }

    [TestMethod]
    [DataRow("-x")]
    [DataRow("-1.")]
    [DataRow("-1.2.3")]
    [DataRow("-")]
    [DataRow("-50,")]
    public void ControlToolRefusesAFlagThatIsNotANumber(string arg)
    {
        Assert.IsFalse(ControlInvocation.TryCreate(["resizeactive", arg], out _, out _));
    }

    private static Rectangle PlacedAt(Outcome outcome, long window)
    {
        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(window), outcome.State.Clients);
        return Assert.ContainsSingle(placement => placement.Client == client.Id, outcome.Placements).Bounds;
    }
}
