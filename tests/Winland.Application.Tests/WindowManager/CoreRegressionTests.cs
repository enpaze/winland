namespace Winland.Application.Tests;

[TestClass]
public class CoreRegressionTests
{
    private static readonly Rectangle Area = new(0, 0, 1000, 800);

    [TestMethod]
    public void ClosingAFloatingClientSkipsTheHiddenGroupMemberInFocusHistory()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new Dispatch("group"));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        core.Handle(new WindowAppeared(new WindowId(3), Area) { Traits = WindowTraits.Normal with { Dialog = true } });

        var closed = core.Handle(new WindowClosed(new WindowId(3)));

        Assert.AreEqual(new WindowId(2), closed.Focus);
        Assert.DoesNotContain(client => client.Window == closed.Focus, closed.State.Hidden);
    }

    [TestMethod]
    public void EmptyInitialNamesSurviveAMoveAfterRenaming()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowTitleChanged(new WindowId(1), "renamed"));
        core.Handle(new WindowClassChanged(new WindowId(1), "renamed"));

        var moved = core.Handle(new Dispatch("move", "2"));

        Assert.AreEqual("", Client(moved, 1).InitialTitle);
        Assert.AreEqual("", Client(moved, 1).InitialClass);
    }

    [TestMethod]
    public void RecoveryJsonRestoresTheShownWorkspaceOfEachMonitor()
    {
        var screens = new[] { Display("left", 0, 1000, 800), Display("right", 1000, 1000, 800) };
        var prior = new WindowManager();
        prior.Handle(new MonitorsChanged(screens));
        prior.Handle(new Dispatch("workspace", "3"));
        prior.Handle(new WindowAppeared(new WindowId(1), Area));
        prior.Handle(new Dispatch("focus-monitor", "right"));
        prior.Handle(new Dispatch("workspace", "4"));
        prior.Handle(new WindowAppeared(new WindowId(2), screens[1].WorkArea));
        Assert.IsTrue(RecoverySnapshot.TryParse(prior.Recovery().ToJson(), out var saved));
        var core = new WindowManager();
        core.Handle(new MonitorsChanged(screens));
        core.Restore(saved);
        foreach (var client in saved.Clients)
            core.Handle(new WindowAppeared(client.Window, Area));

        var restored = core.Handle(new RestoredAdopted());

        Assert.AreEqual("3", Assert.ContainsSingle(screen => screen.Monitor.Name == "left", restored.State.Screens).ActiveWorkspace.Name);
        Assert.AreEqual("4", Assert.ContainsSingle(screen => screen.Monitor.Name == "right", restored.State.Screens).ActiveWorkspace.Name);
        Assert.IsEmpty(restored.State.Hidden);
    }

    [TestMethod]
    public void AZeroMonitorReportUsesFallbackAndReturnsTheFloatingClient()
    {
        var core = new WindowManager();
        var screen = Display("right", 2000, 1000, 800);
        core.Handle(new MonitorsChanged([screen]));
        var opened = core.Handle(new WindowAppeared(new WindowId(1), screen.WorkArea)
        {
            Traits = WindowTraits.Normal with { Dialog = true },
            Bounds = new Rectangle(2200, 200, 300, 200),
        });

        var gone = core.Handle(new MonitorsChanged([]));

        Assert.AreEqual("FALLBACK", Assert.ContainsSingle(gone.State.Screens).Monitor.Name);
        Assert.IsInRange(0, 1920 - 300, Client(gone, 1).Bounds.X);
        var back = core.Handle(new MonitorsChanged([screen]));
        Assert.AreEqual(Client(opened, 1).Bounds, Client(back, 1).Bounds);
        Assert.IsEmpty(back.State.Hidden);
    }

    [TestMethod]
    public void ClosingTheLastSpecialClientOnAnotherMonitorLeavesFocusHere()
    {
        var core = new WindowManager();
        core.Handle(new MonitorsChanged([Display("left", 0, 1000, 800), Display("right", 1000, 1000, 800)]));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new Dispatch("focus-monitor", "right"));
        core.Handle(new WindowAppeared(new WindowId(2), new Rectangle(1000, 0, 1000, 800)));
        core.Handle(new Dispatch("scratchpad", "tools"));
        core.Handle(new WindowAppeared(new WindowId(3), new Rectangle(1000, 0, 1000, 800)));
        core.Handle(new Dispatch("focus-monitor", "left"));

        var closed = core.Handle(new WindowClosed(new WindowId(3)));

        Assert.IsNull(closed.Focus);
        Assert.AreEqual(new MonitorId(0), closed.State.ActiveMonitor);
        Assert.AreEqual("0x1", Json(core.Execute("j/get window").Text).GetProperty("address").GetString());
    }

    [TestMethod]
    public void APinnedGuestThatShowsItselfStaysVisible()
    {
        var core = new WindowManager();
        var left = Display("left", 0, 1000, 800);
        var right = Display("right", 1000, 1000, 800);
        core.Handle(new MonitorsChanged([left, right]));
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new Dispatch("focus-monitor", "right"));
        core.Handle(new WindowAppeared(new WindowId(2), right.WorkArea) { Traits = WindowTraits.Normal with { Dialog = true } });
        core.Handle(new Dispatch("pin"));
        core.Handle(new MonitorsChanged([left]));

        var shown = core.Handle(new WindowAppeared(new WindowId(2), Area));

        Assert.DoesNotContain(new WindowId(2), shown.Hide);
        Assert.DoesNotContain(client => client.Window == new WindowId(2), shown.State.Hidden);
    }

    [TestMethod]
    public void AnOldRecoveryFileWithBothFullscreenFieldsStillRestoresItsMode()
    {
        Assert.IsTrue(RecoverySnapshot.TryParse("""
            {"clients":[{"window":1,"workspace":"1","fullscreen":2,"fullscreenClient":2}]}
            """, out var saved));

        Assert.AreEqual(FullscreenMode.Fullscreen, Assert.ContainsSingle(saved.Clients).Fullscreen);
        Assert.DoesNotContain("fullscreenClient", saved.ToJson(), StringComparison.Ordinal);
    }
}
