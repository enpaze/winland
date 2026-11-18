namespace Winland.Domain.Tests;

[TestClass]
public class MinimizedConcealmentTests
{
    private static readonly WindowId Kept = new(4);
    private static readonly WindowId Other = new(9);

    [TestMethod]
    public void MinimizedClientOnAnInactiveWorkspaceIsConcealedOnce()
    {
        var first = MinimizedConcealment.Plan([Inactive(Kept)], []);

        Assert.AreSequenceEqual([Kept], first.Hide);
        Assert.IsEmpty(first.Show);
        Assert.AreSequenceEqual([Kept], first.Concealed);

        var again = MinimizedConcealment.Plan([Inactive(Kept)], first.Concealed);

        Assert.IsEmpty(again.Hide);
        Assert.IsEmpty(again.Show);
        Assert.AreSequenceEqual([Kept], again.Concealed);
    }

    [TestMethod]
    public void ShowingItsWorkspaceShowsItStillMinimized()
    {
        var away = MinimizedConcealment.Plan([Inactive(Kept)], []);

        var back = MinimizedConcealment.Plan([Active(Kept)], away.Concealed);

        Assert.IsEmpty(back.Hide);
        Assert.AreSequenceEqual([Kept], back.Show);
        Assert.IsEmpty(back.Concealed);
    }

    [TestMethod]
    public void MinimizedClientOnTheShownWorkspaceIsLeftAlone()
    {
        var plan = MinimizedConcealment.Plan([Active(Kept)], []);

        Assert.IsEmpty(plan.Hide);
        Assert.IsEmpty(plan.Show);
        Assert.IsEmpty(plan.Concealed);
    }

    [TestMethod]
    public void RestoringDropsTheConcealmentWithoutShowingItMinimized()
    {
        var away = MinimizedConcealment.Plan([Inactive(Kept)], []);

        var restored = MinimizedConcealment.Plan(
            [new MinimizedPresence(Kept, WorkspaceShown: false, Minimized: false)],
            away.Concealed);

        Assert.IsEmpty(restored.Hide);
        Assert.IsEmpty(restored.Show);
        Assert.IsEmpty(restored.Concealed);
    }

    [TestMethod]
    public void ClosingDropsTheConcealment()
    {
        var away = MinimizedConcealment.Plan([Inactive(Kept), Active(Other)], []);

        var closed = MinimizedConcealment.Plan([Active(Other)], away.Concealed);

        Assert.IsEmpty(closed.Hide);
        Assert.IsEmpty(closed.Show);
        Assert.IsEmpty(closed.Concealed);
    }

    [TestMethod]
    public void OrdinaryClientIsNotPartOfTheMinimizedPlan()
    {
        var plan = MinimizedConcealment.Plan(
            [new MinimizedPresence(Kept, WorkspaceShown: false, Minimized: false)],
            []);

        Assert.IsEmpty(plan.Hide);
        Assert.IsEmpty(plan.Show);
        Assert.IsEmpty(plan.Concealed);
    }

    private static MinimizedPresence Inactive(WindowId window) =>
        new(window, WorkspaceShown: false, Minimized: true);

    private static MinimizedPresence Active(WindowId window) =>
        new(window, WorkspaceShown: true, Minimized: true);
}
