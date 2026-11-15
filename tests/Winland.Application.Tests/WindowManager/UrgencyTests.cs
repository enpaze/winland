namespace Winland.Application.Tests;

public class UrgencyTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [Fact]
    public void AFlashingClientIsReportedUrgentAndKeepsFocusWhereItIs()
    {
        var core = TwoClients();

        var flashed = core.Handle(new WindowFlashed(new WindowId(1)));

        Assert.Equal(["urgent>>1"], Lines(flashed));
        Assert.Null(flashed.Focus);
    }

    [Fact]
    public void TheFocusedClientFlashingIsNotUrgent()
    {
        var core = TwoClients();

        var flashed = core.Handle(new WindowFlashed(new WindowId(2)));

        Assert.Empty(flashed.Events);
    }

    [Fact]
    public void AnUnmanagedWindowFlashingIsNotUrgent()
    {
        var core = TwoClients();

        var flashed = core.Handle(new WindowFlashed(new WindowId(99)));

        Assert.Empty(flashed.Events);
    }

    [Fact]
    public void AClientOnAHiddenWorkspaceTheUserFocusesBringsItsWorkspace()
    {
        var core = TwoClients();
        core.Handle(new Dispatch("focuswindow", "address:0x1"));
        core.Handle(new Dispatch("movetoworkspacesilent", "2"));

        var outcome = core.Handle(new WindowFocused(new WindowId(1), FocusCause.User));

        Assert.Equal(new WindowId(1), outcome.Focus);
        Assert.Equal(new WorkspaceId("2"), outcome.State.ActiveWorkspace);
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
