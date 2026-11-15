namespace Winland.Application.Tests;

public class SelectorDispatcherTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);

    [Fact]
    public void FocusWindowSwitchesToTheClientsWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2 silent, title:^(away)$\n"));
        Appear(core, 1, "app", "here");
        Appear(core, 2, "app", "away");

        var stayed = core.Handle(new Dispatch("focuswindow", "class:app"));

        Assert.Equal("ok", core.Execute("dispatch focuswindow class:app").Text);
        Assert.Equal(new WindowId(1), stayed.Focus);
        Assert.Equal(new WorkspaceId("1"), stayed.State.ActiveWorkspace);

        var followed = core.Handle(new Dispatch("focuswindow", "title:away"));

        Assert.Equal(new WindowId(2), followed.Focus);
        Assert.Equal(new WorkspaceId("2"), followed.State.ActiveWorkspace);
    }

    [Fact]
    public void FocusWindowMovesToTheClientsMonitor()
    {
        var core = new WindowManager();
        var left = Display(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1);
        var right = Display(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "left" },
        });
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "right" },
        });

        var outcome = core.Handle(new Dispatch("focuswindow", "class:right"));

        Assert.Equal(new WindowId(2), outcome.Focus);
        Assert.Equal(new MonitorId(1), outcome.State.ActiveMonitor);
        Assert.Equal(WorkspaceId.FromNumber(1, new MonitorId(1)), outcome.State.ActiveWorkspace);
    }

    [Fact]
    public void FocusWindowReportsAMiss()
    {
        var core = new WindowManager();
        Appear(core, 1, "app", "here");

        var reply = core.Execute("dispatch focuswindow class:missing");

        Assert.Equal("error: no such window", reply.Text);
        Assert.Null(reply.Effect.Focus);
    }

    [Theory]
    [InlineData("togglefloating class:b")]
    [InlineData("pin class:b")]
    [InlineData("pin enable class:b")]
    [InlineData("movetoworkspace 2,class:b")]
    [InlineData("movetoworkspacesilent 2,address:0x2")]
    [InlineData("swapwindow class:b")]
    [InlineData("moveintogroup address:0x2")]
    [InlineData("moveoutofgroup class:b")]
    [InlineData("movewindow l,address:0x2")]
    [InlineData("fullscreen 1,address:0x2")]
    [InlineData("togglegroup ,class:b")]
    [InlineData("changegroupactive f,class:b")]
    public void ADispatcherGivenAWindowIsRefusedAndChangesNothing(string line)
    {
        var desk = new TestDesktop();
        desk.Spawn("a");
        desk.Spawn("b");
        desk.Ok("focuswindow", "class:a");
        var before = desk.Send("j/clients");

        Assert.StartsWith("error:", desk.Dispatch(line), StringComparison.Ordinal);
        Assert.Equal(before, desk.Send("j/clients"));
    }

    [Fact]
    public void ExitStopsTheDaemon()
    {
        var core = new WindowManager();
        Appear(core, 1, "app", "term");

        var exit = core.Execute("dispatch exit");
        Assert.Equal("ok", exit.Text);
        Assert.True(exit.Effect.Exit);
        Assert.Single(exit.Effect.State.Clients);
    }

    private static void Appear(WindowManager core, long window, string windowClass, string title, long processId = 0) =>
        core.Handle(new WindowAppeared(new WindowId(window), Wide)
        {
            Traits = WindowTraits.Normal with { Class = windowClass, Title = title },
            ProcessId = processId,
        });

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        CommandProtocol.FormatStream(reply.Effect.Events, reply.Effect.State);
}
