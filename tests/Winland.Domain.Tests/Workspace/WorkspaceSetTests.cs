namespace Winland.Domain.Tests;

[TestClass]
public class WorkspaceSetTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);
    private static readonly MonitorId Left = new(0);
    private static readonly MonitorId Right = new(1);

    private static WorkspaceId Number(long number, MonitorId monitor) => WorkspaceId.FromNumber(number, monitor);

    [TestMethod]
    public void TheFirstFreeNumberIsTheLowestNoWorkspaceOnThatMonitorHas()
    {
        var set = new WorkspaceSet();
        Add(set, Number(1, Left));
        Add(set, Number(2, Left));
        Add(set, Number(1, Right));

        Assert.AreEqual(3, set.FirstFree(Left));
        Assert.AreEqual(Number(2, Right), set.NextFree(Right));
    }

    [TestMethod]
    public void AGuestIsNumberedAfterTheHostsWorkspacesAndAtLeastElevenWhenOneToTenAreTaken()
    {
        var set = new WorkspaceSet();
        Add(set, Number(4, Left));
        Assert.AreEqual(5, set.NextGuestNumber(Left));

        for (var number = 1; number <= 10; number++)
        {
            if (number != 4)
                Add(set, Number(number, Right));
        }

        Add(set, Number(4, Right));
        Assert.AreEqual(11, set.NextGuestNumber(Right));
    }

    [TestMethod]
    public void AMonitorThatLeavesTurnsItsWorkspacesIntoGuestsAndDropsTheEmptyOnes()
    {
        var set = new WorkspaceSet();
        set.Shown.SetLive([Left, Right]);
        Add(set, Number(1, Left));
        var kept = Add(set, Number(2, Right), windows: 1);
        Add(set, Number(3, Right));
        set.Shown.Show(Right, Number(2, Right));

        var renames = set.Depart(Right, "DEL-1", Left);

        Assert.AreSequenceEqual([new WorkspaceRename(Number(2, Right), Number(2, Left))], renames);
        Assert.AreEqual(Number(2, Left), kept.Id);
        Assert.IsTrue(kept.IsGuest);
        Assert.IsNull(set.Find(Number(3, Right)));
    }

    [TestMethod]
    public void AMonitorThatComesBackTakesItsGuestsHomeAndSaysWhatItShowed()
    {
        var set = new WorkspaceSet();
        set.Shown.SetLive([Left, Right]);
        Add(set, Number(1, Left));
        var guest = Add(set, Number(2, Right), windows: 1);
        set.Shown.Show(Right, Number(2, Right));
        set.Depart(Right, "DEL-1", Left);

        var renames = set.Return(Right, "DEL-1", out var shownNumber);

        Assert.AreSequenceEqual([new WorkspaceRename(Number(2, Left), Number(2, Right))], renames);
        Assert.AreEqual(Number(2, Right), guest.Id);
        Assert.IsFalse(guest.IsGuest);
        Assert.AreEqual(2, shownNumber);
    }

    [TestMethod]
    public void ARenameIsFollowedByWhatTheMonitorShows()
    {
        var set = new WorkspaceSet();
        set.Shown.SetLive([Left]);
        var desk = Add(set, Number(1, Left));
        set.Shown.Show(Left, desk.Id);

        var rename = set.Rebind(desk, Number(7, Left));

        Assert.AreEqual(new WorkspaceRename(Number(1, Left), Number(7, Left)), rename);
        Assert.AreEqual(Number(7, Left), set.Shown.Shown(Left));
        Assert.IsNull(set.Rebind(desk, Number(7, Left)));
    }

    [TestMethod]
    public void OnlyAnEmptyWorkspaceThatIsNotShownOrPersistentIsDropped()
    {
        var set = new WorkspaceSet();
        Add(set, Number(1, Left));
        Add(set, Number(2, Left), windows: 1);

        set.DropIfEmpty(Number(1, Left), shown: true);
        set.DropIfEmpty(Number(2, Left), shown: false);
        Assert.AreEqual(2, set.Count);

        set.DropIfEmpty(Number(1, Left), shown: false);
        Assert.IsNull(set.Find(Number(1, Left)));
    }

    [TestMethod]
    public void ARemovedWorkspaceIsNoLongerShown()
    {
        var set = new WorkspaceSet();
        set.Shown.SetLive([Left]);
        var desk = Add(set, Number(1, Left));
        set.Shown.Show(Left, desk.Id);

        set.Remove(desk);

        Assert.IsNull(set.Shown.Shown(Left));
        Assert.IsEmpty(set);
    }

    [TestMethod]
    public void AGuestMovedByHandIsNoLongerAGuest()
    {
        var set = new WorkspaceSet();
        set.Shown.SetLive([Left, Right]);
        Add(set, Number(1, Left));
        var first = Add(set, Number(1, Right), windows: 1);
        set.Depart(Right, "DEL-1", Left);
        Assert.IsTrue(first.IsGuest);

        set.MoveTo(first, Right);
        Assert.IsFalse(first.IsGuest);
    }

    [TestMethod]
    public void GuestsLeaveAndComeHomeInTheOrderOfTheirNumbers()
    {
        var set = new WorkspaceSet();
        set.Shown.SetLive([Left, Right]);
        Add(set, Number(1, Left));
        var three = Add(set, Number(3, Right), windows: 1);
        var two = Add(set, Number(2, Right), windows: 1);

        set.Depart(Right, "DEL-1", Left);
        Assert.AreEqual(Number(2, Left), two.Id);
        Assert.AreEqual(Number(3, Left), three.Id);

        set.Rebind(two, Number(7, Left));
        var renames = set.Return(Right, "DEL-1", out _);
        Assert.AreSequenceEqual([Number(7, Left), Number(3, Left)], renames.Select(rename => rename.From));
        Assert.AreEqual(Number(2, Right), two.Id);
        Assert.AreEqual(Number(3, Right), three.Id);
    }

    [TestMethod]
    public void NamedWorkspacesAreListedNewestFirstBeforeTheNumberedOnes()
    {
        var set = new WorkspaceSet();
        Add(set, Number(2, Left));
        Add(set, new WorkspaceId("web", Left.Value));
        Add(set, Number(1, Left));
        Add(set, new WorkspaceId("mail", Left.Value));
        Add(set, Number(1, Right));

        Assert.AreSequenceEqual(
            [new WorkspaceId("mail", Left.Value), new WorkspaceId("web", Left.Value), Number(1, Left), Number(2, Left)],
            set.Listed(Left));
    }

    private static long _nextWindow;

    private static LiveWorkspace Add(WorkspaceSet set, WorkspaceId id, int windows = 0)
    {
        var layout = new DwindleLayout();
        layout.Fit(Area, Area);
        for (var i = 0; i < windows; i++)
            layout.Add(new WindowId(Interlocked.Increment(ref _nextWindow)), Area, null, false, new Rectangle(0, 0, 400, 300), id, "app", "title", monitor: Area);

        var desk = new LiveWorkspace(id, new MonitorId(id.Monitor), layout);
        set.Add(desk);
        return desk;
    }
}
