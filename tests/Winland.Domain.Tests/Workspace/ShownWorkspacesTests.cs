namespace Winland.Domain.Tests;

[TestClass]
public class ShownWorkspacesTests
{
    private static readonly MonitorId Left = new(0);
    private static readonly MonitorId Right = new(1);

    private static WorkspaceId Number(long number, MonitorId monitor) => WorkspaceId.FromNumber(number, monitor);

    [TestMethod]
    public void AMonitorThatJustBecameLiveHasNoShownWorkspaceButStillAnswersWhatItShowedLast()
    {
        var shown = new ShownWorkspaces();
        shown.Show(Left, Number(3, Left));

        shown.SetLive([Left]);

        Assert.IsNull(shown.Shown(Left));
        Assert.AreEqual(Number(3, Left), shown.ActiveOn(Left));
    }

    [TestMethod]
    public void AShowOnALiveMonitorIsItsShownWorkspace()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left, Right]);

        shown.Show(Left, Number(2, Left));

        Assert.AreEqual(Number(2, Left), shown.Shown(Left));
        Assert.IsNull(shown.Shown(Right));
        Assert.AreEqual(WorkspaceId.Initial, shown.ActiveOn(Right));
    }

    [TestMethod]
    public void AMonitorThatLeavesKeepsWhatItShowedLast()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left, Right]);
        shown.Show(Right, Number(4, Right));

        shown.SetLive([Left]);

        Assert.IsNull(shown.Shown(Right));
        Assert.AreEqual(Number(4, Right), shown.LastShown(Right));
        Assert.AreEqual(Number(4, Right), shown.ActiveOn(Right));
    }

    [TestMethod]
    public void ASpecialWorkspaceShowsOverALiveMonitorUntilItCloses()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left]);
        var scratch = new WorkspaceId("special:scratch");

        shown.ShowSpecial(Left, scratch);
        var open = shown.SpecialOn(Left);
        shown.ShowSpecial(Left, null);

        Assert.AreEqual(scratch, open);
        Assert.IsNull(shown.SpecialOn(Left));
    }

    [TestMethod]
    public void ARenameMovesTheShownWorkspaceAndItsVisits()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left]);
        shown.Show(Left, Number(1, Left));
        shown.Visit(Number(2, Left), Left);
        shown.Visit(Number(1, Left), Left);

        shown.Rename(Number(2, Left), Number(7, Left));

        Assert.AreEqual(Number(7, Left), shown.Last(Left));
    }

    [TestMethod]
    public void ARemovedWorkspaceIsNoLongerShownOrVisited()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left]);
        shown.Visit(Number(2, Left), Left);
        shown.Visit(Number(1, Left), Left);
        shown.Show(Left, Number(1, Left));

        shown.Remove(Number(1, Left));

        Assert.IsNull(shown.Shown(Left));
        Assert.AreEqual(WorkspaceId.Initial, shown.ActiveOn(Left));
    }

    [TestMethod]
    public void LastIsTheVisitBeforeEvenOnAnotherMonitor()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left, Right]);
        shown.Visit(Number(5, Left), Left);
        shown.Visit(Number(1, Right), Right);
        shown.Visit(Number(1, Left), Left);
        shown.Show(Left, Number(1, Left));

        Assert.AreEqual(Number(1, Right), shown.Last(Left));
    }

    [TestMethod]
    public void WithoutCyclesTheSameWorkspaceTwiceIsOneVisit()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left]);
        shown.Visit(Number(2, Left), Left);
        shown.Visit(Number(1, Left), Left);
        shown.Visit(Number(1, Left), Left);
        shown.Show(Left, Number(1, Left));

        Assert.AreEqual(Number(2, Left), shown.Last(Left));
    }

    [TestMethod]
    public void OnlyWhatAnEnabledMonitorShowsIsShown()
    {
        var shown = new ShownWorkspaces();
        shown.SetLive([Left, Right], enabled: [Left]);
        shown.Show(Left, Number(1, Left));
        shown.Show(Right, Number(1, Right));

        Assert.IsTrue(shown.IsShown(Number(1, Left)));
        Assert.IsFalse(shown.IsShown(Number(1, Right)));
        Assert.IsTrue(shown.ShowsAWorkspace(Left));
        Assert.IsFalse(shown.ShowsAWorkspace(Right));
    }

    [TestMethod]
    public void ASpecialWorkspaceIsShownOverTheEnabledMonitorThatOpensIt()
    {
        var shown = new ShownWorkspaces();
        var scratch = new WorkspaceId("special:scratch");
        shown.SetLive([Left, Right]);
        shown.ShowSpecial(Right, scratch);

        Assert.AreEqual(Right, shown.ShowingSpecial(scratch));
        Assert.IsTrue(shown.IsShown(scratch));

        shown.SetLive([Left, Right], enabled: [Left]);
        Assert.IsNull(shown.ShowingSpecial(scratch));
    }

    [TestMethod]
    public void WithNoMonitorReportedThePrimaryShowsWhatItShowedLast()
    {
        var shown = new ShownWorkspaces();
        shown.Show(MonitorId.Primary, Number(3, MonitorId.Primary));

        Assert.IsTrue(shown.IsShown(Number(3, MonitorId.Primary)));
        Assert.IsFalse(shown.IsShown(Number(4, MonitorId.Primary)));
        Assert.IsTrue(shown.ShowsAWorkspace(Left));
    }
}
