using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class ExtraCommandTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private const string SuperKill =
        """
        [{"mouse":false,"has_description":false,"modmask":64,"key":"Q","keycode":0,"description":"","dispatcher":"killactive","arg":""}]
        """;

    [TestMethod]
    public void FlagPrefixSelectsJson()
    {
        var json = Assert.IsExactInstanceOfType<CommandParsed>(CommandProtocol.Parse("j/binds"));
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
        Assert.AreEqual("[]", core.Execute("j/binds").Text);

        core.Handle(new Configure(ConfigParser.Parse("bind = SUPER, Q, killactive\n")));
        Assert.AreEqual(SuperKill, core.Execute("j/binds").Text);

        core.Handle(new Configure(ConfigParser.Parse("""
            bind = SUPER, Q, killactive
            bind = SUPER SHIFT, E, killactive
            bindd = SUPER, F, Close, killactive
            bind = SUPER, code:42, killactive
            """)));
        var binds = Json(core.Execute("j/binds").Text);
        var second = binds[1];
        Assert.AreEqual(65, second.GetProperty("modmask").GetInt32());
        Assert.AreEqual("E", second.GetProperty("key").GetString());

        var described = binds[2];
        Assert.IsTrue(described.GetProperty("has_description").GetBoolean());
        Assert.AreEqual("Close", described.GetProperty("description").GetString());

        var coded = binds[3];
        Assert.AreEqual("", coded.GetProperty("key").GetString());
        Assert.AreEqual(42, coded.GetProperty("keycode").GetInt32());
    }

    [TestMethod]
    public void VersionHasItsShape()
    {
        Assert.IsTrue(ControlInvocation.TryAnswerLocally(["-j", "version"], out var json));
        var version = Json(json);
        Assert.AreEqual(BuildStamp.Version, version.GetProperty("version").GetString());
        Assert.AreEqual(BuildStamp.Commit, version.GetProperty("commit").GetString());
        Assert.AreEqual("", version.GetProperty("tag").GetString());
        Assert.AreEqual(JsonValueKind.Array, version.GetProperty("flags").ValueKind);
        Assert.StartsWith("{\"branch\":", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void VersionIsAnsweredByTheCliAndNotByThePipe()
    {
        Assert.Contains("winland [-j] version\n", ControlInvocation.Usage, StringComparison.Ordinal);
        Assert.IsFalse(ControlInvocation.TryCreate(["version"], out _, out _));

        var core = new WindowManager();
        Assert.AreEqual("error: unknown request", core.Execute("version").Text);
        Assert.AreEqual("error: unknown request", core.Execute("j/version").Text);
    }

    [TestMethod]
    public void KeyboardLayoutAndTheDevicesCommandAreNotApplied()
    {
        var core = new WindowManager();

        Assert.AreEqual("Unsupported keyword 'input:kb_variant'.", Assert.ContainsSingle(ConfigParser.Parse("input:kb_variant = nodeadkeys\n").Warnings).Message);
        Assert.AreEqual("error: unknown request", core.Execute("devices").Text);
        Assert.AreEqual("error: unknown request", core.Execute("j/devices").Text);
        Assert.AreEqual("error: unknown request", core.Execute("switchxkblayout all next").Text);
    }

    [TestMethod]
    public void ClientsOmitMinimizedWindows()
    {
        var core = new WindowManager();
        var area = new Rectangle(0, 0, 400, 300);
        core.Handle(new WindowAppeared(new WindowId(1), area));
        core.Handle(new WindowAppeared(new WindowId(2), area));
        core.Handle(new WindowMinimized(new WindowId(2)));
        core.Execute("dispatch workspace 2");
        core.Handle(new WindowAppeared(new WindowId(3), area));

        var shown = Json(core.Execute("j/clients").Text).EnumerateArray().ToArray();
        Assert.AreEqual(2, shown.Length);
        Assert.AreEqual("0x1", shown[0].GetProperty("address").GetString());
        Assert.AreEqual("0x3", shown[1].GetProperty("address").GetString());
    }

    [TestMethod]
    public void ControlToolMapsFlagsAndSubcommands()
    {
        Assert.IsTrue(ControlInvocation.TryCreate(["-j", "binds"], out var line, out var events));
        Assert.IsFalse(events);
        Assert.AreEqual("j/binds", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "resizeactive", "-20", "0"], out line, out events));
        Assert.AreEqual("dispatch resizeactive -20 0", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "exec", "wt", "-p", "pwsh"], out line, out events));
        Assert.AreEqual("dispatch exec wt -p pwsh", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "exec", @"C:\Program Files\App\app.exe", "--flag"], out line, out events));
        Assert.AreEqual(@"dispatch exec ""C:\Program Files\App\app.exe"" --flag", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "exec", @"notepad C:\a b.txt"], out line, out events));
        Assert.AreEqual(@"dispatch exec notepad C:\a b.txt", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "exec", "[float; size 800 600] notepad", "x y"], out line, out events));
        Assert.AreEqual(@"dispatch exec [float; size 800 600] notepad ""x y""", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "exec", "wsl", "--", "ls"], out line, out events));
        Assert.AreEqual("dispatch exec wsl -- ls", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["dispatch", "EXEC", "wt", "-p", "pwsh"], out line, out events));
        Assert.AreEqual("dispatch EXEC wt -p pwsh", line);

        Assert.IsTrue(ControlInvocation.TryCreate(["events"], out line, out events));
        Assert.IsTrue(events);
        Assert.IsFalse(ControlInvocation.TryCreate(["-j", "events"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["nope"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["-r", "clients"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["-a", "clients"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["eval", "1"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["repl"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["plugin", "list"], out _, out _));
        Assert.IsFalse(ControlInvocation.TryCreate(["plugin", "install", "https://example.com/plugins.git"], out _, out _));
    }

    [TestMethod]
    public void ControlToolTakesEveryVerbThePipeAnswersAndListsIt()
    {
        foreach (var form in CommandProtocol.VerbForms)
        {
            Assert.Contains(" " + form + "\n", ControlInvocation.Usage, StringComparison.Ordinal);
            var verb = form.Split(' ')[0];
            if (!form.Contains('<', StringComparison.Ordinal))
                Assert.IsTrue(ControlInvocation.TryCreate([verb], out _, out _), verb);
        }

        Assert.IsFalse(ControlInvocation.TryCreate(["clients", "extra"], out _, out _));
    }

    [TestMethod]
    [DataRow(new[] { "dispatch", "resizeactive", "-0.1", "0" }, "dispatch resizeactive -0.1 0")]
    [DataRow(new[] { "dispatch", "resizeactive", "-1.5", "-2.25" }, "dispatch resizeactive -1.5 -2.25")]
    [DataRow(new[] { "-j", "dispatch", "resizeactive", "-12", "0" }, "j/dispatch resizeactive -12 0")]
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
        Assert.IsFalse(ControlInvocation.TryCreate(["dispatch", "resizeactive", arg], out _, out _));
    }

    private static Rectangle PlacedAt(Outcome outcome, long window)
    {
        var client = Assert.ContainsSingle(candidate => candidate.Window == new WindowId(window), outcome.State.Clients);
        return Assert.ContainsSingle(placement => placement.Client == client.Id, outcome.Placements).Bounds;
    }
}
