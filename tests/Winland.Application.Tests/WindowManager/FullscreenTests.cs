using System.Text.Json;

namespace Winland.Application.Tests;

[TestClass]
public class FullscreenTests
{
    private static readonly Rectangle MonitorBounds = new(0, 0, 1920, 1080);
    private static readonly Rectangle Work = new(0, 40, 1920, 1040);

    [TestMethod]
    public void FullscreenZeroAndTwoToggleTheMonitorAndRestoreTheSlot()
    {
        var core = Desk();
        var before = OpenWindows(core, 2);
        var left = BoundsOf(before, 1);
        var right = BoundsOf(before, 2);

        var entered = core.Handle(new Dispatch("fullscreen", "0"));

        Assert.AreEqual(MonitorBounds, BoundsOf(entered, 2));
        Assert.AreEqual(left, BoundsOf(entered, 1));
        Assert.AreEqual(FullscreenMode.Fullscreen, Client(entered, 2).Fullscreen);
        Assert.AreEqual(FullscreenMode.None, Client(entered, 1).Fullscreen);
        Assert.Contains(static item => item is FullscreenChanged { Entered: true }, entered.Events);
        Assert.IsTrue(DecorationOf(entered, 2).Borderless);
        Assert.IsFalse(DecorationOf(entered, 1).Borderless);

        var again = core.Handle(new Dispatch("fullscreen", "2"));

        Assert.AreEqual(right, BoundsOf(again, 2));
        Assert.AreEqual(left, BoundsOf(again, 1));
        Assert.AreEqual(FullscreenMode.None, Client(again, 2).Fullscreen);
        Assert.Contains(static item => item is FullscreenChanged { Entered: false }, again.Events);
        Assert.IsFalse(DecorationOf(again, 2).Borderless);
    }

    [TestMethod]
    public void MaximizeFillsTheWorkAreaAndKeepsTheBorder()
    {
        var core = Desk();
        OpenWindows(core, 2);
        var left = Bounds(core, 1);

        var entered = core.Handle(new Dispatch("fullscreen", "1"));

        Assert.AreEqual(Work, BoundsOf(entered, 2));
        Assert.AreEqual(left, BoundsOf(entered, 1));
        Assert.AreEqual(FullscreenMode.Maximized, Client(entered, 2).Fullscreen);
        Assert.IsFalse(DecorationOf(entered, 2).Borderless);

        var named = core.Handle(new Dispatch("fullscreen", "maximized"));

        Assert.AreEqual(left, BoundsOf(named, 1));
        Assert.AreEqual(FullscreenMode.None, Client(named, 2).Fullscreen);
        var restored = BoundsOf(named, 2);
        core.Handle(new Dispatch("fullscreen", "1 set"));
        Assert.AreEqual(Work, Bounds(core, 2));
        core.Handle(new Dispatch("fullscreen", "1 unset"));
        Assert.AreEqual(restored, Bounds(core, 2));
        Assert.AreEqual(left, Bounds(core, 1));
    }

    [TestMethod]
    public void MaximizeIsNotADispatcherAndABindToItIsSkipped()
    {
        var core = Desk();
        OpenWindows(core, 1);

        var dispatched = core.Handle(new Dispatch("maximize"));

        Assert.StartsWith("error: unknown dispatcher 'maximize'", dispatched.Error, StringComparison.Ordinal);
        Assert.AreEqual(FullscreenMode.None, Client(dispatched, 1).Fullscreen);

        var document = ConfigParser.Parse("bind = SUPER, M, maximize\nbind = SUPER, F, fullscreen, 1\n", dispatchers: WindowManager.DispatcherNames);

        var warning = Assert.ContainsSingle(document.Warnings);
        Assert.AreEqual(1, warning.Line);
        Assert.StartsWith("Unknown dispatcher 'maximize'", warning.Message, StringComparison.Ordinal);
        Assert.AreEqual("fullscreen", Assert.IsExactInstanceOfType<Bind>(Assert.ContainsSingle(document.Binds)).Dispatcher);
        Assert.IsTrue(ControlInvocation.TryListNames(["dispatch"], out var names));
        Assert.DoesNotContain("maximize", names.Split('\n'));
    }

    [TestMethod]
    public void TreeEditsOnACoveringClientAreRefusedAndTheSlotComesBack()
    {
        var core = Desk();
        OpenWindows(core, 3);
        Rectangle[] before = [Bounds(core, 1), Bounds(core, 2), Bounds(core, 3)];
        core.Handle(new Dispatch("fullscreen"));

        Assert.AreEqual("error: can't togglesplit in the current workspace", core.Handle(new Dispatch("layoutmsg", "togglesplit")).Error);
        Assert.AreEqual("error: can't move fullscreen window", core.Handle(new Dispatch("movewindow", "l")).Error);
        Assert.AreEqual("error: Can't swap fullscreen window", core.Handle(new Dispatch("swapwindow", "l")).Error);
        core.Handle(new Dispatch("fullscreen"));

        Rectangle[] after = [Bounds(core, 1), Bounds(core, 2), Bounds(core, 3)];
        Assert.AreSequenceEqual(before, after);
    }

    [TestMethod]
    public void ASecondCoveringClientReplacesTheFirst()
    {
        var core = Desk();
        OpenWindows(core, 2);
        core.Handle(new Dispatch("fullscreen", "1 set"));

        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        var outcome = core.Handle(new Dispatch("fullscreen", "0 set"));

        Assert.AreEqual(MonitorBounds, BoundsOf(outcome, 1));
        Assert.AreEqual(FullscreenMode.None, Client(outcome, 2).Fullscreen);
        Assert.AreNotEqual(MonitorBounds, BoundsOf(outcome, 2));
    }

    [TestMethod]
    public void RulesRequestFullscreenAndMaximize()
    {
        var core = new WindowManager(ConfigParser.Parse("""
            general:gaps_in = 0
            general:gaps_out = 0
            general:border_size = 0
            windowrule = fullscreen, class:game
            windowrule = maximize, class:edit
            """));
        core.Handle(new MonitorsChanged([Screen()]));

        var game = core.Handle(new WindowAppeared(new WindowId(1), Work)
        {
            Traits = WindowTraits.Normal with { Class = "game" },
        });
        Assert.AreEqual(MonitorBounds, BoundsOf(game, 1));
        Assert.Contains(static item => item is FullscreenChanged { Entered: true }, game.Events);

        var edit = core.Handle(new WindowAppeared(new WindowId(2), Work)
        {
            Traits = WindowTraits.Normal with { Class = "edit" },
        });
        Assert.AreEqual(Work, BoundsOf(edit, 2));
        Assert.AreEqual(FullscreenMode.Maximized, Client(edit, 2).Fullscreen);
    }

    [TestMethod]
    public void ClientsReportTheModesAndTheWorkspace()
    {
        var core = Desk();
        OpenWindows(core, 1);
        core.Handle(new Dispatch("fullscreen", "1"));

        using var clients = JsonDocument.Parse(core.Execute("j/clients").Text);
        var client = clients.RootElement[0];
        Assert.AreEqual(1, client.GetProperty("fullscreen").GetInt32());
        Assert.AreEqual(1, client.GetProperty("fullscreenClient").GetInt32());
        Assert.AreEqual("default", client.GetProperty("fullscreenHandler").GetString());

        using var workspaces = JsonDocument.Parse(core.Execute("j/workspaces").Text);
        Assert.IsTrue(workspaces.RootElement[0].GetProperty("hasfullscreen").GetBoolean());
    }

    [TestMethod]
    public void FullscreenWithoutAClientIsAnErrorAndABadActionDoesNothing()
    {
        var core = Desk();
        var missing = core.Execute("dispatch fullscreen");
        Assert.AreEqual("error: window not found", missing.Text);

        OpenWindows(core, 1);
        var ignored = core.Handle(new Dispatch("fullscreen", "1 sideways"));
        Assert.AreEqual(FullscreenMode.None, Client(ignored, 1).Fullscreen);
        Assert.DoesNotContain(static item => item is FullscreenChanged, ignored.Events);

        core.Handle(new Dispatch("fullscreen", "1 set"));
        var repeat = core.Handle(new Dispatch("fullscreen", "1 set"));
        Assert.AreEqual(FullscreenMode.Maximized, Client(repeat, 1).Fullscreen);
        Assert.DoesNotContain(static item => item is FullscreenChanged, repeat.Events);
    }

    [TestMethod]
    public void AnExclusiveFullscreenWindowIsNotAdopted()
    {
        var core = Desk();
        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Work)
        {
            Bounds = MonitorBounds,
            Traits = WindowTraits.Normal with { ExclusiveFullscreen = true },
        });

        Assert.IsEmpty(outcome.State.Clients);
    }

    [TestMethod]
    public void APopupThatCoversTheMonitorIsExclusive()
    {
        Assert.IsTrue(ExclusiveFullscreenWindow.Is(ExclusiveFullscreenWindow.Popup, MonitorBounds, MonitorBounds));
        Assert.IsFalse(ExclusiveFullscreenWindow.Is(ExclusiveFullscreenWindow.Popup | ExclusiveFullscreenWindow.Caption, MonitorBounds, MonitorBounds));
        Assert.IsFalse(ExclusiveFullscreenWindow.Is(ExclusiveFullscreenWindow.Popup, new Rectangle(10, 10, 100, 100), MonitorBounds));
    }

    [TestMethod]
    public void AFloatingClientReturnsToItsFloatAfterFullscreen()
    {
        var core = Desk();
        OpenWindows(core, 1);
        var floated = core.Handle(new Dispatch("togglefloating"));
        var slot = BoundsOf(floated, 1);
        core.Handle(new Dispatch("fullscreen"));

        var back = core.Handle(new Dispatch("fullscreen"));

        Assert.AreEqual(slot, BoundsOf(back, 1));
        Assert.IsTrue(Client(back, 1).Floating);
    }

    [TestMethod]
    [DataRow("movetoworkspace")]
    [DataRow("movetoworkspacesilent")]
    public void AFullscreenClientStaysFullscreenOnTheWorkspaceItMovesTo(string dispatcher)
    {
        var core = Desk();
        OpenWindows(core, 2);
        core.Handle(new Dispatch("fullscreen", "1"));

        var moved = core.Handle(new Dispatch(dispatcher, "2"));

        var client = Client(moved, 2);
        Assert.AreEqual(new WorkspaceId("2"), client.Workspace);
        Assert.AreEqual(FullscreenMode.Maximized, client.Fullscreen);
        Assert.AreEqual(Work, Bounds(core, 2));
    }

    [TestMethod]
    public void AFullscreenClientMovedOntoAnotherReplacesIt()
    {
        var core = Desk();
        core.Handle(new WindowAppeared(new WindowId(1), Work));
        core.Handle(new Dispatch("fullscreen"));
        core.Handle(new Dispatch("workspace", "2"));
        core.Handle(new WindowAppeared(new WindowId(2), Work));
        core.Handle(new Dispatch("fullscreen"));

        var moved = core.Handle(new Dispatch("movetoworkspace", "1"));

        Assert.AreEqual(FullscreenMode.Fullscreen, Client(moved, 2).Fullscreen);
        Assert.AreEqual(FullscreenMode.None, Client(moved, 1).Fullscreen);
        Assert.AreEqual(MonitorBounds, BoundsOf(moved, 2));
    }

    [TestMethod]
    public void AFullscreenClientStaysFullscreenOnTheMonitorItMovesTo()
    {
        var core = new WindowManager();
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 984));
        core.Handle(new MonitorsChanged([Screen(), right]));
        core.Handle(new WindowAppeared(new WindowId(1), Work));
        core.Handle(new Dispatch("fullscreen"));

        var moved = core.Handle(new Dispatch("movewindow", "mon:" + right.Name));

        Assert.AreEqual(FullscreenMode.Fullscreen, Client(moved, 1).Fullscreen);
        Assert.AreEqual(new Rectangle(1920, 0, 1280, 1024), Bounds(core, 1));
    }

    [TestMethod]
    public void ChangingTheLayoutOnReloadKeepsAFullscreenClient()
    {
        var core = Desk();
        OpenWindows(core, 2);
        core.Handle(new Dispatch("fullscreen"));

        var reloaded = core.Handle(new Configure(ConfigParser.Parse("general:layout = master\n")));

        Assert.AreEqual(FullscreenMode.Fullscreen, Client(reloaded, 2).Fullscreen);
        Assert.AreEqual(MonitorBounds, Bounds(core, 2));
    }

    [TestMethod]
    public void MaximizeLeavesGapsOutAndTheBorder()
    {
        var core = Desk("general:gaps_out = 20\ngeneral:border_size = 2\n");
        OpenWindows(core, 2);

        var maximized = core.Handle(new Dispatch("fullscreen", "1"));

        Assert.AreEqual(new Rectangle(22, 62, 1876, 996), BoundsOf(maximized, 2));
    }

    [TestMethod]
    public void AFloatingClientMaximizesInsideTheWorkAreaWithoutGapsOut()
    {
        var core = Desk("general:gaps_out = 20\ngeneral:border_size = 2\n");
        OpenWindows(core, 1);
        core.Handle(new Dispatch("togglefloating"));

        var maximized = core.Handle(new Dispatch("fullscreen", "1"));

        Assert.AreEqual(new Rectangle(2, 42, 1916, 1036), BoundsOf(maximized, 1));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AMaximizedGroupMemberLeavesRoomForTheBar(bool floating)
    {
        var core = Desk("general:gaps_out = 0\ngeneral:border_size = 0\n");
        OpenWindows(core, 1);
        core.Handle(new Dispatch("togglegroup"));
        core.Handle(new WindowAppeared(new WindowId(2), Work));
        if (floating)
            core.Handle(new Dispatch("togglefloating"));

        var maximized = core.Handle(new Dispatch("fullscreen", "1"));

        Assert.AreEqual(new Rectangle(0, 61, 1920, 1019), BoundsOf(maximized, 2));
    }

    [TestMethod]
    [DataRow("0", FullscreenMode.Fullscreen, "Toggle", true)]
    [DataRow("1 default", FullscreenMode.Maximized, "Toggle", false)]
    [DataRow("0 set default", FullscreenMode.Fullscreen, "Set", false)]
    [DataRow("1 unset", FullscreenMode.Maximized, "Unset", true)]
    public void ALastWordDefaultAsksForTheDefaultFullscreen(string arguments, FullscreenMode mode, string action, bool layoutAware)
    {
        var parsed = FullscreenCommands.Parse(arguments);

        Assert.IsFalse(parsed.Ignored);
        Assert.AreEqual(mode, parsed.Mode);
        Assert.AreEqual(action, parsed.Action.ToString());
        Assert.AreEqual(layoutAware, parsed.LayoutAware);
    }

    [TestMethod]
    public void TheScrollingLayoutKeepsAFullscreenTileOnItsTapeWhenFocusScrollsAway()
    {
        var core = Desk("general:layout = scrolling\n");
        OpenWindows(core, 2);

        var entered = core.Handle(new Dispatch("fullscreen", "0"));
        Assert.AreEqual(MonitorBounds, BoundsOf(entered, 2));
        Assert.IsTrue(Client(entered, 2).LayoutFullscreen);

        var away = core.Handle(new Dispatch("focuswindow", "address:0x1"));
        var held = Client(away, 2);
        Assert.AreEqual(FullscreenMode.Fullscreen, held.Fullscreen);
        Assert.AreEqual((MonitorBounds.Width, MonitorBounds.Height), (held.Bounds.Width, held.Bounds.Height));
        Assert.AreNotEqual(MonitorBounds.X, held.Bounds.X);

        using var clients = JsonDocument.Parse(core.Execute("j/clients").Text);
        foreach (var client in clients.RootElement.EnumerateArray())
{
    Assert.AreEqual("scrolling", client.GetProperty("fullscreenHandler").GetString());
}
    }

    [TestMethod]
    public void DefaultFullscreenInTheScrollingLayoutCoversTheMonitorAsElsewhere()
    {
        var core = Desk("general:layout = scrolling\n");
        OpenWindows(core, 2);

        var entered = core.Handle(new Dispatch("fullscreen", "0 default"));

        Assert.AreEqual(MonitorBounds, BoundsOf(entered, 2));
        Assert.IsFalse(Client(entered, 2).LayoutFullscreen);
        using var active = JsonDocument.Parse(core.Execute("j/activewindow").Text);
        Assert.AreEqual("default", active.RootElement.GetProperty("fullscreenHandler").GetString());
    }

    [TestMethod]
    public void AFloatingWindowUnderAFullscreenTileIsLoweredAndRaisedWhenItEnds()
    {
        var core = Desk();
        OpenWindows(core, 2);
        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        core.Handle(new Dispatch("togglefloating"));
        core.Handle(new Dispatch("focuswindow", "address:0x2"));

        var covered = core.Handle(new Dispatch("fullscreen", "0"));

        Assert.Contains(new ZOrderRequest(new WindowId(1), Top: false), covered.ZOrder);
        Assert.IsFalse(Client(covered, 1).AllowedOverFullscreen);
        Assert.IsTrue(Client(covered, 1).BelowFullscreen);
        using (var clients = JsonDocument.Parse(core.Execute("j/clients").Text))
        {
            var floating = clients.RootElement.EnumerateArray().Single(client => client.GetProperty("address").GetString() == "0x1");
            Assert.IsFalse(floating.GetProperty("acceptsInput").GetBoolean());
            Assert.IsFalse(floating.GetProperty("visible").GetBoolean());
        }

        var ended = core.Handle(new Dispatch("fullscreen", "0"));

        Assert.Contains(new ZOrderRequest(new WindowId(1), Top: true), ended.ZOrder);
        Assert.IsTrue(Client(ended, 1).AllowedOverFullscreen);
        Assert.IsFalse(Client(ended, 1).BelowFullscreen);
    }

    private static WindowManager Desk(string config)
    {
        var core = new WindowManager();
        core.Handle(new Configure(ConfigParser.Parse(config)));
        core.Handle(new MonitorsChanged([Screen()]));
        return core;
    }

    private static WindowManager Desk()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([Screen()]));
        return core;
    }

    private static Monitor Screen() =>
        new(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Work);

    private static Outcome OpenWindows(WindowManager core, int count)
    {
        Outcome outcome = null!;
        for (var id = 1; id <= count; id++)
            outcome = Appear(core, id, Work);
        return outcome;
    }

    private static Rectangle Bounds(WindowManager core, long window) =>
        Client(core.Handle(new Dispatch("resizewindow")), window).Bounds;

    private static Decoration DecorationOf(Outcome outcome, long window)
    {
        var client = Client(outcome, window);
        return Assert.ContainsSingle(candidate => candidate.Client == client.Id, outcome.Decorations);
    }
}
