namespace Winland.Application.Tests;

[TestClass]
public class UrgencyTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void AFlashingClientIsReportedUrgentAndKeepsFocusWhereItIs()
    {
        var core = TwoClients();

        var flashed = core.Handle(new WindowFlashed(new WindowId(1)));

        Assert.AreSequenceEqual(["urgent>>1"], Lines(flashed));
        Assert.IsNull(flashed.Focus);
    }

    [TestMethod]
    public void TheFocusedClientFlashingIsNotUrgent()
    {
        var core = TwoClients();

        var flashed = core.Handle(new WindowFlashed(new WindowId(2)));

        Assert.IsEmpty(flashed.Events);
    }

    [TestMethod]
    public void AnUnmanagedWindowFlashingIsNotUrgent()
    {
        var core = TwoClients();

        var flashed = core.Handle(new WindowFlashed(new WindowId(99)));

        Assert.IsEmpty(flashed.Events);
    }

    [TestMethod]
    public void AClientOnAHiddenWorkspaceTheUserFocusesBringsItsWorkspace()
    {
        var core = TwoClients();
        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        core.Handle(new Dispatch("movetoworkspacesilent", "2"));

        var outcome = core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));

        Assert.AreEqual(new WindowId(1), outcome.Focus);
        Assert.AreEqual(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
        Assert.Contains(new WindowId(1), outcome.Show);
    }

    private static WindowManager TwoClients()
    {
        var core = new WindowManager();
        core.Handle(new WindowAppeared(new WindowId(1), Area));
        core.Handle(new WindowAppeared(new WindowId(2), Area));
        return core;
    }

    private static WindowManager ThreeClients()
    {
        var core = TwoClients();
        core.Handle(new WindowAppeared(new WindowId(3), Area));
        return core;
    }

    private static IReadOnlyList<string> Lines(Outcome outcome) =>
        CommandProtocol.FormatStream(outcome.Events, outcome.State);
}
