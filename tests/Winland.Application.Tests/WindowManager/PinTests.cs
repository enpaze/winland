namespace Winland.Application.Tests;

[TestClass]
public class PinTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly WindowId Window = new(1);

    [TestMethod]
    public void PinnedClientStaysVisibleAcrossWorkspaceSwitches()
    {
        var core = Floating();
        core.Execute("pin");

        var switched = core.Execute("workspace 2");

        Assert.AreEqual("ok", switched.Text);
        Assert.DoesNotContain(window => window == Window, switched.Effect.Hide);
        Assert.DoesNotContain(client => client.Window == Window, switched.Effect.State.Hidden);
        var client = ClientOf(switched.Effect, Window);
        Assert.IsTrue(client.Pinned);
        Assert.IsTrue(client.Floating);
        Assert.AreEqual(new WorkspaceId("2"), client.Workspace);
        Assert.AreEqual(new WorkspaceId("2"), switched.Effect.State.ActiveWorkspace);
    }

    [TestMethod]
    public void UnpinningLeavesTheClientOnTheShownWorkspace()
    {
        var core = Floating();
        core.Execute("pin");
        core.Execute("workspace 2");

        var unpinned = core.Execute("pin");
        var client = ClientOf(unpinned.Effect, Window);
        Assert.IsFalse(client.Pinned);
        Assert.AreEqual(new WorkspaceId("2"), client.Workspace);
        Assert.DoesNotContain(hidden => hidden.Window == Window, unpinned.Effect.State.Hidden);

        var left = core.Execute("workspace 3");

        Assert.Contains(window => window == Window, left.Effect.Hide);
        Assert.Contains(hidden => hidden.Window == Window, left.Effect.State.Hidden);
        Assert.AreEqual(new WorkspaceId("2"), ClientOf(left.Effect, Window).Workspace);
    }

    [TestMethod]
    public void PinDispatcherEmitsThePinEvent()
    {
        var core = Floating();

        var pinned = core.Execute("pin");
        var again = core.Execute("pin on");
        var cleared = core.Execute("pin off");

        Assert.AreSequenceEqual(["pin>>1,1"], Lines(pinned));
        Assert.IsEmpty(again.Effect.Events);
        Assert.AreSequenceEqual(["pin>>1,0"], Lines(cleared));
        Assert.AreEqual("ok", pinned.Text);
    }

    [TestMethod]
    public void TiledClientDoesNotQualify()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(Window, Area));

        var reply = core.Execute("pin");

        Assert.AreEqual("error: window does not qualify to be pinned", reply.Text);
        Assert.IsEmpty(reply.Effect.Events);
        Assert.IsFalse(ClientOf(reply.Effect, Window).Pinned);
    }

    [TestMethod]
    public void MissingClientIsLeftAlone()
    {
        var core = new WindowManager();

        var reply = core.Execute("pin");

        Assert.AreEqual("ok", reply.Text);
        Assert.IsEmpty(reply.Effect.Events);
    }

    [TestMethod]
    public void PinRuleKeepsAFloatingClientAndSkipsATiledOne()
    {
        var pinned = new WindowManager(ConfigParser.Parse("""
            windowrule = float, class:stick
            windowrule = pin, class:stick
            """));
        var opened = pinned.Handle(new WindowAppeared(Window, Area)
        {
            Traits = WindowTraits.Normal with { Class = "stick" },
        });

        Assert.IsTrue(ClientOf(opened, Window).Pinned);
        Assert.DoesNotContain(stream => stream is ClientPinned, opened.Events);
        var stayed = pinned.Execute("workspace 2");
        Assert.DoesNotContain(window => window == Window, stayed.Effect.Hide);
        Assert.IsTrue(ClientOf(stayed.Effect, Window).Pinned);

        var tiled = new WindowManager(ConfigParser.Parse("windowrule = pin, class:stick\n"));
        var plain = tiled.Handle(new WindowAppeared(Window, Area)
        {
            Traits = WindowTraits.Normal with { Class = "stick" },
        });
        Assert.IsFalse(ClientOf(plain, Window).Pinned);
        Assert.Contains(window => window == Window, tiled.Execute("workspace 2").Effect.Hide);
    }

    [TestMethod]
    public void TrailingZeroClearsThePinRule()
    {
        var document = ConfigParser.Parse("windowrule = pin 0, class:kitty\nwindowrule = pin maybe, class:kitty\n");

        var effect = Assert.ContainsSingle(Assert.ContainsSingle(document.WindowRules).Effects);
        Assert.AreEqual(WindowRuleEffect.Pin, effect.Kind);
        Assert.IsFalse(effect.Enabled);
        Assert.AreEqual(new ConfigWarning(2, "Malformed window rule."), Assert.ContainsSingle(document.Warnings));
    }

    [TestMethod]
    public void StayingPinnedClientIsShownOnTheCurrentWorkspace()
    {
        var core = new WindowManager(ConfigParser.Parse("""
            windowrule = float, class:stick
            windowrule = pin, class:stick
            windowrule = workspace 2 stay, class:stick
            """));

        var opened = core.Handle(new WindowAppeared(Window, Area)
        {
            Traits = WindowTraits.Normal with { Class = "stick" },
        });

        Assert.AreEqual(new WorkspaceId("1"), opened.State.ActiveWorkspace);
        Assert.AreEqual(new WorkspaceId("2"), ClientOf(opened, Window).Workspace);
        Assert.Contains(window => window == Window, opened.Show);
        Assert.DoesNotContain(window => window == Window, opened.Hide);
        Assert.DoesNotContain(client => client.Window == Window, opened.State.Hidden);
    }

    [TestMethod]
    public void FocusStaysOnAPinnedClient()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:stick\n"));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new WindowAppeared(Window, Area)
        {
            Traits = WindowTraits.Normal with { Class = "stick" },
        });
        core.Execute("pin");

        var switched = core.Execute("workspace 2");

        Assert.AreSequenceEqual(
            [
                "createworkspace>>2,2,",
                "movewindow>>1,2,2,",
                "workspace>>2,2,",
                "focus>>,2,2,1,",
            ],
            Lines(switched));
        Assert.Contains(window => window == new WindowId(2), switched.Effect.Hide);
        Assert.DoesNotContain(window => window == Window, switched.Effect.Hide);
        Assert.Contains("\"pinned\":true", core.Execute("j/get windows").Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TilingClearsPinWithoutAnEvent()
    {
        var core = Floating();
        core.Execute("pin");

        var tiled = core.Execute("float");

        Assert.DoesNotContain(stream => stream is ClientPinned, tiled.Effect.Events);
        Assert.IsFalse(ClientOf(tiled.Effect, Window).Pinned);
        Assert.Contains(window => window == Window, core.Execute("workspace 2").Effect.Hide);
    }

    [TestMethod]
    public void APinnedClientIsNotMovedButTheSpecialWorkspaceStillOpens()
    {
        var core = Floating();
        core.Execute("pin");

        var opened = core.Execute("move special:scratch");

        var client = ClientOf(opened.Effect, Window);
        Assert.IsTrue(client.Pinned);
        Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
        Assert.AreEqual(new WorkspaceId("special:scratch"), opened.Effect.State.SpecialWorkspace);
        Assert.AreEqual(Window, opened.Effect.Focus);
    }

    [TestMethod]
    [DataRow(" stay")]
    public void APinnedClientIsNotMovedOntoASpecialWorkspace(string stay)
    {
        var core = Floating();
        core.Execute("pin");

        var refused = core.Execute($"move special:scratch{stay}");

        var client = ClientOf(refused.Effect, Window);
        Assert.IsTrue(client.Pinned);
        Assert.AreEqual(new WorkspaceId("1"), client.Workspace);
        Assert.IsNull(refused.Effect.State.SpecialWorkspace);
        Assert.DoesNotContain(workspace => workspace.Id.IsSpecial, refused.Effect.State.Workspaces);
        Assert.IsEmpty(refused.Effect.Hide);

        var normal = core.Execute($"move 3{stay}");

        Assert.AreEqual(new WorkspaceId("3"), ClientOf(normal.Effect, Window).Workspace);
    }

    [TestMethod]
    public void APinnedClientDoesNotGoFullscreenWhenPinsCannotGoFullscreen()
    {
        var core = Floating();
        core.Execute("pin");

        var fullscreen = core.Execute("maximize");

        var client = ClientOf(fullscreen.Effect, Window);
        Assert.AreEqual(FullscreenMode.None, client.Fullscreen);
        Assert.IsTrue(client.Pinned);
    }

    private static WindowManager Floating()
    {
        var core = new WindowManager(ConfigParser.Parse("windowrule = float, class:stick\n"));
        core.Handle(new WindowAppeared(Window, Area)
        {
            Traits = WindowTraits.Normal with { Class = "stick" },
        });
        return core;
    }

    private static Client ClientOf(Outcome outcome, WindowId window)
    {
        foreach (var client in outcome.State.Clients)
        {
            if (client.Window == window)
                return client;
        }

        throw new InvalidOperationException("missing client");
    }

    private static IReadOnlyList<string> Lines(CommandReply reply) =>
        CommandProtocol.FormatStream(reply.Effect.Events, reply.Effect.State);
}
