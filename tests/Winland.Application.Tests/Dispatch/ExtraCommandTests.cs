using System.Text.Json;

namespace Winland.Application.Tests;

public class ExtraCommandTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    private const string SuperKill =
        """
        [{"mouse":false,"has_description":false,"modmask":64,"key":"Q","keycode":0,"description":"","dispatcher":"killactive","arg":""}]
        """;

    [Fact]
    public void FlagPrefixSelectsJson()
    {
        var json = Assert.IsType<CommandParsed>(CommandProtocol.Parse("j/binds"));
        Assert.IsType<BindsRequest>(json.Request);
        Assert.Equal(CommandFlags.Json, json.Request.Flags);
    }

    [Fact]
    public void PluginListIsAnUnknownCommand()
    {
        Assert.IsType<CommandRejected>(CommandProtocol.Parse("plugin list"));
    }

    [Fact]
    public void BindsReportModifierBitsDescriptionsAndKeycodes()
    {
        var core = new WindowManager();
        Assert.Equal("[]", core.Execute("j/binds").Text);

        core.Handle(new Configure(ConfigParser.Parse("bind = SUPER, Q, killactive\n")));
        Assert.Equal(SuperKill, core.Execute("j/binds").Text);

        core.Handle(new Configure(ConfigParser.Parse("""
            bind = SUPER, Q, killactive
            bind = SUPER SHIFT, E, killactive
            bindd = SUPER, F, Close, killactive
            bind = SUPER, code:42, killactive
            """)));
        var binds = Json(core.Execute("j/binds").Text);
        var second = binds[1];
        Assert.Equal(65, second.GetProperty("modmask").GetInt32());
        Assert.Equal("E", second.GetProperty("key").GetString());

        var described = binds[2];
        Assert.True(described.GetProperty("has_description").GetBoolean());
        Assert.Equal("Close", described.GetProperty("description").GetString());

        var coded = binds[3];
        Assert.Equal("", coded.GetProperty("key").GetString());
        Assert.Equal(42, coded.GetProperty("keycode").GetInt32());
    }

    [Fact]
    public void VersionHasItsShape()
    {
        var core = new WindowManager();
        var version = Json(core.Execute("j/version").Text);
        Assert.Equal(BuildStamp.Version, version.GetProperty("version").GetString());
        Assert.Equal(BuildStamp.Commit, version.GetProperty("commit").GetString());
        Assert.Equal("", version.GetProperty("tag").GetString());
        Assert.Equal(JsonValueKind.Array, version.GetProperty("flags").ValueKind);
        Assert.StartsWith("{\"branch\":", core.Execute("j/version").Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DisableAutoreloadStopsReloadingOnSaveUntilItIsTurnedOff()
    {
        var core = new WindowManager();
        Assert.True(core.ReloadsOnSave);

        core.Handle(new Configure(ConfigParser.Parse("misc:disable_autoreload = true\n")));
        Assert.False(core.ReloadsOnSave);

        core.Handle(new Configure(ConfigParser.Parse("misc:disable_autoreload = false\n")));
        Assert.True(core.ReloadsOnSave);
    }

    [Fact]
    public void KeyboardLayoutAndTheDevicesCommandAreNotApplied()
    {
        var core = new WindowManager();

        Assert.Equal("Unsupported keyword 'input:kb_variant'.", Assert.Single(ConfigParser.Parse("input:kb_variant = nodeadkeys\n").Warnings).Message);
        Assert.Equal("error: unknown request", core.Execute("devices").Text);
        Assert.Equal("error: unknown request", core.Execute("j/devices").Text);
        Assert.Equal("error: unknown request", core.Execute("switchxkblayout all next").Text);
    }

    [Fact]
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
        Assert.Equal(2, shown.Length);
        Assert.Equal("0x1", shown[0].GetProperty("address").GetString());
        Assert.Equal("0x3", shown[1].GetProperty("address").GetString());
    }

    [Fact]
    public void ControlToolMapsFlagsAndSubcommands()
    {
        Assert.True(ControlInvocation.TryCreate(["-j", "binds"], out var line, out var events));
        Assert.False(events);
        Assert.Equal("j/binds", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "resizeactive", "-20", "0"], out line, out events));
        Assert.Equal("dispatch resizeactive -20 0", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "exec", "wt", "-p", "pwsh"], out line, out events));
        Assert.Equal("dispatch exec wt -p pwsh", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "exec", @"C:\Program Files\App\app.exe", "--flag"], out line, out events));
        Assert.Equal(@"dispatch exec ""C:\Program Files\App\app.exe"" --flag", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "exec", @"notepad C:\a b.txt"], out line, out events));
        Assert.Equal(@"dispatch exec notepad C:\a b.txt", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "exec", "[float; size 800 600] notepad", "x y"], out line, out events));
        Assert.Equal(@"dispatch exec [float; size 800 600] notepad ""x y""", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "exec", "wsl", "--", "ls"], out line, out events));
        Assert.Equal("dispatch exec wsl -- ls", line);

        Assert.True(ControlInvocation.TryCreate(["dispatch", "EXEC", "wt", "-p", "pwsh"], out line, out events));
        Assert.Equal("dispatch EXEC wt -p pwsh", line);

        Assert.True(ControlInvocation.TryCreate(["events"], out line, out events));
        Assert.True(events);
        Assert.False(ControlInvocation.TryCreate(["-j", "events"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["nope"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["-r", "clients"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["-a", "clients"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["eval", "1"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["repl"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["plugin", "list"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["plugin", "install", "https://example.com/plugins.git"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["--batch", "binds ; clients"], out _, out _));
        Assert.False(ControlInvocation.TryCreate(["keyword", "general:border_size", "4"], out _, out _));
    }

    [Fact]
    public void ControlToolTakesEveryVerbThePipeAnswersAndListsIt()
    {
        foreach (var form in CommandProtocol.VerbForms)
        {
            Assert.Contains(" " + form + "\n", ControlInvocation.Usage, StringComparison.Ordinal);
            var verb = form.Split(' ')[0];
            if (!form.Contains('<', StringComparison.Ordinal))
                Assert.True(ControlInvocation.TryCreate([verb], out _, out _), verb);
        }

        Assert.False(ControlInvocation.TryCreate(["clients", "extra"], out _, out _));
    }

    [Theory]
    [InlineData(new[] { "dispatch", "resizeactive", "-0.1", "0" }, "dispatch resizeactive -0.1 0")]
    [InlineData(new[] { "dispatch", "resizeactive", "-1.5", "-2.25" }, "dispatch resizeactive -1.5 -2.25")]
    [InlineData(new[] { "-j", "dispatch", "resizeactive", "-12", "0" }, "j/dispatch resizeactive -12 0")]
    public void ControlToolPassesNegativeNumbersThrough(string[] args, string expected)
    {
        Assert.True(ControlInvocation.TryCreate(args, out var line, out _));
        Assert.Equal(expected, line);
    }

    [Theory]
    [InlineData("-x")]
    [InlineData("-1.")]
    [InlineData("-1.2.3")]
    [InlineData("-")]
    [InlineData("-50,")]
    public void ControlToolRefusesAFlagThatIsNotANumber(string arg)
    {
        Assert.False(ControlInvocation.TryCreate(["dispatch", "resizeactive", arg], out _, out _));
    }

    private static Rectangle PlacedAt(Outcome outcome, long window)
    {
        var client = Assert.Single(outcome.State.Clients, candidate => candidate.Window == new WindowId(window));
        return Assert.Single(outcome.Placements, placement => placement.Client == client.Id).Bounds;
    }
}
