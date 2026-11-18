namespace Winland.Application.Tests;

[TestClass]
public class EventTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void OpeningAClientEmitsOpenThenFocus()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty", Title = "terminal" },
        });

        Assert.AreSequenceEqual(
            ["openwindow>>1,1,,kitty,terminal", "activewindow>>kitty,terminal", "activewindowv2>>1"],
            Lines(outcome));
    }

    [TestMethod]
    public void OpeningKeepsACommaInTheTitle()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "app", Title = "a,b" },
        });

        Assert.AreEqual("openwindow>>1,1,,app,a,b", Lines(outcome)[0]);
    }

    [TestMethod]
    public void OpeningFlattensANewlineInTheTitle()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Title = "a\nb" },
        });

        Assert.AreEqual("openwindow>>1,1,,,a b", Lines(outcome)[0]);
    }

    [TestMethod]
    public void RejectedWindowEmitsNothing()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Tool = true },
        });

        Assert.IsEmpty(outcome.Events);
    }

    [TestMethod]
    public void SilentWorkspaceOpenDoesNotChangeFocus()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2 silent, class:kitty\n"));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty", Title = "term" },
        });

        Assert.AreSequenceEqual(
            ["createworkspace>>2,", "createworkspacev2>>2,2,", "openwindow>>1,2,,kitty,term"],
            Lines(outcome));
    }

    [TestMethod]
    public void WorkspaceRuleOpenSwitchesThenFocuses()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = workspace 2, class:kitty\n"));

        var outcome = core.Handle(new WindowAppeared(new WindowId(1), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty", Title = "term" },
        });

        Assert.AreSequenceEqual(
            [
                "createworkspace>>2,",
                "createworkspacev2>>2,2,",
                "destroyworkspace>>1,",
                "destroyworkspacev2>>1,1,",
                "openwindow>>1,2,,kitty,term",
                "workspace>>2,",
                "workspacev2>>2,2,",
                "activewindow>>kitty,term",
                "activewindowv2>>1",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void ClosingTheFocusedClientEmitsCloseThenTheNextFocus()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new WindowClosed(new WindowId(2)));

        Assert.AreSequenceEqual(["closewindow>>2", "activewindow>>,", "activewindowv2>>1"], Lines(outcome));
    }

    [TestMethod]
    public void ClosingAnUnfocusedClientEmitsOnlyClose()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new WindowClosed(new WindowId(1)));

        Assert.AreSequenceEqual(["closewindow>>1"], Lines(outcome));
    }

    [TestMethod]
    public void ClosingTheLastClientOnAHiddenWorkspaceDestroysIt()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(7), Area));
        core.Execute("dispatch workspace 2");

        var closed = core.Handle(new WindowClosed(new WindowId(7)));

        Assert.AreSequenceEqual(["destroyworkspace>>1,", "destroyworkspacev2>>1,1,", "closewindow>>7"], Lines(closed));
    }

    [TestMethod]
    public void ClosingTheLastClientClearsFocus()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowClosed(new WindowId(1)));

        Assert.AreSequenceEqual(["closewindow>>1", "activewindow>>,", "activewindowv2>>"], Lines(outcome));
    }

    [TestMethod]
    public void KillActiveWaitsForTheWindowToClose()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var killed = core.Execute("dispatch killactive");
        var closed = core.Handle(new WindowClosed(new WindowId(1)));

        Assert.IsEmpty(killed.Effect.Events);
        Assert.AreSequenceEqual(["closewindow>>1", "activewindow>>,", "activewindowv2>>"], Lines(closed));
    }

    [TestMethod]
    public void FocusDispatcherEmitsTheNewAddress()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new Dispatch("movefocus", "l"));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        Assert.AreSequenceEqual(["activewindow>>,", "activewindowv2>>1"], Lines(outcome));
    }

    [TestMethod]
    public void ReportedFocusEmitsActiveWindow()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Handle(new WindowFocused(new WindowId(1)));

        Assert.AreSequenceEqual(["activewindow>>,", "activewindowv2>>1"], Lines(outcome));
    }

    [TestMethod]
    public void FocusingTheSameClientEmitsNothing()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Handle(new WindowFocused(new WindowId(1)));

        Assert.IsEmpty(outcome.Events);
    }

    [TestMethod]
    public void WorkspaceSwitchEmitsTheDeskThenClearsFocus()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var outcome = core.Execute("dispatch workspace 2");

        Assert.AreSequenceEqual(
            [
                "createworkspace>>2,",
                "createworkspacev2>>2,2,",
                "workspace>>2,",
                "workspacev2>>2,2,",
                "activewindow>>,",
                "activewindowv2>>",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void SwitchingBackRestoresFocusAfterTheDeskEvent()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Execute("dispatch workspace 2");

        var outcome = core.Execute("dispatch workspace 1");

        Assert.AreSequenceEqual(
            [
                "destroyworkspace>>2,",
                "destroyworkspacev2>>2,2,",
                "workspace>>1,",
                "workspacev2>>1,1,",
                "activewindow>>,",
                "activewindowv2>>1",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void StayingOnTheSameWorkspaceEmitsNothing()
    {
        var core = new WindowManager();

        var outcome = core.Execute("dispatch workspace 1");

        Assert.IsEmpty(outcome.Effect.Events);
    }

    [TestMethod]
    public void SilentMoveEmitsTheDestinationThenTheReplacementFocus()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Execute("dispatch movetoworkspacesilent 3");

        Assert.AreSequenceEqual(
            [
                "createworkspace>>3,",
                "createworkspacev2>>3,3,",
                "movewindow>>2,3,",
                "movewindowv2>>2,3,3,",
                "activewindow>>,",
                "activewindowv2>>1",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void FollowingAMoveEmitsTheMoveThenTheDesk()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));

        var outcome = core.Execute("dispatch movetoworkspace 4");

        Assert.AreSequenceEqual(
            [
                "createworkspace>>4,",
                "createworkspacev2>>4,4,",
                "movewindow>>2,4,",
                "movewindowv2>>2,4,4,",
                "workspace>>4,",
                "workspacev2>>4,4,",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void MinimizeAndRestoreEmitTheFlag()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var minimized = core.Handle(new WindowMinimized(new WindowId(1)));
        var restored = core.Handle(new WindowRestored(new WindowId(1)));

        Assert.AreSequenceEqual(["minimized>>1,1"], Lines(minimized));
        Assert.AreSequenceEqual(["minimized>>1,0"], Lines(restored));
    }

    [TestMethod]
    public void TogglingFloatEmitsTheMode()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(0xFF), Area));

        core.Execute("dispatch focuswindow address:0x1");
        var lifted = core.Execute("dispatch togglefloating");
        core.Execute("dispatch focuswindow address:0x1");
        var dropped = core.Execute("dispatch togglefloating");

        Assert.AreSequenceEqual(["changefloatingmode>>1,1"], Lines(lifted));
        Assert.AreSequenceEqual(["changefloatingmode>>1,0"], Lines(dropped));
    }

    [TestMethod]
    public void EventAddressesAreBareHexAndTheReadKeepsThePrefix()
    {
        var core = new WindowManager();
        var outcome = core.Handle(new WindowAppeared(new WindowId(0x2A), Area)
        {
            Traits = WindowTraits.Normal with { Class = "kitty", Title = "term" },
        });

        Assert.AreSequenceEqual(
            ["openwindow>>2a,1,,kitty,term", "activewindow>>kitty,term", "activewindowv2>>2a"],
            Lines(outcome));
        using var active = System.Text.Json.JsonDocument.Parse(core.Execute("j/activewindow").Text);
        Assert.AreEqual("0x2a", active.RootElement.GetProperty("address").GetString());
    }

    [TestMethod]
    public void ConfigWarningsEmitInOrder()
    {
        var outcome = new WindowManager().Handle(new ConfigLoaded(ConfigParser.Parse("ghostkeyword = path\nnope = 1\n")));

        Assert.AreSequenceEqual(
            [
                "warning>>1,,Unsupported keyword 'ghostkeyword'.",
                "warning>>2,,Unsupported keyword 'nope'.",
                "configreloaded>>",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void SourcedWarningKeepsTheFileAndFlattensNewlines()
    {
        var document = new ConfigDocument(
            [],
            [],
            [new ConfigWarning(4, "bad\r\nline, still", "bad.conf")]);

        var outcome = new WindowManager().Handle(new ConfigLoaded(document));

        Assert.AreSequenceEqual(["warning>>4,bad.conf,bad line, still", "configreloaded>>"], Lines(outcome));
    }

    [TestMethod]
    public void ConnectingMonitorsEmitsAddedThenTheDescribedForm()
    {
        var core = new WindowManager();

        var outcome = core.Handle(new MonitorsChanged([
            new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area),
            new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024)),
        ]));

        Assert.AreSequenceEqual(
            [
                @"createworkspace>>1,\\.\DISPLAY2",
                @"createworkspacev2>>1,1,\\.\DISPLAY2",
                @"monitoradded>>\\.\DISPLAY1",
                @"monitoraddedv2>>0,\\.\DISPLAY1,Dell",
                @"monitoradded>>\\.\DISPLAY2",
                @"monitoraddedv2>>1,\\.\DISPLAY2,Samsung",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void UnpluggingAMonitorEmitsRemovedThenTheDescribedForm()
    {
        var core = new WindowManager();
        var left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area);
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024));
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea));

        var outcome = core.Handle(new MonitorsChanged([left]));

        Assert.AreSequenceEqual(
            [
                @"moveworkspace>>2,\\.\DISPLAY1",
                @"moveworkspacev2>>2,2,\\.\DISPLAY1",
                @"monitorremoved>>\\.\DISPLAY2",
                @"monitorremovedv2>>1,\\.\DISPLAY2,Samsung",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void UnpluggingAMonitorDestroysItsEmptyWorkspace()
    {
        var core = new WindowManager();
        var left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area);
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024));
        core.Handle(new MonitorsChanged([left, right]));

        var outcome = core.Handle(new MonitorsChanged([left]));

        Assert.AreSequenceEqual(
            [
                @"destroyworkspace>>1,\\.\DISPLAY2",
                @"destroyworkspacev2>>1,1,\\.\DISPLAY2",
                @"monitorremoved>>\\.\DISPLAY2",
                @"monitorremovedv2>>1,\\.\DISPLAY2,Samsung",
            ],
            Lines(outcome));
        Assert.DoesNotContain(workspace => workspace.Id == new WorkspaceId("1", 1), outcome.State.Workspaces);
    }

    [TestMethod]
    public void FocusingAWorkspaceOnAnotherMonitorEmitsTheMonitor()
    {
        var core = new WindowManager();
        var left = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area);
        var right = new Monitor(@"\\.\DISPLAY2", "Samsung", 1920, 0, 1280, 1024, 1, true, new Rectangle(1920, 0, 1280, 1024));
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), left.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "left", Title = "one" },
        });
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea)
        {
            Traits = WindowTraits.Normal with { Class = "right", Title = "two" },
        });

        var outcome = core.Execute(@"dispatch focusmonitor \\.\DISPLAY2");

        Assert.AreSequenceEqual(
            [
                @"focusedmon>>\\.\DISPLAY2,1",
                @"focusedmonv2>>\\.\DISPLAY2,1",
                "activewindow>>right,two",
                "activewindowv2>>2",
            ],
            Lines(outcome));
    }

    [TestMethod]
    public void ReportingTheSameMonitorsEmitsNothing()
    {
        var core = new WindowManager();
        var display = new Monitor(@"\\.\DISPLAY1", "Dell", 0, 0, 1920, 1080, 1, true, Area);
        core.Handle(new MonitorsChanged([display]));

        var outcome = core.Handle(new MonitorsChanged([display]));

        Assert.IsEmpty(outcome.Events);
    }

    [TestMethod]
    public void EventsCommandIsAStreamAndLeavesTheDeskAlone()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));

        var reply = core.Execute("events");

        Assert.IsTrue(reply.Stream);
        Assert.AreEqual("", reply.Text);
        Assert.IsEmpty(reply.Effect.Events);
        Assert.ContainsSingle(reply.Effect.State.Clients);
        Assert.AreEqual("error: unknown request", core.Execute("events extra").Text);
    }

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        Lines(reply.Effect);
}
