namespace Winland.Domain.Tests;

[TestClass]
public class FocusStateTests
{
    private static readonly Rectangle Area = new(0, 0, 1920, 1080);

    [TestMethod]
    public void TakingFocusIsRememberedByTheWorkspace()
    {
        var focus = new FocusState(_ => true);
        var desk = Desk(1, 2);

        focus.Take(Window(1), desk);

        Assert.AreEqual(Window(1), focus.Focused);
        Assert.AreEqual(Window(1), desk.Focus);
        Assert.AreEqual(Window(1), focus.Remembered(desk));
    }

    [TestMethod]
    public void FocusWithoutAWorkspaceLeavesEveryWorkspaceAsItWas()
    {
        var focus = new FocusState(_ => true);
        var desk = Desk(1, 2);
        focus.Take(Window(1), desk);

        focus.Take(Window(2));

        Assert.AreEqual(Window(2), focus.Focused);
        Assert.AreEqual(Window(1), desk.Focus);
    }

    [TestMethod]
    public void AWorkspaceDoesNotGiveFocusBackToAWindowThatTakesNone()
    {
        var focus = new FocusState(window => window != Window(2));
        var desk = Desk(1, 2);
        FocusState.Remember(desk, Window(2));

        Assert.IsNull(focus.Remembered(desk));
    }

    [TestMethod]
    public void AWorkspaceWithNothingRememberedGivesFocusToItsLastVisibleClient()
    {
        var focus = new FocusState(_ => true);
        var desk = Desk(1, 2);

        Assert.AreEqual(desk.LastVisible(), focus.Remembered(desk));
    }

    [TestMethod]
    public void TheHistoryIsNewestFirstAndRanksRecency()
    {
        var focus = new FocusState(_ => true);
        focus.Confirm(Window(1));
        focus.Confirm(Window(2));
        focus.Confirm(Window(1));

        Assert.AreSequenceEqual([Window(1), Window(2)], focus.History);
        Assert.IsTrue(focus.Recency(Window(1)) > focus.Recency(Window(2)));
        Assert.AreEqual(0, focus.Recency(Window(3)));
    }

    [TestMethod]
    public void RecentIsTheNewestOnTheWorkspaceThatCanTakeFocus()
    {
        var focus = new FocusState(window => window != Window(2));
        var desk = Desk(1, 2);
        focus.Confirm(Window(1));
        focus.Confirm(Window(9));
        focus.Confirm(Window(2));

        Assert.AreEqual(Window(1), focus.Recent(desk));
    }

    [TestMethod]
    public void AnUnmanagedWindowThatBecomesAClientHandsBackThePriorFocusOnce()
    {
        var focus = new FocusState(_ => true);
        focus.Take(Window(1));
        focus.UnmanagedTook(Window(5));

        Assert.AreEqual(Window(1), focus.Adopted(Window(5)));
        Assert.IsNull(focus.Adopted(Window(5)));
    }

    [TestMethod]
    public void AClosedWindowLeavesTheHistoryAndTheUnmanagedMarks()
    {
        var focus = new FocusState(_ => true);
        focus.Confirm(Window(1));
        focus.Take(Window(1));
        focus.UnmanagedTook(Window(5));

        focus.Forget(Window(1));
        focus.Forget(Window(5));

        Assert.IsEmpty(focus.History);
        Assert.IsNull(focus.Unmanaged);
        Assert.IsNull(focus.BeforeUnmanaged);
    }

    [TestMethod]
    public void ForgettingAnotherWindowKeepsTheUnmanagedMarks()
    {
        var focus = new FocusState(_ => true);
        focus.Take(Window(1));
        focus.UnmanagedTook(Window(5));

        focus.Forget(Window(9));

        Assert.AreEqual(Window(5), focus.Unmanaged);
        Assert.AreEqual(Window(1), focus.BeforeUnmanaged);

        focus.Forget(Window(1));
        Assert.AreEqual(Window(5), focus.Unmanaged);
        Assert.IsNull(focus.BeforeUnmanaged);
    }

    [TestMethod]
    public void AfterLeavingTheOneRevealedWindowTakesFocusAndOtherwiseTheLastVisible()
    {
        var desk = Desk(1, 2, 3);

        Assert.AreEqual(Window(2), FocusState.AfterLeaving(desk, [Window(2)]));
        Assert.AreEqual(desk.LastVisible(), FocusState.AfterLeaving(desk, []));
        Assert.AreEqual(desk.LastVisible(), FocusState.AfterLeaving(desk, [Window(1), Window(2)]));
    }

    private static LiveWorkspace Desk(params long[] windows)
    {
        var layout = new DwindleLayout();
        layout.Fit(Area, Area);
        foreach (var window in windows)
            layout.Add(Window(window), Area, null, false, new Rectangle(0, 0, 400, 300), WorkspaceId.Initial, "app", "title", monitor: Area);

        return new LiveWorkspace(WorkspaceId.Initial, MonitorId.Primary, layout);
    }

    [TestMethod]
    public void RecentSkipsAGroupMemberThatIsWaitingHidden()
    {
        var focus = new FocusState(_ => true);
        var desk = Desk(1);
        var group = desk.StartGroup(Window(1));
        desk.Join(group, Window(2), new Taken(false, false, Area), 1);
        focus.Confirm(Window(1));
        focus.Confirm(Window(2));

        Assert.AreEqual(Window(1), focus.Recent(desk));
    }

    private static WindowId Window(long id) => new(id);
}
